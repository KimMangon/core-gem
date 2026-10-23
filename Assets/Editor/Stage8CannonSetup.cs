using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using Unity.AI.Navigation;

public static class Stage8CannonSetup
{
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform section=GameObject.Find("FullCourseSections").transform.Find("Section8");
        if(section.Find("HeavyCannons")!=null)throw new System.InvalidOperationException("Heavy cannons already exist.");
        Stage5BallLauncher source=null;
        foreach(Stage5BallLauncher launcher in Object.FindObjectsByType<Stage5BallLauncher>(FindObjectsSortMode.None))
            if(launcher.ActiveSection==5){source=launcher;break;}
        if(source==null)throw new System.InvalidOperationException("Stage 5 cannon missing.");
        SerializedObject original=new(source);
        string sourcePath=AssetDatabase.GetAssetPath(original.FindProperty("projectilePrefab").objectReferenceValue);
        const string path="Assets/Prefabs/Stage8HeavyCannonball.prefab";
        GameObject root=PrefabUtility.LoadPrefabContents(sourcePath);
        GameObject projectile;
        try
        {
            root.name="Stage8HeavyCannonball";
            root.transform.localScale*=1.5f;
            root.GetComponent<Rigidbody>().collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            SerializedObject settings=new(root.GetComponent<Stage5Projectile>());
            settings.FindProperty("activeSection").intValue=8;
            settings.FindProperty("maxLifetime").floatValue=18;
            settings.ApplyModifiedPropertiesWithoutUndo();
            projectile=PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        NetworkPrefabsList list=AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if(!list.Contains(projectile))list.Add(new NetworkPrefab{Prefab=projectile});
        EditorUtility.SetDirty(list);
        Transform group=new GameObject("HeavyCannons").transform;group.SetParent(section,false);
        Quaternion rotation=Quaternion.Euler(-14,0,0);
        for(int i=0;i<3;i++)
        {
            GameObject cannon=Object.Instantiate(source.gameObject,group);
            cannon.name="Stage8HeavyCannon_"+(i+1);
            cannon.transform.SetPositionAndRotation(section.position+rotation*new Vector3((i-1)*38,0,290),rotation);
            cannon.transform.localScale=Vector3.one*1.3f;
            Stage5BallLauncher launcher=cannon.GetComponent<Stage5BallLauncher>();
            SerializedObject settings=new(launcher);
            settings.FindProperty("projectilePrefab").objectReferenceValue=projectile;
            settings.FindProperty("activeSection").intValue=8;
            settings.FindProperty("initialDelay").floatValue=2f+i*.6f;
            settings.FindProperty("shotInterval").floatValue=1.6f;
            settings.FindProperty("randomShotInterval").vector2Value=new Vector2(1.6f,2.3f);
            settings.FindProperty("launchSpeed").floatValue=30;
            settings.FindProperty("downhillDirection").vector3Value=Vector3.back;
            settings.FindProperty("yawSpreadDegrees").floatValue=6;
            settings.FindProperty("useCustomYawRange").boolValue=true;
            settings.FindProperty("customYawRange").vector2Value=i==0?new Vector2(-40,0):i==1?new Vector2(-25,25):new Vector2(0,40);
            settings.FindProperty("upwardPitchRange").vector2Value=new Vector2(2,5);
            settings.ApplyModifiedPropertiesWithoutUndo();
            NavMeshModifier modifier=cannon.GetComponent<NavMeshModifier>();
            if(modifier==null)modifier=cannon.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild=true;
        }
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
    }
}
