using System;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class MonsterSetupEditor
{
    private const string MonsterPath = "Assets/Prefabs/BoulderHunter.prefab";
    private const string ControllerPath = "Assets/Animations/BoulderHunter.controller";
    private const string SourceModelPath = "Assets/QuarterView 3D Action BE5/Prefabs/Enemy A.prefab";
    private const string SourceAnimationPath = "Assets/QuarterView 3D Action BE5/Models/Simple Enemy A AnimTest.fbx";

    [MenuItem("Tools/Monsters/Fit Hunter Hitboxes")]
    public static void FitHunterHitboxes()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MonsterPath);
        try
        {
            foreach (CapsuleCollider old in root.GetComponents<CapsuleCollider>())
                UnityEngine.Object.DestroyImmediate(old);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.localBounds;
                BoxCollider hitbox = renderer.GetComponent<BoxCollider>();
                if (hitbox == null) hitbox = renderer.gameObject.AddComponent<BoxCollider>();
                hitbox.center = bounds.center;
                hitbox.size = bounds.size;
                hitbox.isTrigger = false;
                hitbox.enabled = true;
            }
            PrefabUtility.SaveAsPrefabAsset(root, MonsterPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Monsters/Install Boulder Hunters")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().name != "Stage1")
            throw new InvalidOperationException("Open Stage1 in Edit Mode before installing monsters.");
        EnsureFolder("Assets/Animations");
        EnsureFolder("Assets/Navigation");
        AnimatorController controller = BuildController();
        GameObject monster = BuildMonster(controller);
        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
            "Assets/DefaultNetworkPrefabs.asset");
        if (list == null) throw new InvalidOperationException("Network prefab list is missing.");
        if (!list.Contains(monster)) list.Add(new NetworkPrefab { Prefab = monster });
        EditorUtility.SetDirty(list);
        BuildZoneTemplate(monster.GetComponent<BoulderMonster>());
        InstallBoulderHealth();
        SetupNavigation();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Boulder hunters installed. No encounter zones have been placed in the course.");
    }

    private static AnimatorController BuildController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SourceAnimationPath))
        {
            if (!(asset is AnimationClip clip) || clip.name.StartsWith("__")) continue;
            if (clip.name != "Idle" && clip.name != "Walk" && clip.name != "Attack" && clip.name != "Die")
                continue;
            AnimatorState state = machine.AddState(clip.name);
            state.motion = clip;
            if (clip.name == "Attack") state.speed = clip.length / 1.2f;
            if (clip.name == "Idle") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static GameObject BuildMonster(AnimatorController controller)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPath);
        if (existing != null) return existing;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        if (source == null) throw new InvalidOperationException("Enemy A model is missing.");
        GameObject root = new GameObject("BoulderHunter");
        try
        {
            root.AddComponent<NetworkObject>();
            NetworkTransform netTransform = root.AddComponent<NetworkTransform>();
            netTransform.Interpolate = true;
            netTransform.PositionThreshold = 0.01f;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.4f;
            collider.height = 1.6f;
            collider.center = Vector3.up * 0.8f;
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
            agent.radius = 0.4f;
            agent.height = 1.6f;
            agent.speed = 2.4f;
            agent.acceleration = 12f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.15f;
            agent.enabled = false;
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.name = "EnemyAVisual";
            Animator animator = visual.transform.GetChild(0).gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SourceAnimationPath))
                if (asset is Avatar avatar) animator.avatar = avatar;
            BoulderMonster behaviour = root.AddComponent<BoulderMonster>();
            SerializedObject settings = new SerializedObject(behaviour);
            settings.FindProperty("animator").objectReferenceValue = animator;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, MonsterPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildZoneTemplate(BoulderMonster monster)
    {
        const string path = "Assets/Prefabs/MonsterSpawnZone.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        GameObject root = new GameObject("MonsterSpawnZone");
        try
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8f, 6f, 3f);
            Transform point = new GameObject("SpawnPoint_01").transform;
            point.SetParent(root.transform, false);
            MonsterSpawnZone zone = root.AddComponent<MonsterSpawnZone>();
            SerializedObject settings = new SerializedObject(zone);
            SerializedProperty entries = settings.FindProperty("entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("spawnPoint").objectReferenceValue = point;
            entry.FindPropertyRelative("monsterPrefab").objectReferenceValue = monster;
            entry.FindPropertyRelative("count").intValue = 1;
            entry.FindPropertyRelative("intervalSeconds").floatValue = 0.5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void InstallBoulderHealth()
    {
        const string path = "Assets/Prefabs/Boulder.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<BoulderHealth>() == null) root.AddComponent<BoulderHealth>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach (BoulderSlopeDrag boulder in UnityEngine.Object.FindObjectsByType<BoulderSlopeDrag>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (boulder.GetComponent<BoulderHealth>() == null)
                boulder.gameObject.AddComponent<BoulderHealth>();
    }

    private static void SetupNavigation()
    {
        StageManager stage = UnityEngine.Object.FindFirstObjectByType<StageManager>();
        SerializedObject settings = new SerializedObject(stage);
        foreach (string field in new[] { "stageOneTrack", "stageOneGround", "fullCourseSections" })
        {
            GameObject root = settings.FindProperty(field).objectReferenceValue as GameObject;
            if (root == null) continue;
            NavMeshModifier modifier = root.GetComponent<NavMeshModifier>() ?? root.AddComponent<NavMeshModifier>();
            modifier.applyToChildren = true;
            modifier.overrideArea = true;
            modifier.area = 0;
            modifier.ignoreFromBuild = false;
            EditorUtility.SetDirty(modifier);
        }
        foreach (MonoBehaviour script in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(script is PushingStageObstacle) && !(script is SwingingStageObstacle) &&
                !(script is RotatingStageObstacle) && !(script is Stage5CompressionGate) &&
                !(script is Stage5BallLauncher)) continue;
            NavMeshModifier modifier = script.GetComponent<NavMeshModifier>() ??
                script.gameObject.AddComponent<NavMeshModifier>();
            modifier.applyToChildren = true;
            modifier.ignoreFromBuild = true;
            EditorUtility.SetDirty(modifier);
        }
        GameObject navigation = GameObject.Find("MonsterNavigation") ?? new GameObject("MonsterNavigation");
        NavMeshSurface surface = navigation.GetComponent<NavMeshSurface>() ??
            navigation.AddComponent<NavMeshSurface>();
        surface.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        surface.collectObjects = CollectObjects.MarkedWithModifier;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.12f;
        if (surface.navMeshData == null) BakeNavigation(surface);
        EditorUtility.SetDirty(surface);
    }

    [MenuItem("Tools/Monsters/Rebuild Course Navigation")]
    public static void RebuildNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Rebuild navigation in Edit Mode.");
        NavMeshSurface surface = GameObject.Find("MonsterNavigation")?.GetComponent<NavMeshSurface>();
        if (surface == null) throw new InvalidOperationException("Install Boulder Hunters first.");
        BakeNavigation(surface);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    private static void BakeNavigation(NavMeshSurface surface)
    {
        surface.BuildNavMesh();
        if (surface.navMeshData == null)
            throw new InvalidOperationException("Could not build monster course navigation.");
        surface.navMeshData.name = "MonsterCourseNavMesh";
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Navigation/MonsterCourseNavMesh.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, path);
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
