using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

public static class Stage3HuntersSetup
{
    public static void Apply(bool preserveTrigger = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit play mode first.");
        Transform section = GameObject.Find("FullCourseSections").transform.Find("Section3");
        Transform track = section.Find("Stage3Track");
        Collider road = track.Find("Stage3Road").GetComponent<Collider>();
        Transform trigger = section.Find("Stage3HunterTrigger");
        if (trigger == null)
        {
            trigger = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterSpawnZone.prefab"), section)).transform;
            trigger.name = "Stage3HunterTrigger";
        }
        if (!preserveTrigger)
            trigger.SetPositionAndRotation(new Vector3(24, 67, 119), Quaternion.identity);
        BoxCollider box = trigger.GetComponent<BoxCollider>();
        box.size = new Vector3(25, 9, 1.2f); box.center = Vector3.zero; box.isTrigger = true;
        SerializedObject settings = new(trigger.GetComponent<MonsterSpawnZone>());
        settings.FindProperty("encounterId").stringValue = "stage3-three-hunters";
        settings.FindProperty("requireCenterPastPlane").boolValue = true;
        SerializedProperty entries = settings.FindProperty("entries"); entries.arraySize = 3;
        string[] names = { "Stage3Pillar_3 (1)", "Stage3Pillar_3 (2)", "Stage3Pillar_3 (3)" };
        MonsterSetupEditor.RebuildNavigation();
        for (int i = 0; i < 3; i++)
        {
            Collider platform = track.Find(names[i]).GetComponent<Collider>();
            Vector3 position = platform.bounds.center; position.y = platform.bounds.max.y + .02f;
            Transform spawn = trigger.Find("HunterSpawn_" + i);
            if (spawn == null) { spawn = new GameObject("HunterSpawn_" + i).transform; spawn.SetParent(trigger); }
            spawn.position = position;
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("spawnPoint").objectReferenceValue = spawn;
            entry.FindPropertyRelative("monsterPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BoulderHunter.prefab").GetComponent<BoulderMonster>();
            entry.FindPropertyRelative("count").intValue = 1;
            Vector3 nearest = position; float distance = float.PositiveInfinity;
            for (float x = position.x - 15; x <= position.x + 15; x += .5f)
                for (float z = position.z - 15; z <= position.z + 15; z += .5f)
                    if (road.Raycast(new Ray(new Vector3(x, 80, z), Vector3.down), out RaycastHit hit, 30) &&
                        NavMesh.SamplePosition(hit.point, out NavMeshHit nav, .35f, NavMesh.AllAreas))
                    {
                        float d = (nav.position - position).sqrMagnitude;
                        if (d < distance) { distance = d; nearest = nav.position; }
                    }
            if (float.IsPositiveInfinity(distance)) throw new System.InvalidOperationException("No road navigation near platform.");
            NavMeshLink link = spawn.GetComponent<NavMeshLink>() ?? spawn.gameObject.AddComponent<NavMeshLink>();
            link.startPoint = Vector3.zero;
            link.endPoint = spawn.InverseTransformPoint(nearest);
            link.width = 1.5f; link.bidirectional = true;
            link.UpdateLink();
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.SamplePosition(position, out NavMeshHit start, 1.5f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(start.position, nearest, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                throw new System.InvalidOperationException("Hunter platform " + i + " is disconnected from the road.");
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (Object obj in new Object[] { trigger, box, trigger.GetComponent<MonsterSpawnZone>() }) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
    }
}
