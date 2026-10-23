using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class MonsterSpawnEntry
{
    public Transform spawnPoint;
    public BoulderMonster monsterPrefab;
    [Min(1)] public int count = 1;
    [Min(0.1f)] public float intervalSeconds = 0.5f;
}

/// <summary>One encounter per room attempt, activated only by the shared boulder.</summary>
[RequireComponent(typeof(BoxCollider))]
public class MonsterSpawnZone : MonoBehaviour
{
    [Tooltip("Optional stable save identifier. Leave blank to use this scene hierarchy path.")]
    [SerializeField] private string encounterId;
    [Tooltip("Every entry uses a position you explicitly assign; there is no random spawning.")]
    [SerializeField] private MonsterSpawnEntry[] entries = Array.Empty<MonsterSpawnEntry>();
    [SerializeField] private BoulderMonster[] randomMeleePool = Array.Empty<BoulderMonster>();
    [SerializeField, Min(0.1f)] private float navMeshSnapDistance = 1.5f;
    [Tooltip("Wait until the boulder's center passes the trigger's local forward plane.")]
    [SerializeField] private bool requireCenterPastPlane;
    private readonly List<BoulderMonster> monsters = new();
    private bool triggered;
    private int[] spawnedCounts;
    public bool HasTriggered => triggered;
    public BoulderMonster FirstLivingMonster
    {
        get
        {
            foreach (BoulderMonster monster in monsters)
                if (monster != null && monster.IsSpawned && !monster.IsDead) return monster;
            return null;
        }
    }
    public bool IsEncounterCleared
    {
        get
        {
            if (!triggered || spawnedCounts == null) return false;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && spawnedCounts[i] < Mathf.Max(1, entries[i].count)) return false;
            return FirstLivingMonster == null;
        }
    }

    public string EncounterId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(encounterId)) return encounterId;
            string path = "";
            for (Transform part = transform; part != null; part = part.parent)
                path = "/" + part.name + "[" + part.GetSiblingIndex() + "]" + path;
            return gameObject.scene.name + path;
        }
    }

    private static bool ServerRunning => NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening;

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnValidate()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
    }

    private void Update()
    {
        if (ServerRunning && triggered && StageManager.Instance != null &&
            !StageManager.Instance.IsGameStarted)
            ResetEncounter();
    }

    private void OnTriggerEnter(Collider other) => TryActivate(other);
    private void OnTriggerStay(Collider other) => TryActivate(other);

    public bool ActivateFromMechanic(Vector3 focus, Collider floor, int desiredCount = 3)
    {
        if (!ServerRunning || triggered || entries == null || entries.Length == 0 || floor == null ||
            StageManager.Instance == null || !StageManager.Instance.IsGameStarted ||
            StageManager.Instance.RoomTimerFinished) return false;
        var points = new List<Vector3>();
        int activeEntries = Mathf.Clamp(desiredCount, 1, entries.Length);
        for (int index = 0; index < activeEntries; index++)
        {
            bool found = false;
            for (int attempt = 0; attempt < 96; attempt++)
            {
                float angle = (attempt * 137.5f + index * 120f) * Mathf.Deg2Rad;
                float radius = 8f + attempt / 12 * 3f;
                Vector3 desired = focus + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if (!floor.Raycast(new Ray(desired+Vector3.up*60,Vector3.down),out RaycastHit ground,120) ||
                    !NavMesh.SamplePosition(ground.point,out NavMeshHit nav,1f,NavMesh.AllAreas)) continue;
                bool blocked = false;
                foreach(Vector3 chosen in points) if(Vector3.Distance(chosen,nav.position)<3.5f)blocked=true;
                if(Physics.OverlapCapsule(nav.position+Vector3.up*.7f,nav.position+Vector3.up*2.1f,
                    .55f,~0,QueryTriggerInteraction.Ignore).Length>0)blocked=true;
                if(blocked)continue;
                points.Add(nav.position);found=true;break;
            }
            if(!found || entries[index] == null || entries[index].spawnPoint == null)
            {
                Debug.LogWarning("Cannot find safe ambush positions near the broken cannon control.",this);
                return false;
            }
        }
        for(int i=0;i<points.Count;i++)entries[i].spawnPoint.position=points[i];
        triggered=true;
        spawnedCounts=new int[entries.Length];
        // Only one monster per selected entry; mark the remainder complete so
        // saving and resuming cannot recreate the entries intentionally skipped.
        for (int i=0;i<entries.Length;i++)
            if(entries[i]!=null)spawnedCounts[i]=Mathf.Max(1,entries[i].count)-(i<activeEntries?1:0);
        StartCoroutine(SpawnRemaining());
        return true;
    }

    private void TryActivate(Collider other)
    {
        if (!ServerRunning || triggered || entries == null || entries.Length == 0 ||
            StageManager.Instance == null || !StageManager.Instance.IsGameStarted ||
            StageManager.Instance.RoomTimerFinished ||
            other.GetComponentInParent<BoulderHealth>() == null)
            return;
        if (requireCenterPastPlane)
        {
            Vector3 center = other.attachedRigidbody != null
                ? other.attachedRigidbody.worldCenterOfMass : other.bounds.center;
            if (Vector3.Dot(center - transform.position, transform.forward) < 0f)
                return;
        }
        triggered = true;
        spawnedCounts = new int[entries.Length];
        StartCoroutine(SpawnRemaining());
    }

    private IEnumerator SpawnRemaining()
    {
        for (int index = 0; index < entries.Length; index++)
        {
            MonsterSpawnEntry entry = entries[index];
            if (entry == null || entry.spawnPoint == null || entry.monsterPrefab == null)
            {
                Debug.LogWarning($"{name}: spawn entry {index + 1} needs a point and monster prefab.", this);
                continue;
            }
            while (spawnedCounts[index] < Mathf.Max(1, entry.count))
            {
                if (!ServerRunning || StageManager.Instance == null ||
                    !StageManager.Instance.IsGameStarted || StageManager.Instance.RoomTimerFinished)
                    yield break;
                SpawnMonster(index, entry.spawnPoint.position, entry.spawnPoint.rotation, 0);
                spawnedCounts[index]++;
                if (spawnedCounts[index] < Mathf.Max(1, entry.count))
                    yield return new WaitForSeconds(Mathf.Max(0.1f, entry.intervalSeconds));
            }
        }
    }

    private void SpawnMonster(int entryIndex, Vector3 position, Quaternion rotation, int health, int savedVariant = 0)
    {
        if (entryIndex < 0 || entryIndex >= entries.Length || entries[entryIndex] == null ||
            entries[entryIndex].monsterPrefab == null) return;
        BoulderMonster prefab = entries[entryIndex].monsterPrefab;
        int variant = 0;
        if (prefab.RequiresNavigation && randomMeleePool != null && randomMeleePool.Length > 0)
        {
            int choice = health > 0 ? savedVariant - 1 : UnityEngine.Random.Range(0, randomMeleePool.Length);
            if (choice >= 0 && choice < randomMeleePool.Length && randomMeleePool[choice] != null &&
                randomMeleePool[choice].RequiresNavigation)
            {
                prefab = randomMeleePool[choice];
                variant = choice + 1;
            }
        }
        if (prefab.RequiresNavigation)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
            {
                Debug.LogWarning($"{name}: spawn point {entryIndex + 1} is not on the monster NavMesh.", this);
                return;
            }
            position = hit.position;
        }
        BoulderMonster monster = Instantiate(prefab, position, rotation);
        monster.SpawnEntryIndex = entryIndex;
        monster.SpawnVariant = variant;
        monster.PrepareSavedHealth(health);
        monster.GetComponent<NetworkObject>().Spawn(true);
        monsters.Add(monster);
    }

    private void ResetEncounter()
    {
        StopAllCoroutines();
        triggered = false;
        spawnedCounts = null;
        monsters.Clear();
    }

    public static void ResetAllForRetry()
    {
        if (!ServerRunning) return;
        MonsterShell.ClearAll();
        foreach (BoulderMonster monster in FindObjectsByType<BoulderMonster>(FindObjectsSortMode.None))
            if (monster.IsSpawned) monster.NetworkObject.Despawn(true);
        foreach (MonsterSpawnZone zone in FindObjectsByType<MonsterSpawnZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            zone.ResetEncounter();
    }

    public static void CompleteActiveEncounters()
    {
        if (!ServerRunning) return;
        MonsterShell.ClearAll();
        foreach (MonsterSpawnZone zone in FindObjectsByType<MonsterSpawnZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!zone.triggered) continue;
            zone.StopAllCoroutines();
            zone.spawnedCounts = new int[zone.entries.Length];
            for (int index = 0; index < zone.entries.Length; index++)
                zone.spawnedCounts[index] = zone.entries[index] != null
                    ? Mathf.Max(1, zone.entries[index].count) : 0;
        }
        foreach (BoulderMonster monster in FindObjectsByType<BoulderMonster>(FindObjectsSortMode.None))
            monster.DefeatForStageCompletion();
    }

    public static SavedMonsterEncounter[] CaptureAll()
    {
        var result = new List<SavedMonsterEncounter>();
        foreach (MonsterSpawnZone zone in FindObjectsByType<MonsterSpawnZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!zone.triggered) continue;
            var alive = new List<SavedMonsterState>();
            foreach (BoulderMonster monster in zone.monsters)
                if (monster != null && monster.IsSpawned && !monster.IsDead)
                    alive.Add(new SavedMonsterState
                    {
                        entryIndex = monster.SpawnEntryIndex,
                        variant = monster.SpawnVariant,
                        health = monster.CurrentHealth,
                        position = monster.transform.position,
                        rotation = monster.transform.rotation
                    });
            result.Add(new SavedMonsterEncounter
            {
                encounterId = zone.EncounterId,
                spawnedCounts = zone.spawnedCounts,
                monsters = alive.ToArray()
            });
        }
        return result.ToArray();
    }

    public static void RestoreAll(SavedMonsterEncounter[] saved)
    {
        if (!ServerRunning) return;
        ResetAllForRetry();
        if (saved == null) return;
        foreach (MonsterSpawnZone zone in FindObjectsByType<MonsterSpawnZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SavedMonsterEncounter match = Array.Find(saved,
                item => item != null && item.encounterId == zone.EncounterId);
            if (match == null) continue;
            zone.triggered = true;
            zone.spawnedCounts = new int[zone.entries.Length];
            if (match.spawnedCounts != null)
                Array.Copy(match.spawnedCounts, zone.spawnedCounts,
                    Mathf.Min(match.spawnedCounts.Length, zone.spawnedCounts.Length));
            if (match.monsters != null)
                foreach (SavedMonsterState monster in match.monsters)
                    if (monster != null && monster.health > 0)
                        zone.SpawnMonster(monster.entryIndex, monster.position, monster.rotation, monster.health, monster.variant);
            if (zone.isActiveAndEnabled) zone.StartCoroutine(zone.SpawnRemaining());
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Gizmos.color = new Color(0f, 0.85f, 1f, 0.75f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;
        if (entries == null) return;
        foreach (MonsterSpawnEntry entry in entries)
            if (entry != null && entry.spawnPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(entry.spawnPoint.position + Vector3.up * 0.8f, 0.5f);
                Gizmos.DrawLine(transform.position, entry.spawnPoint.position);
            }
    }
}
