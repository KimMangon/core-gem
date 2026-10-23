using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage8HidingExpansion
{
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform section=GameObject.Find("FullCourseSections").transform.Find("Section8");
        Transform layout=section.Find("BattleLayout");
        Transform group=layout.Find("HiddenRoutes");
        if(group==null){group=new GameObject("HiddenRoutes").transform;group.SetParent(layout,false);}
        Material material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stage7ObstacleFrame.mat");
        Quaternion slope=Quaternion.Euler(-14,0,0);
        var walls=new System.Collections.Generic.List<BoxCollider>();
        foreach(string name in new[]{"CoverBlock_Left_01","CoverBlock_Right_02","CoverBlock_Left_03"})walls.Add(layout.Find(name).GetComponent<BoxCollider>());
        // Short, staggered walls and isolated pillars hide small targets while
        // leaving the centre and both outer routes traversable by the large boulder.
        float[] distances={40,58,95,117,153,174,211,232,258,272};
        float[] xs={-42,37,39,-38,-43,39,40,-39,39,-42};
        for(int i=0;i<distances.Length;i++)
        {
            string name="HidingWall_"+(i+1);
            Transform existing=group.Find(name);
            GameObject wall=existing!=null?existing.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name=name;wall.transform.SetParent(group,true);
            wall.transform.SetPositionAndRotation(section.position+slope*new Vector3(xs[i],2.5f,distances[i]),slope*Quaternion.Euler(0,i%2==0?25:-25,0));
            wall.transform.localScale=new Vector3(i%3==0?18:14,5,1.5f);
            wall.GetComponent<Renderer>().sharedMaterial=material;walls.Add(wall.GetComponent<BoxCollider>());
        }
        for(int i=0;i<6;i++)
        {
            string name="HidingPillar_"+(i+1);Transform existing=group.Find(name);
            GameObject pillar=existing!=null?existing.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name=name;pillar.transform.SetParent(group,true);
            pillar.transform.SetPositionAndRotation(section.position+slope*new Vector3(i%2==0?-9:9,3,48+i*39),slope*Quaternion.Euler(0,20,0));
            pillar.transform.localScale=new Vector3(3.5f,6,3.5f);pillar.GetComponent<Renderer>().sharedMaterial=material;
            walls.Add(pillar.GetComponent<BoxCollider>());
        }
        Stage8CannonControls controls=section.Find("CannonControlSystem").GetComponent<Stage8CannonControls>();
        SerializedObject settings=new(controls);
        SerializedProperty hiding=settings.FindProperty("hidingWalls");hiding.arraySize=walls.Count;
        for(int i=0;i<walls.Count;i++)hiding.GetArrayElementAtIndex(i).objectReferenceValue=walls[i];
        SerializedProperty ambushes=settings.FindProperty("breakAmbushes");ambushes.arraySize=3;
        for(int i=0;i<3;i++)
        {
            string name="ControlBreakAmbush_"+(i+1);Transform existing=controls.transform.Find(name);
            GameObject zone;
            if(existing!=null)zone=existing.gameObject;
            else zone=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterSpawnZone.prefab"),controls.transform);
            zone.name=name;zone.GetComponent<BoxCollider>().enabled=false;
            MonsterSpawnZone encounter=zone.GetComponent<MonsterSpawnZone>();
            SerializedObject encounterSettings=new(encounter);
            encounterSettings.FindProperty("encounterId").stringValue="stage8-control-break-"+i;
            SerializedProperty entries=encounterSettings.FindProperty("entries");entries.arraySize=3;
            for(int j=0;j<3;j++)
            {
                string pointName="AmbushSpawn_"+j;Transform point=zone.transform.Find(pointName);
                if(point==null){point=new GameObject(pointName).transform;point.SetParent(zone.transform,false);}
                SerializedProperty entry=entries.GetArrayElementAtIndex(j);
                entry.FindPropertyRelative("spawnPoint").objectReferenceValue=point;
                entry.FindPropertyRelative("monsterPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(j==2?"Assets/Prefabs/BoulderCharger.prefab":"Assets/Prefabs/BoulderHunter.prefab").GetComponent<BoulderMonster>();
                entry.FindPropertyRelative("count").intValue=1;
            }
            encounterSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(encounter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone.GetComponent<BoxCollider>());
            ambushes.GetArrayElementAtIndex(i).objectReferenceValue=encounter;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
    }
}
