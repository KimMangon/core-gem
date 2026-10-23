using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage8CourseBuilder
{
    [MenuItem("Tools/Course/Create Stage 8 Incline Plate")]
    public static void BuildPlate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Stage1")
            throw new System.InvalidOperationException("Open Stage1 in edit mode.");
        Transform full = GameObject.Find("FullCourseSections").transform;
        if (full.Find("Section8") != null) return;
        Collider previous = full.Find("Section7/Stage7FinishPlatform").GetComponent<Collider>();
        Vector3 start = new Vector3(previous.bounds.center.x, previous.bounds.max.y, previous.bounds.max.z);
        const float length = 300f;
        const float width = 120f;
        const float angle = 14f;
        float rise = Mathf.Sin(angle * Mathf.Deg2Rad) * length;
        float run = Mathf.Cos(angle * Mathf.Deg2Rad) * length;
        GameObject section = new GameObject("Section8");
        section.transform.SetParent(full, false);
        section.transform.position = start;
        GameObject plate = new GameObject("Stage8Incline_300x120_14deg");
        plate.transform.SetParent(section.transform, false);
        Vector3[] v = {
            new(-width/2,0,0), new(width/2,0,0),
            new(-width/2,rise,run), new(width/2,rise,run),
            new(-width/2,-1.2f,0), new(width/2,-1.2f,0),
            new(-width/2,rise-1.2f,run), new(width/2,rise-1.2f,run)
        };
        Mesh mesh = new Mesh { name = "Stage8Incline_14deg" };
        mesh.vertices = v;
        mesh.triangles = new[] { 0,2,1,1,2,3, 4,5,6,5,7,6,
            0,4,2,2,4,6, 1,3,5,3,7,5, 0,1,4,1,5,4, 2,6,3,3,6,7 };
        mesh.uv = new[] { new Vector2(0,0),new Vector2(30,0),new Vector2(0,75f),new Vector2(30,75f),
            new Vector2(0,0),new Vector2(30,0),new Vector2(0,75f),new Vector2(30,75f) };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Meshes/Stage8Incline.asset");
        AssetDatabase.CreateAsset(mesh, path);
        plate.AddComponent<MeshFilter>().sharedMesh = mesh;
        plate.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrackAsphalt.mat");
        plate.AddComponent<MeshCollider>().sharedMesh = mesh;
        Transform entrance = new GameObject("Stage8StartMarker").transform;
        entrance.SetParent(section.transform, false);
        Transform end = new GameObject("Stage8EndMarker").transform;
        end.SetParent(section.transform, false); end.localPosition = new Vector3(0,rise,run);
        EditorSceneManager.MarkSceneDirty(section.scene);
        EditorSceneManager.SaveScene(section.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
        Selection.activeGameObject = plate;
    }

    public static void ResizePlate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit play mode first.");
        Transform section = GameObject.Find("FullCourseSections").transform.Find("Section8");
        MeshCollider collider = section.GetComponentInChildren<MeshCollider>();
        Mesh mesh = collider.sharedMesh;
        float rise = Mathf.Sin(14f * Mathf.Deg2Rad) * 300f;
        float run = Mathf.Cos(14f * Mathf.Deg2Rad) * 300f;
        mesh.vertices = new[] {
            new Vector3(-60,0,0),new Vector3(60,0,0),new Vector3(-60,rise,run),new Vector3(60,rise,run),
            new Vector3(-60,-1.2f,0),new Vector3(60,-1.2f,0),new Vector3(-60,rise-1.2f,run),new Vector3(60,rise-1.2f,run)
        };
        mesh.uv = new[] { new Vector2(0,0),new Vector2(30,0),new Vector2(0,75f),new Vector2(30,75f),
            new Vector2(0,0),new Vector2(30,0),new Vector2(0,75f),new Vector2(30,75f) };
        mesh.name = "Stage8Incline_14deg";
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        collider.sharedMesh = null; collider.sharedMesh = mesh;
        collider.name = "Stage8Incline_300x120_14deg";
        section.Find("Stage8EndMarker").localPosition = new Vector3(0,rise,run);
        EditorUtility.SetDirty(mesh);
        EditorSceneManager.MarkSceneDirty(section.gameObject.scene);
        EditorSceneManager.SaveScene(section.gameObject.scene);
        AssetDatabase.SaveAssets();
        MonsterSetupEditor.RebuildNavigation();
    }
}
