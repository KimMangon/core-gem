using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage7CourseBuilder
{
    private const int LengthSegments = 128;
    private const int ArcSegments = 28;
    private const float Radius = 6.5f;
    private const float StartZ = 456f;
    private const float Length = 190f;
    private const float PreviousLength = 145f;
    private static readonly float[] ElevationTimes = { 0f, 0.23f, 0.45f, 0.66f, 0.84f, 1f };
    private static readonly float[] ElevationOffsets = { 0f, 8f, 1f, 12f, 3f, 10f };

    public static void UpgradeStageOneScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity");
        UpgradeOpenStageSeven();
    }

    public static void UpdateOpenStageSeven()
    {
        UpgradeOpenStageSeven();
    }

    [InitializeOnLoadMethod]
    private static void RegisterClosedPipeUpgrade()
    {
        EditorApplication.delayCall += UpgradeOpenStageSeven;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
    }

    private static void OnActiveSceneChanged(Scene previous, Scene current)
    {
        EditorApplication.delayCall += UpgradeOpenStageSeven;
    }

    private static void UpgradeOpenStageSeven()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().name != "Stage1")
            return;

        GameObject section = GameObject.Find("Section7");
        if (section == null)
            return;

        Mesh channelMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
            "Assets/Meshes/Stage7SewerChannel.asset");
        if (channelMesh == null)
            return;

        bool changed = false;
        Mesh ringsMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
            "Assets/Meshes/Stage7SewerRings.asset");
        if (channelMesh.vertexCount != (LengthSegments + 1) * (ArcSegments + 1) + 2)
        {
            ReplaceMesh(channelMesh, BuildChannelMesh());
            if (ringsMesh != null)
                ReplaceMesh(ringsMesh, BuildRingMesh());
            MeshCollider collider = section.transform.Find("SewerChannel")?.GetComponent<MeshCollider>();
            if (collider != null)
            {
                collider.sharedMesh = null;
                collider.sharedMesh = channelMesh;
            }
            EditorUtility.SetDirty(channelMesh);
            if (ringsMesh != null)
                EditorUtility.SetDirty(ringsMesh);
            float extension = Length - PreviousLength;
            MoveChildForward(section.transform, "Stage7FinishPlatform", extension);
            MoveChildForward(section.transform, "Stage7FinishLine", extension);
            Transform goal = section.transform.Find("Stage7BoulderGoal");
            if (goal != null)
            {
                goal.position += Vector3.forward * extension;
                StageGoalTrigger trigger = goal.GetComponent<StageGoalTrigger>();
                if (trigger != null)
                {
                    SerializedObject settings = new SerializedObject(trigger);
                    settings.FindProperty("finishZ").floatValue += extension;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            CreateStage7Obstacles(section.transform);
            changed = true;
        }

        Transform drain = section.transform.Find("SewerDrain");
        if (drain != null)
        {
            UnityEngine.Object.DestroyImmediate(drain.gameObject);
            changed = true;
        }

        Transform startLine = section.transform.Find("Stage7StartLine");
        if (startLine != null && startLine.localScale.x > 6f)
        {
            startLine.localScale = new Vector3(5.4f, 0.05f, 0.4f);
            changed = true;
        }

        for (int i = 1; i <= 6; i++)
        {
            Transform lamp = section.transform.Find($"MaintenanceLight_{i:00}");
            if (lamp == null)
                continue;
            UnityEngine.Object.DestroyImmediate(lamp.gameObject);
            changed = true;
        }

        changed |= UpgradeSwingLayout(section.transform);

        if (!changed)
            return;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Stage 7 course geometry and obstacles updated.");
    }

    private static void ReplaceMesh(Mesh destination, Mesh source)
    {
        destination.Clear();
        destination.vertices = source.vertices;
        destination.uv = source.uv;
        destination.triangles = source.triangles;
        destination.RecalculateNormals();
        destination.RecalculateBounds();
        UnityEngine.Object.DestroyImmediate(source);
    }

    private static void MoveChildForward(Transform section, string name, float distance)
    {
        Transform child = section.Find(name);
        if (child != null)
            child.position += Vector3.forward * distance;
    }

    [MenuItem("Tools/Course/Create Stage 7 Sewer")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Stage1" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open Stage1 in Edit Mode before creating Stage 7.");
        if (HasSectionSeven())
            return;
        GameObject fullCourse = GameObject.Find("FullCourseSections");
        if (fullCourse == null)
            throw new InvalidOperationException("FullCourseSections is missing from Stage1.");

        Transform section = new GameObject("Section7").transform;
        section.SetParent(fullCourse.transform, false);
        Material concrete = GetOrCreateMaterial("Assets/Materials/Stage7SewerConcrete.mat",
            new Color(0.27f, 0.33f, 0.32f), 0.08f);
        Material seam = GetOrCreateMaterial("Assets/Materials/Stage7SewerSeams.mat",
            new Color(0.09f, 0.15f, 0.16f), 0.32f);
        Material road = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/TrackAsphalt.mat");
        Material white = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/TrackLine.mat");
        Material yellow = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Stage4FinishLineYellow.mat");

        Mesh channelMesh = GetOrCreateMesh("Assets/Meshes/Stage7SewerChannel.asset",
            BuildChannelMesh);
        GameObject channel = CreateMeshObject(section, "SewerChannel", channelMesh, concrete);
        channel.AddComponent<MeshCollider>().sharedMesh = channelMesh;
        CreateMeshObject(section, "SewerJointRings",
            GetOrCreateMesh("Assets/Meshes/Stage7SewerRings.asset", BuildRingMesh), seam);
        CreateStage7Obstacles(section);

        CreateCube(section, "Stage7FinishPlatform",
            new Vector3(63.97f, 77.73f, 649f), new Vector3(24f, 1.2f, 20f), road);
        CreateCube(section, "Stage7StartLine",
            new Vector3(49.97f, 68.37f, 461f), new Vector3(5.4f, 0.05f, 0.4f), white,
            false);
        CreateCube(section, "Stage7FinishLine",
            new Vector3(63.97f, 78.37f, 652f), new Vector3(18f, 0.05f, 0.4f), yellow,
            false);

        GameObject goal = new GameObject("Stage7BoulderGoal");
        goal.transform.SetParent(section, false);
        goal.transform.position = new Vector3(63.97f, 81f, 653f);
        BoxCollider goalCollider = goal.AddComponent<BoxCollider>();
        goalCollider.isTrigger = true;
        goalCollider.size = new Vector3(20f, 8f, 6f);
        StageGoalTrigger goalTrigger = goal.AddComponent<StageGoalTrigger>();
        SerializedObject goalSettings = new SerializedObject(goalTrigger);
        goalSettings.FindProperty("stageManager").objectReferenceValue =
            UnityEngine.Object.FindAnyObjectByType<StageManager>();
        goalSettings.FindProperty("stageToComplete").intValue = 7;
        goalSettings.FindProperty("finishZ").floatValue = 651f;
        goalSettings.ApplyModifiedPropertiesWithoutUndo();

        FallResetZone fallZone = UnityEngine.Object.FindAnyObjectByType<FallResetZone>();
        if (fallZone != null)
        {
            BoxCollider resetCollider = fallZone.GetComponent<BoxCollider>();
            if (resetCollider != null)
            {
                Vector3 size = resetCollider.size;
                size.z = Mathf.Max(size.z, 2000f);
                resetCollider.size = size;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = section.gameObject;
        Debug.Log("Stage 7 sewer course created inside Stage1.");
    }

    private static bool HasSectionSeven()
    {
        foreach (Transform transform in UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include))
            if (transform.name == "Section7" &&
                transform.GetComponentInParent<StageManager>() == null)
                return true;
        return false;
    }

    private static Material GetOrCreateMaterial(string path, Color color, float smoothness)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard");
        Material material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Mesh GetOrCreateMesh(string path, Func<Mesh> build)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
            return existing;
        Mesh mesh = build();
        mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static GameObject CreateMeshObject(Transform parent, string name,
        Mesh mesh, Material material)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return gameObject;
    }

    private static GameObject CreateCube(Transform parent, string name,
        Vector3 position, Vector3 scale, Material material, bool collision = true)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, true);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!collision)
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        return cube;
    }

    private static void CreateStage7Obstacles(Transform section)
    {
        Material pusherMaterial = GetOrCreateMaterial("Assets/Materials/Stage7Pusher.mat",
            new Color(0.87f, 0.27f, 0.13f), 0.35f);
        Material frameMaterial = GetOrCreateMaterial("Assets/Materials/Stage7ObstacleFrame.mat",
            new Color(0.12f, 0.17f, 0.18f), 0.4f);

        CreatePusher(section, 1, 0.20f, 1f, pusherMaterial, frameMaterial);
        CreateSwing(section, 1, 0.34f, 0f, pusherMaterial, frameMaterial);
        CreatePusher(section, 2, 0.47f, -1f, pusherMaterial, frameMaterial);
        CreatePusher(section, 3, 0.72f, 1f, pusherMaterial, frameMaterial);
        CreateSwing(section, 2, 0.84f, 1.6f, pusherMaterial, frameMaterial);
    }

    private static void CreatePusher(Transform section, int number, float t, float side,
        Material pusherMaterial, Material frameMaterial)
    {
        Vector3 floor = Floor(t);
        Quaternion orientation = CourseOrientation(t);
        Vector3 right = orientation * Vector3.right;

        GameObject head = CreateCube(section, $"Stage7Pusher_{number:00}",
            floor + right * (side * 5f) - Vector3.up * 0.2f,
            new Vector3(0.85f, 4.5f, 2.6f), pusherMaterial);
        head.transform.rotation = orientation;
        Rigidbody body = head.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        PushingStageObstacle obstacle = head.AddComponent<PushingStageObstacle>();
        SerializedObject settings = new SerializedObject(obstacle);
        settings.FindProperty("localPushDirection").vector3Value =
            side > 0f ? Vector3.left : Vector3.right;
        settings.FindProperty("strokeDistance").floatValue = 5.8f;
        settings.FindProperty("cycleSeconds").floatValue = 5.1f;
        settings.FindProperty("extendSeconds").floatValue = 0.52f;
        settings.FindProperty("holdSeconds").floatValue = 0.22f;
        settings.FindProperty("retractSeconds").floatValue = 0.76f;
        settings.FindProperty("phaseOffsetSeconds").floatValue = number * 1.45f;
        settings.FindProperty("boulderHitImpulse").floatValue = 42f;
        settings.ApplyModifiedPropertiesWithoutUndo();

        GameObject housing = CreateCube(section, $"Stage7PusherHousing_{number:00}",
            floor + right * (side * 7.1f) - Vector3.up * 1.2f,
            new Vector3(2.2f, 3.8f, 3.6f), frameMaterial, false);
        housing.transform.rotation = orientation;
        GameObject guide = CreateCube(section, $"Stage7PusherGuide_{number:00}",
            floor + right * (side * 6.2f) - Vector3.up * 0.2f,
            new Vector3(2.3f, 0.55f, 0.7f), frameMaterial, false);
        guide.transform.rotation = orientation;
    }

    private static void CreateSwing(Transform section, int number, float t, float phase,
        Material weightMaterial, Material frameMaterial)
    {
        Vector3 floor = Floor(t);
        Quaternion orientation = CourseOrientation(t);
        GameObject pivot = new GameObject($"Stage7Swing_{number:00}");
        pivot.transform.SetParent(section, true);
        pivot.transform.SetPositionAndRotation(floor + Vector3.up * 11.2f, orientation);

        GameObject hinge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hinge.name = "SwingHinge";
        hinge.transform.SetParent(pivot.transform, false);
        hinge.transform.localScale = Vector3.one * 0.65f;
        hinge.GetComponent<MeshRenderer>().sharedMaterial = frameMaterial;
        UnityEngine.Object.DestroyImmediate(hinge.GetComponent<SphereCollider>());

        const float weightCenterDistance = 9f;
        const float weightRadius = 1.3f;
        float ropeLength = weightCenterDistance - weightRadius;

        GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rope.name = "SwingRope";
        rope.transform.SetParent(pivot.transform, false);
        rope.transform.localPosition = Vector3.down * (ropeLength * 0.5f);
        rope.transform.localScale = new Vector3(0.09f, ropeLength * 0.5f, 0.09f);
        rope.GetComponent<MeshRenderer>().sharedMaterial = frameMaterial;
        UnityEngine.Object.DestroyImmediate(rope.GetComponent<CapsuleCollider>());

        GameObject weight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        weight.name = "SwingWeight";
        weight.transform.SetParent(pivot.transform, false);
        weight.transform.localPosition = Vector3.down * weightCenterDistance;
        weight.transform.localScale = Vector3.one * (weightRadius * 2f);
        weight.GetComponent<MeshRenderer>().sharedMaterial = weightMaterial;

        Rigidbody body = pivot.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        SwingingStageObstacle obstacle = pivot.AddComponent<SwingingStageObstacle>();
        SerializedObject settings = new SerializedObject(obstacle);
        settings.FindProperty("amplitudeDegrees").floatValue = 52f;
        settings.FindProperty("cycleSeconds").floatValue = 4.2f;
        settings.FindProperty("phaseOffsetSeconds").floatValue = phase;
        settings.FindProperty("boulderHitImpulse").floatValue = 12f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool UpgradeSwingLayout(Transform section)
    {
        bool changed = false;
        Material frameMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Stage7ObstacleFrame.mat");
        Material weightMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Stage7Pusher.mat");
        for (int number = 1; number <= 2; number++)
        {
            Transform pivot = section.Find($"Stage7Swing_{number:00}");
            if (pivot == null ||
                (pivot.Find("SwingRope") != null &&
                 pivot.Find("SwingWeight") != null &&
                 pivot.Find("SwingWeight_Left") == null))
                continue;

            UnityEngine.Object.DestroyImmediate(pivot.gameObject);
            DestroySectionChild(section, $"Stage7SwingCrossbar_{number:00}");
            DestroySectionChild(section, $"Stage7SwingPost_{number:00}_-1");
            DestroySectionChild(section, $"Stage7SwingPost_{number:00}_1");
            DestroySectionChild(section, $"Stage7SwingSupport_{number:00}");
            DestroySectionChild(section, $"Stage7SwingBracket_{number:00}");
            CreateSwing(section, number, number == 1 ? 0.34f : 0.84f,
                number == 1 ? 0f : 1.6f, weightMaterial, frameMaterial);
            changed = true;
        }
        return changed;
    }

    private static void DestroySectionChild(Transform section, string name)
    {
        Transform child = section.Find(name);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static Quaternion CourseOrientation(float t)
    {
        Vector3 tangent = (Floor(Mathf.Min(1f, t + 0.001f)) -
            Floor(Mathf.Max(0f, t - 0.001f))).normalized;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(tangent, Vector3.up).normalized;
        return Quaternion.LookRotation(horizontalForward, Vector3.up);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Envelope(float t) =>
        Smooth(t / 0.12f) * Smooth((1f - t) / 0.12f);

    private static Vector3 Floor(float t)
    {
        float eased = Smooth(t);
        float envelope = Envelope(t);
        return new Vector3(
            49.97f + 14f * eased + 12f * envelope * Mathf.Sin(3f * Mathf.PI * t),
            68.33f + ElevationAt(t),
            StartZ + Length * t);
    }

    private static float ElevationAt(float t)
    {
        t = Mathf.Clamp01(t);
        for (int i = 1; i < ElevationTimes.Length; i++)
            if (t <= ElevationTimes[i])
            {
                float progress = (t - ElevationTimes[i - 1]) /
                    (ElevationTimes[i] - ElevationTimes[i - 1]);
                return Mathf.Lerp(ElevationOffsets[i - 1],
                    ElevationOffsets[i], Smooth(progress));
            }
        return ElevationOffsets[ElevationOffsets.Length - 1];
    }

    private static Vector3 ChannelPoint(float t, float arcFraction)
    {
        Vector3 floor = Floor(t);
        Vector3 tangent = (Floor(Mathf.Min(1f, t + 0.001f)) -
            Floor(Mathf.Max(0f, t - 0.001f))).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        Vector3 up = Vector3.Cross(tangent, right).normalized;
        float bank = 12f * Mathf.Sin(4f * Mathf.PI * t) * Envelope(t);
        Quaternion banking = Quaternion.AngleAxis(bank, tangent);
        float angle = 2f * Mathf.PI * arcFraction;
        return floor - Vector3.up * Radius +
            banking * (right * (Radius * Mathf.Sin(angle)) +
                       up * (Radius * Mathf.Cos(angle)));
    }

    private static Mesh BuildChannelMesh()
    {
        int row = ArcSegments + 1;
        int surfaceCount = (LengthSegments + 1) * row;
        Vector3[] vertices = new Vector3[surfaceCount + 2];
        Vector2[] uv = new Vector2[vertices.Length];
        List<int> triangles = new List<int>();
        for (int i = 0; i <= LengthSegments; i++)
        {
            float t = i / (float)LengthSegments;
            for (int j = 0; j <= ArcSegments; j++)
            {
                int index = i * row + j;
                Vector3 point = ChannelPoint(t, j / (float)ArcSegments);
                vertices[index] = point;
                uv[index] =
                    new Vector2(j / (float)ArcSegments * 2f, t * 18f);
            }
        }
        for (int i = 0; i < LengthSegments; i++)
            for (int j = 0; j < ArcSegments; j++)
            {
                int a = i * row + j;
                int b = (i + 1) * row + j;
                int c = a + 1;
                int d = b + 1;
                AddQuad(triangles, a, b, c, d);
            }
        vertices[surfaceCount] = Floor(0f) - Vector3.up * Radius;
        vertices[surfaceCount + 1] = Floor(1f) - Vector3.up * Radius;
        for (int j = 0; j < ArcSegments; j++)
        {
            triangles.Add(surfaceCount);
            triangles.Add(j + 1);
            triangles.Add(j);
            int end = LengthSegments * row + j;
            triangles.Add(surfaceCount + 1);
            triangles.Add(end);
            triangles.Add(end + 1);
        }
        Mesh mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildRingMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int ring = 1; ring <= 11; ring++)
        {
            float t = ring / 12f;
            int start = vertices.Count;
            for (int j = 0; j <= ArcSegments; j++)
            {
                float arc = j / (float)ArcSegments;
                vertices.Add(ChannelPoint(t - 0.0012f, arc) + Vector3.up * 0.055f);
                vertices.Add(ChannelPoint(t + 0.0012f, arc) + Vector3.up * 0.055f);
            }
            for (int j = 0; j < ArcSegments; j++)
                AddQuad(triangles, start + j * 2, start + j * 2 + 1,
                    start + j * 2 + 2, start + j * 2 + 3);
        }
        Mesh mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(c);
        triangles.Add(b);
        triangles.Add(d);
    }
}
