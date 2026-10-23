using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Unity.Netcode;

public static class Stage8BattleSetup
{
    private static readonly Quaternion SlopeRotation = Quaternion.Euler(-14, 0, 0);
    private static Transform section;
    private static Vector3 origin;
    private static Material asphalt, frame, red, yellow;

    [MenuItem("Tools/Course/Build Stage 8 Battles")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit play mode first.");
        section = GameObject.Find("FullCourseSections").transform.Find("Section8");
        if (section == null) throw new System.InvalidOperationException("Stage 8 plate is missing.");
        if (section.Find("BattleLayout") != null) throw new System.InvalidOperationException("Battle layout already exists; edit it in the scene.");
        origin = section.position;
        asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrackAsphalt.mat");
        frame = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stage7ObstacleFrame.mat");
        red = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stage3PusherRed.mat");
        yellow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stage4FinishLineYellow.mat");
        GameObject layout = new GameObject("BattleLayout"); layout.transform.SetParent(section, false);
        Transform parent = layout.transform;
        BoulderMonster charger = BuildCharger();
        BoulderMonster hunter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BoulderHunter.prefab").GetComponent<BoulderMonster>();
        BoulderMonster artillery = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BoulderArtillery.prefab").GetComponent<BoulderMonster>();
        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (!list.Contains(charger.gameObject)) list.Add(new NetworkPrefab { Prefab = charger.gameObject });
        EditorUtility.SetDirty(list);

        // Staggered structures offer a short central route and roomy outer detours.
        Cube(parent,"CoverBlock_Left_01",-18,82,3.5f,new Vector3(25,7,18),frame);
        Cube(parent,"CoverBlock_Right_02",18,145,3.5f,new Vector3(26,7,23),frame);
        Cube(parent,"CoverBlock_Left_03",-17,205,3.5f,new Vector3(25,7,20),frame);
        Cube(parent,"EarlyCover_Left",-25,30,1.1f,new Vector3(12,2.2f,5),frame);
        Cube(parent,"EarlyCover_Right",25,42,1.1f,new Vector3(12,2.2f,5),frame);
        Rest(parent, "RestStop_01", 0, 100);
        Rest(parent, "RestStop_02", -8, 178);
        Rest(parent, "RestStop_03", 0, 250);
        for (int i=0;i<3;i++)
        {
            float side=i%2==0?1:-1;
            GameObject trap=Cube(parent,"RedPusher_"+i,side*24,65+i*65,1.7f,new Vector3(1,3.4f,5),red);
            PushingStageObstacle movement=trap.AddComponent<PushingStageObstacle>();
            SerializedObject move=new(movement);
            move.FindProperty("localPushDirection").vector3Value=side>0?Vector3.left:Vector3.right;
            move.FindProperty("strokeDistance").floatValue=9;
            move.FindProperty("phaseOffsetSeconds").floatValue=i*1.7f;
            move.ApplyModifiedPropertiesWithoutUndo();
            ShootableObstacle target=trap.AddComponent<ShootableObstacle>();
            SerializedObject damage=new(target);
            damage.FindProperty("obstacleId").stringValue="stage8-pusher-"+i;
            damage.ApplyModifiedPropertiesWithoutUndo();
            trap.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
        }
        GameObject leftTower=Cube(parent,"ArtilleryPlatform_Left",-44,225,2.5f,new Vector3(12,5,12),asphalt);
        GameObject rightTower=Cube(parent,"ArtilleryPlatform_Right",44,230,2.5f,new Vector3(12,5,12),asphalt);
        Zone(parent, "Wave01_Entry", 45,
            new[]{hunter,hunter,charger},new[]{Point(-15,67),Point(15,70),Point(0,77)});
        Zone(parent, "Wave02_Fork", 125,
            new[]{hunter,hunter,hunter,charger,charger},
            new[]{Point(-30,152),Point(0,160),Point(31,153),Point(-15,169),Point(34,171)});
        Zone(parent, "Wave03_ArtilleryAmbush", 210,
            new[]{artillery,artillery,charger,charger,hunter,hunter},
            new[]{TowerTop(leftTower),TowerTop(rightTower),Point(-30,192),Point(30,194),Point(-10,239),Point(20,241)});
        Zone(parent, "Wave04_FinalDefense", 269,
            new[]{hunter,hunter,hunter,charger,charger},
            new[]{Point(-22,289),Point(0,292),Point(22,289),Point(-33,285),Point(33,285)});

        // A level summit supports the final fight and provides a clear destination.
        Vector3 end=origin+SlopeRotation*(Vector3.forward*300);
        GameObject summit=GameObject.CreatePrimitive(PrimitiveType.Cube);
        summit.name="Stage8Summit"; summit.transform.SetParent(parent,true);
        summit.transform.position=end+new Vector3(0,-.6f,10);
        summit.transform.localScale=new Vector3(120,1.2f,20);
        summit.GetComponent<Renderer>().sharedMaterial=asphalt;
        GameObject line=GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name="Stage8FinishLine";line.transform.SetParent(parent,true);
        line.transform.position=end+new Vector3(0,.04f,14);
        line.transform.localScale=new Vector3(100,.06f,.7f);
        line.GetComponent<Renderer>().sharedMaterial=yellow;
        Object.DestroyImmediate(line.GetComponent<Collider>());
        GameObject goal=new GameObject("Stage8BoulderGoal");goal.transform.SetParent(parent,true);
        goal.transform.position=end+new Vector3(0,4,15);
        BoxCollider box=goal.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(120,12,4);
        StageGoalTrigger trigger=goal.AddComponent<StageGoalTrigger>();SerializedObject gs=new(trigger);
        gs.FindProperty("stageToComplete").intValue=8;
        gs.FindProperty("finishZ").floatValue=end.z+14;
        gs.ApplyModifiedPropertiesWithoutUndo();
        FallResetZone reset=Object.FindFirstObjectByType<FallResetZone>();
        if(reset!=null){BoxCollider r=reset.GetComponent<BoxCollider>();Vector3 size=r.size;size.z=Mathf.Max(size.z,3000);r.size=size;}
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
        Selection.activeGameObject=layout;
    }

