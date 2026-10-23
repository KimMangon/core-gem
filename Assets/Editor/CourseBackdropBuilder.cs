using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CourseBackdropBuilder
{
    private const string RootName = "CourseBackdrop";
    private const string MeshFolder = "Assets/Meshes";
    private const float GroundHeight = -113f;

    [MenuItem("Tools/Course/Build Mountain Backdrop")]
    public static void BuildInOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Stage1" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open Stage1 in Edit Mode to build the backdrop.");

        GameObject previous = GameObject.Find(RootName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/GroundDirt.mat");
        Texture2D mountainImage = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Materials/DistantMountainPanorama.png");
        if (groundMaterial == null || mountainImage == null)
            throw new InvalidOperationException("Course backdrop materials are missing.");

        Material backdropSoil = GetOrCreateSoilMaterial(
            "Assets/Materials/BackdropSoil.mat", groundMaterial,
            new Color(0.73f, 0.73f, 0.70f));
        Material mountainMaterial = GetOrCreateMountainMaterial(mountainImage);

        Transform root = new GameObject(RootName).transform;
        CreateMeshObject(root, "DistantGround", "CourseBackdropGround.asset",
            BuildGround(), backdropSoil);
        CreateMeshObject(root, "PaintedMountainHorizon", "CourseBackdropHorizon.asset",
            BuildMountainHorizon(), mountainMaterial);
        Transform horizon = root.Find("PaintedMountainHorizon");
        horizon.position = new Vector3(35f, 0f, 330f);
        MeshRenderer horizonRenderer = horizon.GetComponent<MeshRenderer>();
        horizonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        horizonRenderer.receiveShadows = false;
        horizon.gameObject.AddComponent<MountainHorizonFollower>();

        AssetDatabase.DeleteAsset("Assets/Meshes/CourseBackdropWest.asset");
        AssetDatabase.DeleteAsset("Assets/Meshes/CourseBackdropEast.asset");
        AssetDatabase.DeleteAsset("Assets/Meshes/CourseBackdropFar.asset");
        AssetDatabase.DeleteAsset("Assets/Materials/BackdropSoilDark.mat");
        AssetDatabase.DeleteAsset("Assets/Materials/BackdropSoilLight.mat");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Tiled ground and camera-following mountain panorama added beneath the course.");
    }

    private static Material GetOrCreateSoilMaterial(string path, Material source, Color tint)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            material.color = tint;
            EditorUtility.SetDirty(material);
            return material;
        }
        material = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        material.color = tint;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material GetOrCreateMountainMaterial(Texture2D image)
    {
        const string path = "Assets/Materials/DistantMountainPanorama.mat";
        TextureImporter importer = AssetImporter.GetAtPath(
            AssetDatabase.GetAssetPath(image)) as TextureImporter;
        if (importer != null &&
            (importer.wrapModeU != TextureWrapMode.Mirror || !importer.alphaIsTransparency))
        {
            importer.wrapModeU = TextureWrapMode.Mirror;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("CoreGem/DistantMountainBackdrop");
        if (shader == null)
            throw new InvalidOperationException("Distant mountain backdrop shader is unavailable.");
        if (material == null)
        {
            material = new Material(shader) { name = "DistantMountainPanorama" };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }
        material.SetTexture("_BaseMap", image);
        material.SetColor("_Tint", new Color(1f, 0.86f, 0.72f));
        material.SetFloat("_FadeStart", 0.03f);
        material.SetFloat("_FadeEnd", 0.37f);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateMeshObject(Transform parent, string name, string assetName,
        Mesh mesh, params Material[] materials)
    {
        string path = $"{MeshFolder}/{assetName}";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            existing = mesh;
        }
        else
        {
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.uv = mesh.uv;
            existing.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
                existing.SetTriangles(mesh.GetTriangles(i), i);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        GameObject surface = new GameObject(name);
        surface.transform.SetParent(parent, false);
        surface.AddComponent<MeshFilter>().sharedMesh = existing;
        surface.AddComponent<MeshRenderer>().sharedMaterials = materials;
    }

    private static Mesh BuildGround()
    {
        Mesh mesh = new Mesh { name = "CourseBackdropGround" };
        mesh.vertices = new[]
        {
            new Vector3(-3200f, GroundHeight, -3000f),
            new Vector3(3300f, GroundHeight, -3000f),
            new Vector3(-3200f, GroundHeight, 3500f),
            new Vector3(3300f, GroundHeight, 3500f)
        };
        mesh.uv = new[]
        {
            Vector2.zero, new Vector2(1300f, 0f),
            new Vector2(0f, 1300f), new Vector2(1300f, 1300f)
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildMountainHorizon()
    {
        const int segments = 96;
        // The horizon follows each game camera, so its distance never changes.
        const float radius = 720f;
        const float bottom = -114f;
        const float top = 220f;
        Mesh mesh = new Mesh { name = "CourseBackdropHorizon" };
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float angle = fraction * Mathf.PI * 2f;
            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x, bottom, z);
            vertices[i * 2 + 1] = new Vector3(x, top, z);
            uv[i * 2] = new Vector2(fraction * 4f, 0f);
            uv[i * 2 + 1] = new Vector2(fraction * 4f, 1f);
            if (i == segments)
                continue;
            int triangle = i * 6;
            int vertex = i * 2;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 1;
            triangles[triangle + 5] = vertex + 3;
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
