using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;

public static class Stage8SwingReplacement
{
    [MenuItem("Tools/Course/Replace Stage 8 Pushers With Swings")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform full=GameObject.Find("FullCourseSections").transform;
        Transform section=full.Find("Section8");
        SwingingStageObstacle source=full.Find("Section7").GetComponentInChildren<SwingingStageObstacle>(true);
        Collider plate=section.Find("Stage8Incline_300x120_14deg").GetComponent<Collider>();
        PushingStageObstacle[] pushers=section.GetComponentsInChildren<PushingStageObstacle>(true);
        for(int i=0;i<pushers.Length;i++)
        {
            Transform old=pushers[i].transform;
            if(!plate.Raycast(new Ray(old.position+Vector3.up*60,Vector3.down),out RaycastHit floor,120))
                throw new System.InvalidOperationException("Cannot find the incline under "+old.name);
            GameObject swing=Object.Instantiate(source.gameObject,old.parent);
            swing.name="Stage8Swing_"+(i+1).ToString("00");
            swing.transform.SetPositionAndRotation(floor.point+Vector3.up*11.2f,Quaternion.identity);
            swing.transform.localScale=Vector3.one;
            SerializedObject motion=new(swing.GetComponent<SwingingStageObstacle>());
            motion.FindProperty("phaseOffsetSeconds").floatValue=i*1.4f;
            motion.ApplyModifiedPropertiesWithoutUndo();
            ShootableObstacle shoot=swing.GetComponent<ShootableObstacle>();
            if(shoot==null)shoot=swing.AddComponent<ShootableObstacle>();
            SerializedObject damage=new(shoot);
            damage.FindProperty("obstacleId").stringValue="stage8-swing-"+i;
            damage.ApplyModifiedPropertiesWithoutUndo();
            NavMeshModifier modifier=swing.GetComponent<NavMeshModifier>();
            if(modifier==null)modifier=swing.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild=true;
            Object.DestroyImmediate(old.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
    }
}