    private static Vector3 Point(float x,float distance) => origin+SlopeRotation*new Vector3(x,.05f,distance);
    private static Vector3 TowerTop(GameObject tower)
    {
        // Use the rotated cube's top centre, not the top of its world AABB.
        return tower.transform.TransformPoint(Vector3.up*.5f)+Vector3.up*.03f;
    }
    private static GameObject Cube(Transform parent,string name,float x,float distance,float height,Vector3 size,Material material)
    {
        GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,true);
        obj.transform.SetPositionAndRotation(origin+SlopeRotation*new Vector3(x,height,distance),SlopeRotation);
        obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
    }
    private static void Rest(Transform parent,string name,float x,float distance)
    {
        Transform rest=new GameObject(name).transform;rest.SetParent(parent,true);
        Vector3 start=Point(x,distance-10);rest.position=start;
        float flatY=Point(x,distance+4).y+.12f;
        float[] ds={-10,-4,4,10};Vector3[] vertices=new Vector3[8];
        for(int i=0;i<4;i++)
        {
            Vector3 p=Point(x,distance+ds[i]);if(i==1||i==2)p.y=flatY;
            vertices[i*2]=p-start+Vector3.left*13;
            vertices[i*2+1]=p-start+Vector3.right*13;
        }
        Mesh mesh=new Mesh{name=name};mesh.vertices=vertices;
        mesh.triangles=new[]{0,2,1,1,2,3,2,4,3,3,4,5,4,6,5,5,6,7};
        mesh.uv=new[]{new Vector2(0,0),new Vector2(6.5f,0),new Vector2(0,1.5f),new Vector2(6.5f,1.5f),new Vector2(0,3.5f),new Vector2(6.5f,3.5f),new Vector2(0,5),new Vector2(6.5f,5)};
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Meshes/"+name+".asset"));
        rest.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        rest.gameObject.AddComponent<MeshRenderer>().sharedMaterial=asphalt;
        rest.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        GameObject lip=Cube(rest,"BoulderRestLip",x,distance-3,.25f,new Vector3(22,.5f,.7f),asphalt);
        Vector3 pos=lip.transform.position;pos.y=flatY+.25f;lip.transform.position=pos;
    }
    private static void Zone(Transform parent,string name,float distance,BoulderMonster[] monsters,Vector3[] positions)
    {
        GameObject obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterSpawnZone.prefab"),parent);
        obj.name=name;obj.transform.SetPositionAndRotation(Point(0,distance)+Vector3.up*4,Quaternion.identity);
        BoxCollider box=obj.GetComponent<BoxCollider>();box.size=new Vector3(124,14,2);box.isTrigger=true;
        SerializedObject settings=new(obj.GetComponent<MonsterSpawnZone>());
        settings.FindProperty("encounterId").stringValue="stage8-"+name;
        settings.FindProperty("requireCenterPastPlane").boolValue=true;
        SerializedProperty entries=settings.FindProperty("entries");entries.arraySize=monsters.Length;
        for(int i=0;i<monsters.Length;i++)
        {
            Transform spawn=new GameObject("Spawn_"+(i+1)).transform;spawn.SetParent(obj.transform,true);
            spawn.position=positions[i];spawn.rotation=Quaternion.Euler(0,180,0);
            SerializedProperty entry=entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("spawnPoint").objectReferenceValue=spawn;
            entry.FindPropertyRelative("monsterPrefab").objectReferenceValue=monsters[i];
            entry.FindPropertyRelative("count").intValue=1;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        PrefabUtility.RecordPrefabInstancePropertyModifications(obj.GetComponent<MonsterSpawnZone>());
        // Authoring-only start markers are ordinary transforms and remain editable.
    }
    private static BoulderMonster BuildCharger()
    {
        const string path="Assets/Prefabs/BoulderCharger.prefab";
        GameObject existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(existing!=null)return existing.GetComponent<BoulderMonster>();
        GameObject root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/BoulderHunter.prefab");
        try
        {
            root.name="BoulderCharger";
            Transform old=root.transform.Find("EnemyAVisual");if(old!=null)Object.DestroyImmediate(old.gameObject);
            GameObject visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QuarterView 3D Action BE5/Prefabs/Enemy B.prefab"),root.transform);
            visual.name="EnemyBVisual";
            Animator animator=visual.GetComponentInChildren<Animator>();
            if(animator==null)animator=visual.transform.GetChild(0).gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/BoulderHunter.controller");
            foreach(Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/QuarterView 3D Action BE5/Models/Simple Enemy A AnimTest.fbx"))
                if(asset is Avatar avatar)animator.avatar=avatar;
            animator.applyRootMotion=false;
            foreach(Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                BoxCollider hitbox=renderer.GetComponent<BoxCollider>();
                if(hitbox==null)hitbox=renderer.gameObject.AddComponent<BoxCollider>();
                hitbox.center=renderer.localBounds.center;hitbox.size=renderer.localBounds.size;hitbox.isTrigger=false;
            }
            SerializedObject settings=new(root.GetComponent<BoulderMonster>());
            settings.FindProperty("animator").objectReferenceValue=animator;
            settings.FindProperty("chargingHunter").boolValue=true;
            settings.FindProperty("maximumHealth").intValue=3;
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<NavMeshAgent>().speed=3f;
            return PrefabUtility.SaveAsPrefabAsset(root,path).GetComponent<BoulderMonster>();
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
