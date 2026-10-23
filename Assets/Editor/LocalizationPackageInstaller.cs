using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class LocalizationPackageInstaller
{
    private static AddRequest request;
    private static double deadline;
    public static void Install()
    {
        request = Client.Add("com.unity.localization");
        deadline = EditorApplication.timeSinceStartup + 300;
        EditorApplication.update += Poll;
    }
    private static void Poll()
    {
        if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Poll;
        bool success = request.IsCompleted && request.Status == StatusCode.Success;
        Debug.Log(success ? "Localization package installed." : "Localization install failed: " + request.Error?.message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
