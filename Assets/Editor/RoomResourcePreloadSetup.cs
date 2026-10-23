using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomResourcePreloadSetup
{
    [MenuItem("Tools/Optimization/Configure Room Resource Preloading")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        MenuManager menu=Object.FindAnyObjectByType<MenuManager>();
        if(menu==null)throw new System.InvalidOperationException("Open the game scene first.");
        RoomResourcePreloader loader=menu.GetComponent<RoomResourcePreloader>();
        if(loader==null)loader=menu.gameObject.AddComponent<RoomResourcePreloader>();
        var prefabs=new List<GameObject>();var audio=new HashSet<AudioClip>();
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab!=null)prefabs.Add(prefab);
            foreach(string dependency in AssetDatabase.GetDependencies(path,true))
            {
                AudioClip clip=AssetDatabase.LoadAssetAtPath<AudioClip>(dependency);
                if(clip!=null)audio.Add(clip);
            }
        }
        GameObject handgun=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QuarterView 3D Action BE5/Prefabs/Weapon HandGun.prefab");
        if(handgun!=null)prefabs.Add(handgun);
        foreach(string guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Resources/Sfx"}))
            audio.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
        const string materialPath="Assets/Materials/RoomPreloadTracer.mat";
        Material tracer=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(tracer==null)
        {
            Shader shader=Shader.Find("Sprites/Default");
            if(shader!=null){tracer=new Material(shader);AssetDatabase.CreateAsset(tracer,materialPath);}
        }
        SerializedObject settings=new(loader);
        SerializedProperty list=settings.FindProperty("gameplayPrefabs");list.arraySize=prefabs.Count;
        for(int i=0;i<prefabs.Count;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=prefabs[i];
        SerializedProperty clips=settings.FindProperty("soundEffects");clips.arraySize=audio.Count;
        int index=0;foreach(AudioClip clip in audio)clips.GetArrayElementAtIndex(index++).objectReferenceValue=clip;
        SerializedProperty materials=settings.FindProperty("extraMaterials");materials.arraySize=tracer!=null?1:0;
        if(tracer!=null)materials.GetArrayElementAtIndex(0).objectReferenceValue=tracer;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Configured room preloading: "+prefabs.Count+" prefabs, "+audio.Count+" short audio clips.");
    }
}
