using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>Three separated, server-placed cannon controls, randomized on boulder retries.</summary>
[RequireComponent(typeof(NetworkObject))]
public class Stage8CannonControls : NetworkBehaviour
{
    [SerializeField] private Stage5BallLauncher[] cannons;
    [SerializeField] private Transform[] weakpoints;
    [SerializeField] private Collider inclinePlate;
    [SerializeField] private float minimumSpacing = 40f;
    [SerializeField] private float edgeMargin = 5f;
    [SerializeField] private int controlHealth = 3;
    [SerializeField] private BoxCollider[] hidingWalls;
    [SerializeField] private MonsterSpawnZone[] breakAmbushes;
    private NetworkList<Vector3> positions;
    private NetworkList<Quaternion> rotations;
    private NetworkList<int> health;
    private bool initialized;
    private static Stage8CannonControls instance;

    private void Awake()
    {
        positions = new NetworkList<Vector3>();
        rotations = new NetworkList<Quaternion>();
        health = new NetworkList<int>();
    }
    public override void OnNetworkSpawn()
    {
        instance = this;
        positions.OnListChanged += OnPositionsChanged;
        rotations.OnListChanged += OnRotationsChanged;
        health.OnListChanged += OnHealthChanged;
        Refresh();
    }
    public override void OnNetworkDespawn()
    {
        positions.OnListChanged -= OnPositionsChanged;
        rotations.OnListChanged -= OnRotationsChanged;
        health.OnListChanged -= OnHealthChanged;
        if (instance == this) instance = null;
    }
    private void Update()
    {
        if (!IsServer) return;
        StageManager stage = StageManager.Instance;
        if (stage == null || !stage.IsGameStarted || stage.CurrentSection != 8)
        {
            initialized = false;
            return;
        }
        if (!initialized) Randomize();
    }
    private void OnPositionsChanged(NetworkListEvent<Vector3> change) => Refresh();
    private void OnRotationsChanged(NetworkListEvent<Quaternion> change) => Refresh();
    private void OnHealthChanged(NetworkListEvent<int> change) => Refresh();
    private void Refresh()
    {
        for (int i = 0; i < weakpoints.Length; i++)
        {
            if (i < positions.Count) weakpoints[i].position = positions[i];
            if (i < rotations.Count) weakpoints[i].rotation = rotations[i];
            bool active = i < health.Count && health[i] > 0;
            weakpoints[i].gameObject.SetActive(active);
            if (i < cannons.Length && cannons[i] != null)
                cannons[i].SetControlDisabled(!active);
        }
    }
    public void Hit(int index, int amount)
    {
        if (!IsServer || !IsSpawned || amount <= 0 || index < 0 || index >= health.Count ||
            health[index] <= 0 || StageManager.Instance == null ||
            !StageManager.Instance.IsGameStarted || StageManager.Instance.CurrentSection != 8) return;
        health[index] = Mathf.Max(0, health[index] - amount);
        if (health[index] == 0 && breakAmbushes != null && index < breakAmbushes.Length &&
            breakAmbushes[index] != null)
        {
            Vector3 focus = positions[index];
            if (inclinePlate.Raycast(new Ray(focus+Vector3.up*60,Vector3.down),out RaycastHit floor,150))
                focus = floor.point;
            breakAmbushes[index].ActivateFromMechanic(focus, inclinePlate, Random.Range(2,4));
        }
    }
    public static void ResetForBoulderRetry()
    {
        if (instance != null && instance.IsServer && StageManager.Instance != null &&
            StageManager.Instance.CurrentSection == 8) instance.Randomize();
    }
    private void Randomize()
    {
        if (!IsServer || inclinePlate == null) return;
        Vector3[] chosen = new Vector3[weakpoints.Length];
        Quaternion[] chosenRotations = new Quaternion[weakpoints.Length];
        Quaternion slope = Quaternion.Euler(-14, 0, 0);
        Vector3 start = inclinePlate.transform.position;
        List<Vector3> candidates = new List<Vector3>();
        List<Quaternion> facings = new List<Quaternion>();
        // Hide on the uphill back faces of existing cover, without cabinets or labels.
        if (hidingWalls != null)
            foreach (BoxCollider wall in hidingWalls)
            {
                if (wall == null) continue;
                for (int i = 0; i < 24; i++)
                {
                    Vector3 local = wall.center + new Vector3(Random.Range(-.38f,.38f)*wall.size.x,
                        Random.Range(-.25f,.3f)*wall.size.y, wall.size.z*.5f);
                    Vector3 normal = wall.transform.forward;
                    Vector3 point = wall.transform.TransformPoint(local) + normal*.22f;
                    if (!OverPlate(point)) continue;
                    candidates.Add(point);
                    facings.Add(Quaternion.LookRotation(-normal, wall.transform.up));
                }
            }
        // Looking up near the entrance is another valid discovery, still entirely
        // inside the incline's horizontal footprint and within handgun range.
        for (int i = 0; i < 32; i++)
        {
            Vector3 point = start + slope*new Vector3(Random.Range(-44f,44f),0,Random.Range(8f,22f)) +
                Vector3.up*Random.Range(14f,26f);
            if (!OverPlate(point)) continue;
            candidates.Add(point);
            facings.Add(Quaternion.LookRotation(point-(start+Vector3.up*2),Vector3.up));
        }
        bool found = false;
        for (int attempt = 0; attempt < 2000 && candidates.Count >= weakpoints.Length; attempt++)
        {
            bool valid = true;
            for (int i = 0; i < chosen.Length; i++)
            {
                int candidate = Random.Range(0, candidates.Count);
                chosen[i] = candidates[candidate];
                chosenRotations[i] = facings[candidate];
                if (i < positions.Count && Vector3.Distance(chosen[i], positions[i]) < 12f) valid = false;
                for (int j = 0; j < i; j++)
                    if (Vector3.Distance(chosen[i], chosen[j]) < minimumSpacing) valid = false;
            }
            if (valid) { found = true; break; }
        }
        if (!found)
        {
            Debug.LogError("No valid separated cannon control layout; cannons remain disabled.", this);
            foreach (Stage5BallLauncher cannon in cannons) cannon.SetControlDisabled(true);
            return;
        }
        positions.Clear(); rotations.Clear(); health.Clear();
        for (int i = 0; i < chosen.Length; i++)
        { positions.Add(chosen[i]); rotations.Add(chosenRotations[i]); health.Add(controlHealth); }
        initialized = true;
        Refresh();
        foreach (Stage5Projectile projectile in FindObjectsByType<Stage5Projectile>(FindObjectsSortMode.None))
            if (projectile.IsSpawned && projectile.ActiveSection == 8) projectile.NetworkObject.Despawn(true);
    }
    private bool OverPlate(Vector3 point)
    {
        foreach (Vector3 offset in new[] {new Vector3(-1.2f,0,-1.2f),new Vector3(1.2f,0,-1.2f),
            new Vector3(-1.2f,0,1.2f),new Vector3(1.2f,0,1.2f)})
            if (!inclinePlate.Raycast(new Ray(point+offset+Vector3.up*100,Vector3.down),out RaycastHit hit,300)) return false;
        return Mathf.Abs(point.x-inclinePlate.transform.position.x) < 60f-edgeMargin;
    }
    public static SavedCannonControl[] CaptureCurrent()
    {
        if (instance == null || !instance.IsServer || !instance.initialized) return null;
        var result = new SavedCannonControl[instance.positions.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = new SavedCannonControl { position = instance.positions[i], rotation = instance.rotations[i],
                hiddenMount = true, health = instance.health[i] };
        return result;
    }
    public static void RestoreCurrent(SavedCannonControl[] saved)
    {
        if (instance == null || !instance.IsServer || saved == null || saved.Length != instance.weakpoints.Length) return;
        if (System.Array.Exists(saved, control => control == null)) return;
        if (System.Array.Exists(saved, control => !control.hiddenMount))
        {
            instance.Randomize();
            for (int i=0;i<saved.Length;i++)
                if(i<instance.health.Count)instance.health[i]=Mathf.Clamp(saved[i].health,0,instance.controlHealth);
            return;
        }
        instance.positions.Clear(); instance.rotations.Clear(); instance.health.Clear();
        foreach (SavedCannonControl control in saved)
        {
            instance.positions.Add(control.position);
            instance.rotations.Add(control.rotation);
            instance.health.Add(Mathf.Clamp(control.health, 0, instance.controlHealth));
        }
        instance.initialized = true;
        instance.Refresh();
    }
}
