using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.AI.Navigation;

public static class TutorialCombatSetup
{
    [MenuItem("Tools/Tutorial/Extend Shooting Tutorial")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Scene original = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByName("Tutorial");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Tutorial.unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        TutorialCourse course = null;
        foreach (GameObject obj in scene.GetRootGameObjects())
            if (obj.TryGetComponent(out TutorialCourse found)) course = found;
        if (course == null) throw new InvalidOperationException("TutorialCourse missing.");
        Transform root = course.transform;
        root.Find("Straight Track").position = new Vector3(400, -.5f, 62);
        root.Find("Straight Track").localScale = new Vector3(22, 1, 150);
        foreach (string name in new[] { "Finish Line", "GoalMarker", "Goal Arrow Shaft", "Goal Arrow Left", "Goal Arrow Right" })
        {
            Transform part = root.Find(name);
            Vector3 p = part.position; p.z = 130; part.position = p;
        }
        Transform finish = root.Find("TutorialFinishTrigger");
        Vector3 fp = finish.position; fp.z = 131.5f; finish.position = fp;
        GameObject trap = Cube(root, "Tutorial Red Trap", new Vector3(409, 1.8f, 35), new Vector3(1,4,5), "Assets/Materials/Stage3PusherRed.mat");
        if (!trap.TryGetComponent(out PushingStageObstacle push)) push = trap.AddComponent<PushingStageObstacle>();
        SerializedObject ps = new(push);
        ps.FindProperty("strokeDistance").floatValue = 10;
        ps.FindProperty("cycleSeconds").floatValue = 7;
        ps.ApplyModifiedPropertiesWithoutUndo();
        if (!trap.TryGetComponent(out ShootableObstacle shoot)) shoot = trap.AddComponent<ShootableObstacle>();
        SerializedObject ss = new(shoot);
        ss.FindProperty("obstacleId").stringValue = "tutorial-red-trap";
        ss.FindProperty("maxHealth").intValue = 3;
        ss.FindProperty("respawnSeconds").floatValue = 7;
        ss.ApplyModifiedPropertiesWithoutUndo();
        Ignore(trap);
        GameObject housing = Cube(root, "Tutorial Trap Housing", new Vector3(412, 1.2f, 35), new Vector3(3,3.5f,5), "Assets/Materials/Stage7ObstacleFrame.mat");
        Ignore(housing);
        Transform trigger = root.Find("Tutorial Monster Trigger");
        if (trigger == null)
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterSpawnZone.prefab");
            trigger = ((GameObject)PrefabUtility.InstantiatePrefab(template, root)).transform;
            trigger.name = "Tutorial Monster Trigger";
        }
        trigger.SetPositionAndRotation(new Vector3(400,4,65), Quaternion.identity);
        trigger.localScale = Vector3.one;
        BoxCollider box = trigger.GetComponent<BoxCollider>(); box.size = new Vector3(22.5f,8,1); box.isTrigger = true;
        Transform spawn = trigger.Find("SpawnPoint_01");
        spawn.SetPositionAndRotation(new Vector3(400,.05f,92), Quaternion.Euler(0,180,0));
        MonsterSpawnZone zone = trigger.GetComponent<MonsterSpawnZone>();
        SerializedObject zs = new(zone);
        zs.FindProperty("encounterId").stringValue = "tutorial-monster";
        zs.FindProperty("requireCenterPastPlane").boolValue = true;
        SerializedProperty entries = zs.FindProperty("entries"); entries.arraySize = 1;
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("spawnPoint").objectReferenceValue = spawn;
        entry.FindPropertyRelative("monsterPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BoulderHunter.prefab").GetComponent<BoulderMonster>();
        entry.FindPropertyRelative("count").intValue = 1;
        zs.ApplyModifiedPropertiesWithoutUndo();
        foreach (UnityEngine.Object obj in new UnityEngine.Object[] { trigger, box, spawn, zone }) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        if (!root.TryGetComponent(out NetworkObject net)) net = root.gameObject.AddComponent<NetworkObject>();
        if (!root.TryGetComponent(out TutorialCombatLesson lesson)) lesson = root.gameObject.AddComponent<TutorialCombatLesson>();
        SerializedObject ls = new(lesson);
        ls.FindProperty("redTrap").objectReferenceValue = shoot;
        ls.FindProperty("monsterZone").objectReferenceValue = zone;
        ls.FindProperty("monsterSpawn").objectReferenceValue = spawn;
        ls.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fort/netmarbleB.ttf");
        ls.ApplyModifiedPropertiesWithoutUndo();
        if (!root.TryGetComponent(out NavMeshSurface surface)) surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true; surface.voxelSize = .12f;
        surface.BuildNavMesh();
        string path = "Assets/Navigation/TutorialCombatNavMesh.asset";
        UnityEngine.AI.NavMeshData existing = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
        if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, path);
        else { EditorUtility.CopySerialized(surface.navMeshData, existing); surface.RemoveData(); surface.navMeshData = existing; surface.AddData(); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        SceneManager.SetActiveScene(original);
        if (original != scene) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Tutorial extended to 150m with red trap, monster, and shared shooting objectives.");
    }
    private static void Ignore(GameObject obj)
    {
        if (!obj.TryGetComponent(out NavMeshModifier modifier)) modifier = obj.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }
    private static GameObject Cube(Transform root, string name, Vector3 position, Vector3 scale, string material)
    {
        Transform found = root.Find(name);
        GameObject obj = found != null ? found.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name; obj.transform.SetParent(root, true); obj.transform.position = position; obj.transform.localScale = scale;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(material);
        if (mat != null) obj.GetComponent<Renderer>().sharedMaterial = mat;
        return obj;
    }
}
