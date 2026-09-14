using System;
using System.IO;
using UnityEditor;

[InitializeOnLoad]
public static class GracefulTrailWorkRunner
{
    static GracefulTrailWorkRunner() { EditorApplication.update += Run; }

    private static void Run()
    {
        const string request = "Temp/GracefulTrail.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        string action = File.ReadAllText(request).Trim();
        File.Delete(request);
        if (action == "refresh") { AssetDatabase.Refresh(); return; }
        try
        {
            if (action == "trail-regression")
            {
                GracefulTrailRegressionVerification.Verify();
                File.WriteAllText("Temp/GracefulTrail.result", "OK: trail-regression");
                return;
            }
            if (action == "ribbon-continuity")
            {
                GracefulTrailRibbonContinuityVerification.Verify();
                File.WriteAllText("Temp/GracefulTrail.result", "OK: ribbon-continuity");
                return;
            }
            if (action == "points-tests") { ElegantSuccessVerification.Verify(); return; }
            if (action == "points-install") { ElegantSuccessVerification.Install(); return; }
            if (action == "install")
            {
                // Rebuilds the prefab and re-applies the scene sorting to every renderer, new ribbons included.
                GracefulTrailBuilder.Install();
                File.WriteAllText("Temp/GracefulTrail.result", "OK: install");
                return;
            }
            if (action == "build") GracefulTrailBuilder.RebuildPrefab();
            GracefulTrailVerification.Verify();
            File.WriteAllText("Temp/GracefulTrail.result", "OK");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Temp/GracefulTrail.result", exception.ToString());
            UnityEngine.Debug.LogException(exception);
        }
    }
}
