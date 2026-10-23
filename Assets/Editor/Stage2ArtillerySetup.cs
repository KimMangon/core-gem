using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class Stage2ArtillerySetup
{
    [MenuItem("Tools/Monsters/Place Second Stage 2 Artillery")]
    public static void ApplySecond()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Stage1")
            throw new InvalidOperationException("Open Stage1 in Edit Mode.");
        GameObject enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BoulderArtillery.prefab");
        Transform section = GameObject.Find("FullCourseSections").transform.Find("Section2");
        Collider platform = null;
        Collider road = null;
        foreach (Collider collider in section.GetComponentsInChildren<Collider>(true))
        {
            if (collider.name == "SpiralSummit (2)") platform = collider;
            if (collider.name == "SpiralRoad") road = collider;
        }
        if (enemy == null || platform == null || road == null)
            throw new InvalidOperationException("The second designated artillery platform or prefab is missing.");
        if (!road.Raycast(new Ray(new Vector3(8.9f, 45f, 33f), Vector3.down), out RaycastHit ground, 15f))
            throw new InvalidOperationException("Cannot locate the marked middle spiral segment.");
        Transform group = section.Find("Stage2ArtilleryEncounter_02");
        if (group == null)
        {
            group = new GameObject("Stage2ArtilleryEncounter_02").transform;
            group.SetParent(section, false);
        }
        Transform trigger = group.Find("Stage2ArtilleryTrigger_02");
        if (trigger == null)
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterSpawnZone.prefab");
            trigger = ((GameObject)PrefabUtility.InstantiatePrefab(template, group)).transform;
            trigger.name = "Stage2ArtilleryTrigger_02";
        }
        Vector3 radial = ground.point - new Vector3(10f, ground.point.y, 46.671013f);
        Vector3 tangent = new Vector3(-radial.z, 0f, radial.x).normalized;
        trigger.SetPositionAndRotation(ground.point + Vector3.up * 4f, Quaternion.LookRotation(tangent));
        trigger.localScale = Vector3.one;
        BoxCollider box = trigger.GetComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(10.5f, 9f, 1.2f);
        box.isTrigger = true;
        Transform point = trigger.Find("SpawnPoint_01");
        Vector3 spawn = new Vector3(platform.bounds.center.x, platform.bounds.max.y + 0.02f,
            platform.bounds.center.z);
        point.SetPositionAndRotation(spawn, Quaternion.LookRotation(
            Vector3.ProjectOnPlane(ground.point - spawn, Vector3.up)));
        MonsterSpawnZone zone = trigger.GetComponent<MonsterSpawnZone>();
        SerializedObject settings = new SerializedObject(zone);
        settings.FindProperty("encounterId").stringValue = "stage2-middle-spiral-artillery";
        settings.FindProperty("requireCenterPastPlane").boolValue = true;
        SerializedProperty entries = settings.FindProperty("entries");
        entries.arraySize = 1;
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("spawnPoint").objectReferenceValue = point;
        entry.FindPropertyRelative("monsterPrefab").objectReferenceValue = enemy.GetComponent<BoulderMonster>();
        entry.FindPropertyRelative("count").intValue = 1;
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (UnityEngine.Object modified in new UnityEngine.Object[] { trigger, box, point, zone })
            PrefabUtility.RecordPrefabInstancePropertyModifications(modified);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Second stage 2 artillery encounter placed on SpiralSummit (2).");
    }

    [MenuItem("Tools/Monsters/Place Stage 2 Artillery")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Stage1")
            throw new InvalidOperationException("Open Stage1 in Edit Mode.");
        GameObject shell = BuildShell();
        GameObject enemy = BuildEnemy(shell.GetComponent<MonsterShell>());
        UpdateHitboxes();
        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        foreach (GameObject prefab in new[] { shell, enemy })
            if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);

        Transform section = GameObject.Find("FullCourseSections").transform.Find("Section2");
        Collider platform = null;
        Collider road = null;
        foreach (Collider collider in section.GetComponentsInChildren<Collider>(true))
        {
            if (collider.name == "SpiralSummit (1)") platform = collider;
            if (collider.name == "SpiralRoad") road = collider;
        }
        if (platform == null || road == null)
            throw new InvalidOperationException("The designated SpiralSummit (1) platform or SpiralRoad is missing.");
        Transform group = section.Find("Stage2ArtilleryEncounter");
        if (group == null)
        {
            group = new GameObject("Stage2ArtilleryEncounter").transform;
            group.SetParent(section, false);
        }
        Transform trigger = group.Find("Stage2ArtilleryTrigger");
        if (trigger == null)
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterSpawnZone.prefab");
            trigger = ((GameObject)PrefabUtility.InstantiatePrefab(template, group)).transform;
            trigger.name = "Stage2ArtilleryTrigger";
        }
        if (!road.Raycast(new Ray(new Vector3(14.5f, 35f, 60.5f), Vector3.down), out RaycastHit ground, 20f))
            throw new InvalidOperationException("Cannot locate the marked lower spiral segment.");
        Vector3 radial = ground.point - new Vector3(10f, ground.point.y, 46.671013f);
        Vector3 tangent = new Vector3(-radial.z, 0f, radial.x).normalized;
        trigger.SetPositionAndRotation(ground.point + Vector3.up * 4f, Quaternion.LookRotation(tangent));
        trigger.localScale = Vector3.one;
        BoxCollider box = trigger.GetComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(10.5f, 9f, 1.2f);
        box.isTrigger = true;
        Transform point = trigger.Find("SpawnPoint_01");
        Vector3 spawnPosition = new Vector3(platform.bounds.center.x,
            platform.bounds.max.y + 0.02f, platform.bounds.center.z);
        point.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(
            Vector3.ProjectOnPlane(ground.point - spawnPosition, Vector3.up)));
        SerializedObject settings = new SerializedObject(trigger.GetComponent<MonsterSpawnZone>());
        settings.FindProperty("encounterId").stringValue = "stage2-lower-spiral-artillery";
        settings.FindProperty("requireCenterPastPlane").boolValue = true;
        SerializedProperty entries = settings.FindProperty("entries");
        entries.arraySize = 1;
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("spawnPoint").objectReferenceValue = point;
        entry.FindPropertyRelative("monsterPrefab").objectReferenceValue = enemy.GetComponent<BoulderMonster>();
        entry.FindPropertyRelative("count").intValue = 1;
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (UnityEngine.Object modified in new UnityEngine.Object[] { trigger, box, point, trigger.GetComponent<MonsterSpawnZone>() })
            PrefabUtility.RecordPrefabInstancePropertyModifications(modified);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy C artillery configured on the user-designated stage 2 platform.");
    }

    private static GameObject BuildShell()
    {
        const string path = "Assets/Prefabs/MonsterShell.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject root = new GameObject("MonsterShell");
        try
        {
            root.AddComponent<NetworkObject>();
            NetworkTransform sync = root.AddComponent<NetworkTransform>();
            sync.Interpolate = true;
            sync.PositionThreshold = 0.01f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.22f;
            collider.isTrigger = true;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/QuarterView 3D Action BE5/Prefabs/Missile.prefab");
            Transform visual = ((GameObject)PrefabUtility.InstantiatePrefab(source, root.transform)).transform;
            visual.localScale = Vector3.one * 0.25f;
            visual.localRotation = Quaternion.Euler(0f, -90f, 0f);
            root.AddComponent<MonsterShell>();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static GameObject BuildEnemy(MonsterShell shell)
    {
        const string path = "Assets/Prefabs/BoulderArtillery.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        const string modelPath = "Assets/QuarterView 3D Action BE5/Models/Simple Enemy C.fbx";
        const string controllerPath = "Assets/Animations/BoulderArtillery.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                {
                    AnimatorState state = machine.AddState(clip.name);
                    state.motion = clip;
                    if (clip.name == "Idle") machine.defaultState = state;
                    if (clip.name == "Attack") state.speed = clip.length / 1.2f;
                }
        }
        GameObject root = new GameObject("BoulderArtillery");
        try
        {
            root.AddComponent<NetworkObject>();
            NetworkTransform sync = root.AddComponent<NetworkTransform>();
            sync.Interpolate = true;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = 0.6f;
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/QuarterView 3D Action BE5/Prefabs/Enemy C.prefab");
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.name = "EnemyCVisual";
            GameObject animatedRoot = visual.transform.GetChild(0).gameObject;
            Animator animator = animatedRoot.GetComponent<Animator>();
            if (animator == null) animator = animatedRoot.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is Avatar avatar) animator.avatar = avatar;
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.3f, 0.9f);
            BoulderMonster monster = root.AddComponent<BoulderMonster>();
            SerializedObject settings = new SerializedObject(monster);
            settings.FindProperty("animator").objectReferenceValue = animator;
            settings.FindProperty("stationaryRanged").boolValue = true;
            settings.FindProperty("shellPrefab").objectReferenceValue = shell;
            settings.FindProperty("muzzle").objectReferenceValue = muzzle;
            settings.FindProperty("attackInterval").floatValue = 3f;
            settings.FindProperty("attackWindup").floatValue = 0.6f;
            settings.FindProperty("maximumHealth").intValue = 7;
            settings.FindProperty("healthPerAdditionalPlayer").intValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            ConfigureHitboxes(root);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/Monsters/Update Artillery Hitboxes")]
    public static void UpdateHitboxes()
    {
        const string path = "Assets/Prefabs/BoulderArtillery.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ConfigureHitboxes(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureHitboxes(GameObject root)
    {
        CapsuleCollider oldHitbox = root.GetComponent<CapsuleCollider>();
        if (oldHitbox != null) UnityEngine.Object.DestroyImmediate(oldHitbox);
        foreach (MeshFilter part in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (part.sharedMesh == null || part.GetComponent<MeshRenderer>() == null) continue;
            MeshCollider hitbox = part.GetComponent<MeshCollider>();
            if (hitbox == null) hitbox = part.gameObject.AddComponent<MeshCollider>();
            hitbox.sharedMesh = part.sharedMesh;
            hitbox.convex = true;
            hitbox.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(hitbox);
        }
    }
}
