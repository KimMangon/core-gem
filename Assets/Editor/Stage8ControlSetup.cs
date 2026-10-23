using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;

public static class Stage8ControlSetup
{
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform section=GameObject.Find("FullCourseSections").transform.Find("Section8");
        if(section.Find("CannonControlSystem")!=null)throw new System.InvalidOperationException("Control system already exists.");
        GameObject root=new GameObject("CannonControlSystem");root.transform.SetParent(section,false);
        root.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild=true;
        root.AddComponent<NetworkObject>();
        Stage8CannonControls controls=root.AddComponent<Stage8CannonControls>();
        Material red=Material("Assets/Materials/Stage8WeakPointRed.mat",new Color(1,.04f,.015f),true);
        Material dark=Material("Assets/Materials/Stage8ControlHousing.mat",new Color(.045f,.055f,.07f),false);
        Font font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fort/netmarbleB.ttf");
        Stage5BallLauncher[] launchers=section.Find("HeavyCannons").GetComponentsInChildren<Stage5BallLauncher>(true);
        System.Array.Sort(launchers,(a,b)=>string.CompareOrdinal(a.name,b.name));
        SerializedObject settings=new(controls);
        SerializedProperty cannons=settings.FindProperty("cannons");cannons.arraySize=3;
        SerializedProperty points=settings.FindProperty("weakpoints");points.arraySize=3;
        Collider plate=null;foreach(MeshCollider c in section.GetComponentsInChildren<MeshCollider>())
            if(c.name.StartsWith("Stage8Incline_"))plate=c;
        settings.FindProperty("inclinePlate").objectReferenceValue=plate;
        Quaternion slope=Quaternion.Euler(-14,0,0);
        for(int i=0;i<3;i++)
        {
            cannons.GetArrayElementAtIndex(i).objectReferenceValue=launchers[i];
            GameObject target=new GameObject("CannonControl_"+(i+1));target.transform.SetParent(root.transform,false);
            target.transform.SetPositionAndRotation(section.position+slope*new Vector3((i-1)*32,.7f,50+i*75),slope);
            CannonWeakPoint weak=target.AddComponent<CannonWeakPoint>();SerializedObject ws=new(weak);
            ws.FindProperty("cannonIndex").intValue=i;ws.ApplyModifiedPropertiesWithoutUndo();
            Cube(target.transform,"ControlHousing",Vector3.zero,new Vector3(2.2f,1.4f,1),dark);
            GameObject button=GameObject.CreatePrimitive(PrimitiveType.Cylinder);button.name="RedShootableButton";
            button.transform.SetParent(target.transform,false);button.transform.localPosition=new Vector3(0,.1f,-.6f);
            button.transform.localRotation=Quaternion.Euler(90,0,0);button.transform.localScale=new Vector3(1.15f,.14f,1.15f);
            button.GetComponent<Renderer>().sharedMaterial=red;
            Text(target.transform,"ControlNumber",(i+1).ToString(),new Vector3(0,1.55f,-.4f),new Vector2(200,90),font,62,.012f);
            points.GetArrayElementAtIndex(i).objectReferenceValue=target.transform;
            Text(launchers[i].transform,"ControlNumber",(i+1).ToString(),new Vector3(0,4,-1),new Vector2(200,100),font,65,.018f);
            SerializedObject cannon=new(launchers[i]);cannon.FindProperty("launchSpeed").floatValue=34;
            cannon.ApplyModifiedPropertiesWithoutUndo();
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        GameObject sign=new GameObject("Stage8CannonInstructions");sign.transform.SetParent(section,false);
        sign.transform.position=section.position+slope*new Vector3(18,0,7);
        Cube(sign.transform,"SignPole",new Vector3(0,2,0),new Vector3(.4f,4,.4f),dark);
        Cube(sign.transform,"SignBoard",new Vector3(0,5.3f,0),new Vector3(18,7,.5f),dark);
        Stage8PictogramSetup.Draw(sign.transform);
        Stage8HiddenControlsSetup.Configure(section);
        UpdateBall();
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
    }
    private static void UpdateBall()
    {
        const string path="Assets/Prefabs/Stage8HeavyCannonball.prefab";
        GameObject root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            MeshRenderer old=root.GetComponent<MeshRenderer>();if(old!=null)old.enabled=false;
            GameObject boss=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QuarterView 3D Action BE5/Prefabs/Boss Rock.prefab"),root.transform);
            boss.name="BossRockVisual";
            Mesh mesh=boss.GetComponentInChildren<MeshFilter>().sharedMesh;
            boss.transform.localScale=Vector3.one/Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y,mesh.bounds.size.z);
            SerializedObject settings=new(root.GetComponent<Stage5Projectile>());
            settings.FindProperty("boulderDamage").intValue=1;
            settings.FindProperty("boulderImpactImpulse").floatValue=16;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static Material Material(string path,Color color,bool glow)
    {
        Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null)
        {
            Material template=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stage3PusherRed.mat");
            mat=new Material(template);AssetDatabase.CreateAsset(mat,path);
        }
        mat.color=color;
        if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);
        if(glow&&mat.HasProperty("_EmissionColor")){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*2);}
        EditorUtility.SetDirty(mat);return mat;
    }
    private static void Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
        obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;
    }
    private static void Text(Transform parent,string name,string text,Vector3 position,Vector2 size,Font font,int fontSize,float scale)
    {
        GameObject obj=new GameObject(name,typeof(RectTransform),typeof(Canvas));obj.transform.SetParent(parent,false);
        obj.transform.localPosition=position;obj.transform.localScale=Vector3.one*scale;
        Canvas canvas=obj.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        obj.GetComponent<RectTransform>().sizeDelta=size;
        GameObject label=new GameObject("Label",typeof(RectTransform),typeof(UnityEngine.UI.Text));label.transform.SetParent(obj.transform,false);
        RectTransform rect=label.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        UnityEngine.UI.Text component=label.GetComponent<UnityEngine.UI.Text>();component.font=font;component.text=text;
        component.fontSize=fontSize;component.alignment=TextAnchor.MiddleCenter;component.color=Color.white;component.raycastTarget=false;
        UnityEngine.UI.Shadow shadow=label.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(2,-2);
    }
}
