using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class MenuCourseCameraSetup
{
    [MenuItem("Tools/UI/Set Up Menu Course Camera")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Configure menu camera outside Play Mode.");
        var menu = Object.FindAnyObjectByType<MenuManager>();
        if (menu == null) throw new System.InvalidOperationException("Main course menu scene must be open.");
        var scene = menu.gameObject.scene;
        GameObject original = null, cameraObject = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Main Camera") original = root;
            if (root.name == "Menu Course Camera") cameraObject = root;
        }
        if (cameraObject == null)
        {
            cameraObject = new GameObject("Menu Course Camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
        }
        var camera = cameraObject.GetComponent<Camera>();
        if (camera == null) camera = cameraObject.AddComponent<Camera>();
        if (original != null) camera.CopyFrom(original.GetComponent<Camera>());
        camera.fieldOfView = 52f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 2500f;
        camera.depth = 5f;
        camera.enabled = false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var listener = cameraObject.GetComponent<AudioListener>();
        if (listener == null) listener = cameraObject.AddComponent<AudioListener>();
        listener.enabled = false;
        var tour = cameraObject.GetComponent<MenuCourseCamera>();
        if (tour == null) tour = cameraObject.AddComponent<MenuCourseCamera>();
        var serialized = new SerializedObject(tour);
        serialized.FindProperty("tourCamera").objectReferenceValue = camera;
        serialized.FindProperty("originalMenuCamera").objectReferenceValue = original != null ? original.GetComponent<Camera>() : null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        camera.transform.position = new Vector3(90f,100f,-80f);
        camera.transform.LookAt(new Vector3(20f,30f,60f));
        EditorUtility.SetDirty(tour);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Menu course camera saved: 240-second smooth loop, automatic player camera handoff.");
    }
}
