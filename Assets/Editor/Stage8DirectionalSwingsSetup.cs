using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;

public static class Stage8DirectionalSwingsSetup
{
    [MenuItem("Tools/Course/Add Marked Stage 8 Directional Swings")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform full=GameObject.Find("FullCourseSections").transform;
        Transform section=full.Find("Section8");
        Transform layout=section.Find("BattleLayout");
        if(layout.Find("DirectionalSwings")!=null)throw new System.InvalidOperationException("Directional swings already exist; edit the placed objects.");
        Transform group=new GameObject("DirectionalSwings").transform;group.SetParent(layout,false);
        SwingingStageObstacle source=full.Find("Section7").GetComponentInChildren<SwingingStageObstacle>(true);
        Collider plate=section.Find("Stage8Incline_300x120_14deg").GetComponent<Collider>();
        Quaternion slope=Quaternion.Euler(-14,0,0);
        // Approximate the seven red strokes on the supplied top-down sketch.
        Vector2[] points={new(-33,19),new(44,35),new(-50,93),new(-48,224),new(43,258),new(-20,279),new(18,281)};
        Vector3[] directions={new(1,0,1),new(1,0,-1),Vector3.right,Vector3.forward,new(1,0,-1),Vector3.right,Vector3.forward};
        string[] labels={"LowerLeft_Diagonal","LowerRight_Diagonal","MiddleLeft_Across","UpperLeft_Along","UpperRight_Diagonal","TopLeft_Across","TopRight_Along"};
        for(int i=0;i<points.Length;i++)
        {
            Vector3 position=section.position+slope*new Vector3(points[i].x,0,points[i].y);
            if(!plate.Raycast(new Ray(position+Vector3.up*60,Vector3.down),out RaycastHit floor,120))
                throw new System.InvalidOperationException("Marked swing position is outside the plate.");
            GameObject swing=Object.Instantiate(source.gameObject,group);
            swing.name="Stage8Swing_"+labels[i];
            // Local X is the pendulum's travel direction; rotate only around
            // world up so the rope still hangs vertically under gravity.
            Vector3 direction=directions[i].normalized;
            float yaw=Mathf.Atan2(-direction.z,direction.x)*Mathf.Rad2Deg;
            swing.transform.SetPositionAndRotation(floor.point+Vector3.up*11.2f,Quaternion.Euler(0,yaw,0));
            swing.transform.localScale=Vector3.one;
            SerializedObject movement=new(swing.GetComponent<SwingingStageObstacle>());
            movement.FindProperty("phaseOffsetSeconds").floatValue=i*.63f;
            movement.ApplyModifiedPropertiesWithoutUndo();
            ShootableObstacle target=swing.GetComponent<ShootableObstacle>();
            if(target==null)target=swing.AddComponent<ShootableObstacle>();
            SerializedObject damage=new(target);damage.FindProperty("obstacleId").stringValue="stage8-directional-swing-"+i;
            damage.ApplyModifiedPropertiesWithoutUndo();
            NavMeshModifier modifier=swing.GetComponent<NavMeshModifier>();
            if(modifier==null)modifier=swing.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
        }
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
        Selection.activeGameObject=group.gameObject;
    }
}
