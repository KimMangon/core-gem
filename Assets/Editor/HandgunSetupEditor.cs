using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HandgunSetupEditor
{
    [MenuItem("Tools/Player/Install Handgun")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Install the handgun in Edit Mode.");
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/QuarterView 3D Action BE5/Prefabs/Weapon HandGun.prefab");
        AudioClip shot = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Undead Survivor/Audio/Range.wav");
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/QuarterView 3D Action BE5/Sprites/Icon Weapon HandGun.png");
        if (model == null || shot == null || icon == null)
            throw new InvalidOperationException("The handgun model, icon, or Range.wav is missing.");

        const string playerPath = "Assets/Prefabs/Player.prefab";
        GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            PlayerHandgun gun = player.GetComponent<PlayerHandgun>() ??
                player.AddComponent<PlayerHandgun>();
            SerializedObject settings = new SerializedObject(gun);
            settings.FindProperty("playerCamera").objectReferenceValue =
                player.GetComponentInChildren<Camera>(true);
            settings.FindProperty("handgunPrefab").objectReferenceValue = model;
            settings.FindProperty("shotClip").objectReferenceValue = shot;
            settings.FindProperty("handgunIcon").objectReferenceValue = icon;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Stage1")
        {
            Debug.Log("Handgun installed on Player.prefab. Open Stage1 to mark red hazards.");
            return;
        }

        HashSet<GameObject> targets = new();
        foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Material material = renderer.sharedMaterial;
            if (material == null || (material.name != "Stage3PusherRed" &&
                material.name != "Stage7Pusher"))
                continue;
            Transform target = renderer.transform;
            PushingStageObstacle pusher = renderer.GetComponentInParent<PushingStageObstacle>();
            SwingingStageObstacle swing = renderer.GetComponentInParent<SwingingStageObstacle>();
            Stage5CompressionGate gate = renderer.GetComponentInParent<Stage5CompressionGate>();
            if (pusher != null) target = pusher.transform;
            else if (swing != null) target = swing.transform;
            else if (gate != null) target = gate.transform;
            targets.Add(target.gameObject);
        }

        foreach (GameObject target in targets)
        {
            ShootableObstacle hazard = target.GetComponent<ShootableObstacle>() ??
                target.AddComponent<ShootableObstacle>();
            SerializedObject settings = new SerializedObject(hazard);
            settings.FindProperty("obstacleId").stringValue =
                GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            settings.FindProperty("maxHealth").intValue = 3;
            settings.FindProperty("respawnSeconds").floatValue = 7f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Handgun installed; marked {targets.Count} red hazards.");
    }
}
