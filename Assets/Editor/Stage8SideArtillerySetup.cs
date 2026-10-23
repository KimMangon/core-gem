using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage8SideArtillerySetup
{
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform section=GameObject.Find("FullCourseSections").transform.Find("Section8");
        Transform layout=section.Find("BattleLayout");
        Material road=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrackAsphalt.mat");
        Quaternion slope=Quaternion.Euler(-14,0,0);
        Vector3[] tops=new Vector3[4];
        string[] names={"SideArtilleryPlatform_EarlyLeft","SideArtilleryPlatform_MiddleRight","ArtilleryPlatform_Left","ArtilleryPlatform_Right"};
        float[] xs={-70,70,-70,70};float[] distances={90,150,225,230};
        for(int i=0;i<4;i++)
        {
            Transform platform=layout.Find(names[i]);
            if(platform==null){platform=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;platform.name=names[i];platform.SetParent(layout,true);}
            Vector3 surface=section.position+slope*new Vector3(xs[i],0,distances[i]);
            platform.SetPositionAndRotation(surface+Vector3.up*4.6f,Quaternion.identity);
            platform.localScale=new Vector3(12,1.2f,12);
            platform.GetComponent<Renderer>().sharedMaterial=road;
            tops[i]=surface+Vector3.up*5.22f;
        }
        MonsterSpawnZone late=layout.Find("Wave03_ArtilleryAmbush").GetComponent<MonsterSpawnZone>();
        SerializedObject lateSettings=new(late);SerializedProperty lateEntries=lateSettings.FindProperty("entries");
        for(int i=0;i<2;i++)
        {
            Transform point=(Transform)lateEntries.GetArrayElementAtIndex(i).FindPropertyRelative("spawnPoint").objectReferenceValue;
            point.position=tops[i+2];point.rotation=Quaternion.LookRotation(new Vector3(section.position.x-point.position.x,0,-10));
        }
        BoulderMonster enemy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BoulderArtillery.prefab").GetComponent<BoulderMonster>();
        for(int i=0;i<2;i++)
        {
            string name=i==0?"SideArtilleryEncounter_Early":"SideArtilleryEncounter_Middle";
            Transform zone=layout.Find(name);
            if(zone==null){zone=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterSpawnZone.prefab"),layout)).transform;zone.name=name;}
            zone.SetPositionAndRotation(section.position+slope*(Vector3.forward*(i==0?65:125))+Vector3.up*4,Quaternion.identity);
            BoxCollider box=zone.GetComponent<BoxCollider>();box.size=new Vector3(124,14,2);box.isTrigger=true;
            Transform point=zone.Find("SpawnPoint_01");point.position=tops[i];
            point.rotation=Quaternion.LookRotation(new Vector3(section.position.x-point.position.x,0,-10));
            MonsterSpawnZone encounter=zone.GetComponent<MonsterSpawnZone>();SerializedObject settings=new(encounter);
            settings.FindProperty("encounterId").stringValue="stage8-side-artillery-"+i;
            settings.FindProperty("requireCenterPastPlane").boolValue=true;
            SerializedProperty entries=settings.FindProperty("entries");entries.arraySize=1;
            SerializedProperty entry=entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("spawnPoint").objectReferenceValue=point;
            entry.FindPropertyRelative("monsterPrefab").objectReferenceValue=enemy;
            entry.FindPropertyRelative("count").intValue=1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach(Object changed in new Object[]{zone,box,point,encounter})PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
        }
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
    }
}
