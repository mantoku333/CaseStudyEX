using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class ElegantSuccessVerification
{
    private static TestRunnerApi runner;
    private static Results callbacks;
    [MenuItem("Tools/Effects/Verify Elegant Success System")]
    public static void Verify()
    {
        Directory.CreateDirectory("Temp/ElegantSuccessTests");
        // A run that fails to start never reaches RunFinished, so the previous runner would
        // otherwise stay alive and silently swallow every later request. Tear it down instead.
        DisposeRunner();
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        callbacks = new Results();
        runner.RegisterCallbacks(callbacks);
        try
        {
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { "ElegantActionChainTests", "ElegantActionSuccessTests", "ElegantPointIntegrationTests" }
            }) { runSynchronously = true });
        }
        catch
        {
            DisposeRunner();
            throw;
        }
    }

    [MenuItem("Tools/Effects/Install Elegant Success Sensor")]
    public static void Install()
    {
        const string path = "Assets/Prefabs/Player/Player.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<ElegantActionSuccessSensor>() == null)
            {
                root.AddComponent<ElegantActionSuccessSensor>();
            }
            // RequireComponent may have supplied it during prefab deserialization.
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void DisposeRunner()
    {
        if (runner == null)
        {
            runner = null;
            callbacks = null;
            return;
        }

        if (callbacks != null) runner.UnregisterCallbacks(callbacks);
        Object.DestroyImmediate(runner);
        runner = null;
        callbacks = null;
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Temp/ElegantSuccessTests/Results.xml");
            File.WriteAllText("Temp/ElegantSuccessTests/Result.txt",
                $"{result.TestStatus}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}");
            EditorApplication.delayCall += DisposeRunner;
        }
    }
}
