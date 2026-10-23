using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ResponsiveWindowSetup
{
    [MenuItem("Tools/UI/Apply Responsive UI and Resizable Window")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Apply window settings outside Play Mode.");
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = true;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/Stage1.unity");
        if (!scene.isLoaded)
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity", OpenSceneMode.Additive);
        int configured = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                GameUiScaling.Configure(canvas);
                EditorUtility.SetDirty(canvas.GetComponent<UnityEngine.UI.CanvasScaler>());
                configured++;
            }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Responsive UI saved: " + configured + " screen canvases; resizableWindow=true; allowFullscreenSwitch=true.");
    }
}
