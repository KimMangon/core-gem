using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class Stage1MonsterEncounterSetup
{
    [MenuItem("Tools/Monsters/Place Stage 1 Encounter")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().name != "Stage1")
            throw new InvalidOperationException("Open Stage1 in Edit Mode.");
        Transform track = GameObject.Find("MountainTrack")?.transform;
        if (track == null) throw new InvalidOperationException("MountainTrack is missing.");
        Transform group = track.Find("Stage1MonsterEncounter");
        if (group == null)
        {
            group = new GameObject("Stage1MonsterEncounter").transform;
            group.SetParent(track, false);
        }

        Transform turn = track.Find("Turn_02");
        Transform platform = group.Find("MonsterSpawnPlatform");
        if (platform == null)
        {
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            platform.name = "MonsterSpawnPlatform";
            platform.SetParent(group, false);
        }
        // Same surface height as Turn_02; overlap its outer edge to avoid a gap or step.
        platform.SetPositionAndRotation(new Vector3(26f, turn.position.y, -5.3f), Quaternion.identity);
        platform.localScale = new Vector3(10f, 1f, 10f);
        platform.GetComponent<MeshRenderer>().sharedMaterial = turn.GetComponent<MeshRenderer>().sharedMaterial;

        Transform trigger = group.Find("Stage1MonsterTrigger");
        if (trigger == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/MonsterSpawnZone.prefab");
            if (prefab == null) throw new InvalidOperationException("Monster spawn zone template is missing.");
            trigger = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, group)).transform;
            trigger.name = "Stage1MonsterTrigger";
        }
        Transform firstTurn = track.Find("Turn_01");
        trigger.SetPositionAndRotation(new Vector3(10.5f, firstTurn.position.y + 4f,
            firstTurn.position.z), Quaternion.Euler(0f, 90f, 0f));
        trigger.localScale = Vector3.one;
        BoxCollider box = trigger.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(8.6f, 8f, 0.8f);

        Transform point = trigger.Find("SpawnPoint_01");
        point.SetPositionAndRotation(new Vector3(28f, turn.position.y + 0.52f, -8f),
            Quaternion.identity);
        SerializedObject settings = new SerializedObject(trigger.GetComponent<MonsterSpawnZone>());
        settings.FindProperty("encounterId").stringValue = "stage1-turn01-hunter";
        settings.FindProperty("requireCenterPastPlane").boolValue = true;
        SerializedProperty entries = settings.FindProperty("entries");
        entries.arraySize = 1;
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("spawnPoint").objectReferenceValue = point;
        entry.FindPropertyRelative("monsterPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BoulderHunter.prefab")
                .GetComponent<BoulderMonster>();
        entry.FindPropertyRelative("count").intValue = 1;
        settings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
        PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        PrefabUtility.RecordPrefabInstancePropertyModifications(point);
        PrefabUtility.RecordPrefabInstancePropertyModifications(trigger.GetComponent<MonsterSpawnZone>());
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        MonsterSetupEditor.RebuildNavigation();
        ConnectRamp(group, "Ramp02UpperConnection", new Vector3(12f, 12.06f, 5.8f),
            new Vector3(12f, 11.5f, 9f));
        ConnectRamp(group, "Ramp02LowerConnection", new Vector3(12f, 5.5f, 25.3f),
            new Vector3(12f, 4.78f, 29f));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Stage1: one monster after the first turn, with a connected spawn platform outside Turn_02.");
    }

    private static void ConnectRamp(Transform group, string name, Vector3 start, Vector3 end)
    {
        if (!NavMesh.SamplePosition(start, out NavMeshHit first, 1.5f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(end, out NavMeshHit second, 1.5f, NavMesh.AllAreas))
            throw new InvalidOperationException($"Cannot connect monster route at {name}.");
        Transform root = group.Find(name);
        if (root == null)
        {
            root = new GameObject(name).transform;
            root.SetParent(group, false);
        }
        NavMeshLink link = root.GetComponent<NavMeshLink>() ?? root.gameObject.AddComponent<NavMeshLink>();
        link.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        link.startPoint = root.InverseTransformPoint(first.position);
        link.endPoint = root.InverseTransformPoint(second.position);
        link.width = 5f;
        link.bidirectional = true;
        link.autoUpdate = true;
        EditorUtility.SetDirty(link);
    }
}
