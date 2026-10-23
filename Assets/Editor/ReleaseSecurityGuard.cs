using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Run on every build, including scripted builds. No secret values enter the log.</summary>
public sealed class ReleaseSecurityGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        BuildOptions forbidden = BuildOptions.Development | BuildOptions.AllowDebugging |
            BuildOptions.ConnectWithProfiler | BuildOptions.EnableDeepProfilingSupport;
        if ((report.summary.options & forbidden) != 0)
            throw new BuildFailedException("Release security: disable Development Build, debugging and profiler options.");
        foreach (string path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (path.Replace('\\', '/').Contains("/Editor/")) continue;
            if (extension == ".pfx" || extension == ".p12" || extension == ".key")
                throw new BuildFailedException("Private credential file must be outside Assets: " + path);
            if (extension != ".cs" && extension != ".json" && extension != ".txt" && extension != ".pem" &&
                extension != ".asset" && extension != ".yaml" && extension != ".bytes") continue;
            if (new FileInfo(path).Length > 8 * 1024 * 1024) continue;
            string content = File.ReadAllText(path);
            if (content.Contains("PRIVATE KEY-----") ||
                Regex.IsMatch(content, @"[""'](?:client_secret|service_account_secret|admin_api_key)[""']\s*[:=]\s*[""'][^""']{8,}", RegexOptions.IgnoreCase))
                throw new BuildFailedException("Possible administrator secret must be removed from player assets: " + path);
        }
    }

    [MenuItem("Tools/Security/Use Release Build Settings")]
    public static void UseReleaseSettings()
    {
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
    }
}
