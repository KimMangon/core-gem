using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MenuBoulderVignetteSetup
{
    [MenuItem("Tools/UI/Set Up Boulder Pushing Menu")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
        var menu = Object.FindAnyObjectByType<MenuManager>();
        if (menu == null) throw new System.InvalidOperationException("Open main course scene first.");
        var scene = menu.gameObject.scene;
        Camera camera = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Menu Boulder Vignette") return;
            if (root.name == "Main Camera") camera = root.GetComponent<Camera>();
        }
        if (camera == null) throw new System.InvalidOperationException("Menu camera missing.");
        var rootObject = new GameObject("Menu Boulder Vignette");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rootObject, scene);
        rootObject.transform.position = new Vector3(-650f, 150f, -350f);
        var visuals = new GameObject("MenuOnlyVisuals");
        visuals.transform.SetParent(rootObject.transform, false);
        var slope = new GameObject("Incline");
        slope.transform.SetParent(visuals.transform, false);
        slope.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        Material road = Material("MenuRamp", new Color(0.39f, 0.4f, 0.38f));
        Material blue = Material("MenuAvatar", PlayerColorPalette.Colors[0]);
        Material white = Material("MenuEyesWhite", Color.white);
        Material black = Material("MenuEyesBlack", new Color(0.025f,0.025f,0.025f));
        Primitive("Ramp", PrimitiveType.Cube, slope.transform, new Vector3(0f,-0.45f,0f), new Vector3(30f,0.9f,14f), road);
        var actor = new GameObject("PushingCharacter").transform;
        actor.SetParent(slope.transform, false);
        actor.localPosition = new Vector3(3.7f,0f,0f);
        var torso = new GameObject("LeaningBody").transform;
        torso.SetParent(actor, false);
        var colored = new List<MeshRenderer>();
        colored.Add(Primitive("Body", PrimitiveType.Capsule, torso, new Vector3(0f,1.25f,0f), new Vector3(0.85f,0.75f,0.85f), blue).GetComponent<MeshRenderer>());
        foreach (int side in new[]{-1,1})
        {
            Primitive("Eye",PrimitiveType.Sphere,torso,new Vector3(0.37f,1.7f,side*0.23f),Vector3.one*0.26f,white);
            Primitive("Pupil",PrimitiveType.Sphere,torso,new Vector3(0.49f,1.7f,side*0.23f),Vector3.one*0.12f,black);
            var arm = Primitive("Arm",PrimitiveType.Capsule,actor,new Vector3(0.57f,1.25f,side*0.36f),new Vector3(0.18f,0.48f,0.18f),blue);
            arm.transform.localRotation=Quaternion.Euler(0f,0f,-78f);
            colored.Add(arm.GetComponent<MeshRenderer>());
            colored.Add(Primitive("Hand",PrimitiveType.Sphere,actor,new Vector3(1.02f,1.35f,side*0.36f),Vector3.one*0.25f,blue).GetComponent<MeshRenderer>());
        }
        var left=Primitive("LeftFoot",PrimitiveType.Capsule,actor,new Vector3(0f,0.3f,-0.25f),new Vector3(0.27f,0.3f,0.27f),blue);
        var right=Primitive("RightFoot",PrimitiveType.Capsule,actor,new Vector3(0f,0.3f,0.25f),new Vector3(0.27f,0.3f,0.27f),blue);
        colored.Add(left.GetComponent<MeshRenderer>()); colored.Add(right.GetComponent<MeshRenderer>());
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boulder.prefab");
        var sourceFilter=source.GetComponent<MeshFilter>();
        var stone=new GameObject("DecorativeBoulder",typeof(MeshFilter),typeof(MeshRenderer));
        stone.transform.SetParent(slope.transform,false);
        stone.GetComponent<MeshFilter>().sharedMesh=sourceFilter.sharedMesh;
        stone.GetComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
        float diameter=Mathf.Max(sourceFilter.sharedMesh.bounds.size.x,sourceFilter.sharedMesh.bounds.size.y,sourceFilter.sharedMesh.bounds.size.z);
        stone.transform.localScale=Vector3.one*(3.5f/diameter);
        stone.transform.localPosition=new Vector3(6.4f,1.75f,0f);
        var controller=rootObject.AddComponent<MenuBoulderVignette>();
        var serialized=new SerializedObject(controller);
        serialized.FindProperty("visuals").objectReferenceValue=visuals;
        serialized.FindProperty("menuCamera").objectReferenceValue=camera;
        serialized.FindProperty("actor").objectReferenceValue=actor;
        serialized.FindProperty("stone").objectReferenceValue=stone.transform;
        serialized.FindProperty("torso").objectReferenceValue=torso;
        serialized.FindProperty("leftFoot").objectReferenceValue=left.transform;
        serialized.FindProperty("rightFoot").objectReferenceValue=right.transform;
        var parts=serialized.FindProperty("avatarParts"); parts.arraySize=colored.Count;
        for(int i=0;i<colored.Count;i++)parts.GetArrayElementAtIndex(i).objectReferenceValue=colored[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        camera.transform.position=rootObject.transform.TransformPoint(new Vector3(0f,9f,-23f));
        camera.transform.LookAt(rootObject.transform.TransformPoint(new Vector3(0f,2.5f,0f)));
        camera.fieldOfView=45f;
        camera.farClipPlane=2500f;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Menu-only boulder pushing vignette saved. No NetworkObjects, rigidbodies or colliders.");
    }

    private static Material Material(string name, Color color)
    {
        string path="Assets/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",0f);
        AssetDatabase.CreateAsset(material,path); return material;
    }

    private static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var obj=GameObject.CreatePrimitive(type); obj.name=name; obj.transform.SetParent(parent,false);
        obj.transform.localPosition=position; obj.transform.localScale=scale;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        return obj;
    }
}
