using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage8HiddenControlsSetup
{
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform section=GameObject.Find("FullCourseSections").transform.Find("Section8");
        Configure(section);
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
    }
    public static void Configure(Transform section)
    {
        Transform system=section.Find("CannonControlSystem");
        Transform layout=section.Find("BattleLayout");
        string[] names={"CoverBlock_Left_01","CoverBlock_Right_02","CoverBlock_Left_03"};
        BoxCollider[] walls=new BoxCollider[3];
        for(int i=0;i<3;i++)walls[i]=layout.Find(names[i]).GetComponent<BoxCollider>();
        SerializedObject settings=new(system.GetComponent<Stage8CannonControls>());
        SerializedProperty array=settings.FindProperty("hidingWalls");array.arraySize=3;
        for(int i=0;i<3;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=walls[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
        for(int i=0;i<3;i++)
        {
            Transform target=system.Find("CannonControl_"+(i+1));
            foreach(string name in new[]{"ControlHousing","ControlNumber"})
            {
                Transform old=target.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);
            }
            Transform button=target.Find("RedShootableButton");button.localPosition=Vector3.zero;
            foreach(Collider old in button.GetComponents<Collider>())Object.DestroyImmediate(old);
            MeshCollider collider=button.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=button.GetComponent<MeshFilter>().sharedMesh;collider.convex=true;
            if(i==0)
            {
                Vector3 point=section.position+Quaternion.Euler(-14,0,0)*new Vector3(-28,0,15)+Vector3.up*20;
                target.SetPositionAndRotation(point,Quaternion.LookRotation(point-(section.position+Vector3.up*2)));
            }
            else
            {
                BoxCollider wall=walls[i==1?0:2];
                Vector3 point=wall.transform.TransformPoint(wall.center+new Vector3(.15f,.1f,.5f)) +wall.transform.forward*.22f;
                target.SetPositionAndRotation(point,Quaternion.LookRotation(-wall.transform.forward,wall.transform.up));
            }
        }
        foreach(Stage5BallLauncher cannon in section.Find("HeavyCannons").GetComponentsInChildren<Stage5BallLauncher>())
        {
            Transform label=cannon.transform.Find("ControlNumber");if(label!=null)Object.DestroyImmediate(label.gameObject);
        }
    }
}
