using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;
using ApiTestStatus = UnityEditor.TestTools.TestRunner.Api.TestStatus;

namespace CaseStudy.EditorTools
{
    internal sealed class FixTestRunnerWindow : EditorWindow
    {
        private const string WindowTitle = "Fix Test Runner";
        private const string ResultsDirectory = "TestResults";
        private const string DefaultPerformanceLabel = "FixPerformance";
        private const int MaxFailureRows = 20;

        private static readonly List<string> Failures = new List<string>();
        private static TestRunnerApi testRunnerApi;
        private static FixTestRunnerCallbacks callbacks;
        private static string currentRunGuid;
        private static string currentRunLabel = "Idle";
        private static ApiTestStatus? lastStatus;
        private static int lastPassCount;
        private static int lastFailCount;
        private static int lastSkipCount;
        private static int lastInconclusiveCount;
        private static double lastDurationSeconds;
        private static string lastResultFilePath;
        private static bool isRunning;

        private Vector2 failureScroll;
        private string performanceLabel = DefaultPerformanceLabel;
        private float performanceWarmupSeconds = 2f;
        private float performanceCaptureSeconds = 15f;

        [MenuItem("Tools/CaseStudy/Fix Test Runner")]
        private static void OpenFromMenu()
        {
            OpenWindow();
        }

        [MenuItem("Tools/CaseStudy/Run Fix EditMode Tests")]
        private static void RunEditModeFromMenu()
        {
            OpenWindow().RunSingle(TestMode.EditMode);
        }

        [MenuItem("Tools/CaseStudy/Run Fix PlayMode Tests")]
        private static void RunPlayModeFromMenu()
        {
            OpenWindow().RunSingle(TestMode.PlayMode);
        }

        [MenuItem("Tools/CaseStudy/Run Fix All Tests")]
        private static void RunAllFromMenu()
        {
            OpenWindow().RunAll();
        }

        internal static FixTestRunnerWindow OpenWindow()
        {
            FixTestRunnerWindow window = GetWindow<FixTestRunnerWindow>(WindowTitle);
            window.minSize = new Vector2(560f, 520f);
            window.Show();
            return window;
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(6f);
            DrawActions();
            EditorGUILayout.Space(8f);
            DrawLastResult();
            EditorGUILayout.Space(8f);
            DrawFailures();
            EditorGUILayout.Space(10f);
            DrawPerformanceCapture();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Fix Preflight", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Run Unity Test Framework tests and capture simple performance snapshots before exporting a Fix build.",
                MessageType.Info);
        }

        private void DrawActions()
        {
            using (new EditorGUI.DisabledScope(isRunning || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Run EditMode", GUILayout.Height(32f)))
                    {
                        RunSingle(TestMode.EditMode);
                    }

                    if (GUILayout.Button("Run PlayMode", GUILayout.Height(32f)))
                    {
                        RunSingle(TestMode.PlayMode);
                    }

                    if (GUILayout.Button("Run All", GUILayout.Height(32f)))
                    {
                        RunAll();
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!isRunning || string.IsNullOrWhiteSpace(currentRunGuid)))
                {
                    if (GUILayout.Button("Cancel Current Run"))
                    {
                        TestRunnerApi.CancelTestRun(currentRunGuid);
                    }
                }

                if (GUILayout.Button("Open Unity Test Runner"))
                {
                    EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
                }
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode && !isRunning)
            {
                EditorGUILayout.HelpBox("Stop Play Mode before starting a test run.", MessageType.Warning);
            }
        }

        private void DrawLastResult()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            string statusText = isRunning
                ? $"Running: {currentRunLabel}"
                : lastStatus.HasValue
                    ? $"Last result: {lastStatus.Value}"
                    : "No test run yet.";

            EditorGUILayout.LabelField(statusText);

            if (!isRunning && lastStatus.HasValue)
            {
                EditorGUILayout.LabelField(
                    $"Passed {lastPassCount} / Failed {lastFailCount} / Skipped {lastSkipCount} / Inconclusive {lastInconclusiveCount}");
                EditorGUILayout.LabelField($"Duration: {lastDurationSeconds:0.00}s");

                if (!string.IsNullOrWhiteSpace(lastResultFilePath))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.SelectableLabel(lastResultFilePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                        if (GUILayout.Button("Reveal", GUILayout.Width(72f)))
                        {
                            EditorUtility.RevealInFinder(lastResultFilePath);
                        }
                    }
                }
            }
        }

        private void DrawFailures()
        {
            EditorGUILayout.LabelField("Failures", EditorStyles.boldLabel);

            if (Failures.Count == 0)
            {
                EditorGUILayout.LabelField("No failures recorded.");
                return;
            }

            failureScroll = EditorGUILayout.BeginScrollView(failureScroll);
            foreach (string failure in Failures)
            {
                EditorGUILayout.HelpBox(failure, MessageType.Error);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPerformanceCapture()
        {
            EditorGUILayout.LabelField("Performance Snapshot", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Capture frame time and managed memory in Play Mode. Run once before optimization, set it as baseline, then run again after optimization to compare.",
                MessageType.Info);

            performanceLabel = EditorGUILayout.TextField("Label", performanceLabel);
            performanceWarmupSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Warmup Seconds", performanceWarmupSeconds));
            performanceCaptureSeconds = Mathf.Max(1f, EditorGUILayout.FloatField("Capture Seconds", performanceCaptureSeconds));

            bool isCapturing = FixPerformanceCaptureController.IsCapturing;
            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(isCapturing || !EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Capture In Play Mode", GUILayout.Height(28f)))
                    {
                        FixPerformanceCaptureController.BeginCapture(
                            performanceLabel,
                            performanceWarmupSeconds,
                            performanceCaptureSeconds,
                            false);
                    }
                }

                using (new EditorGUI.DisabledScope(isCapturing || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("Play And Capture", GUILayout.Height(28f)))
                    {
                        FixPerformanceCaptureController.RequestPlayAndCapture(
                            performanceLabel,
                            performanceWarmupSeconds,
                            performanceCaptureSeconds);
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!isCapturing))
                {
                    if (GUILayout.Button("Cancel Capture"))
                    {
                        FixPerformanceCaptureController.CancelCapture();
                    }
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(FixPerformanceCaptureController.LastSnapshotPath)))
                {
                    if (GUILayout.Button("Set Last As Baseline"))
                    {
                        FixPerformanceCaptureController.SetLastAsBaseline();
                    }
                }
            }

            if (isCapturing)
            {
                EditorGUILayout.LabelField($"Capturing: {FixPerformanceCaptureController.ProgressPercent:0}%");
            }

            DrawPerformanceSnapshot("Last", FixPerformanceCaptureController.LastSnapshotPath);
            DrawPerformanceSnapshot("Baseline", FixPerformanceCaptureController.BaselineSnapshotPath);
            DrawPerformanceComparison();
        }

        private static void DrawPerformanceSnapshot(string label, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorGUILayout.LabelField($"{label}: none");
                return;
            }

            FixPerformanceSnapshot snapshot = FixPerformanceCaptureController.LoadSnapshot(path);
            if (snapshot == null)
            {
                EditorGUILayout.LabelField($"{label}: {path}");
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{label}: {snapshot.label} | avg {snapshot.averageFrameMs:0.00} ms | p95 {snapshot.p95FrameMs:0.00} ms | max {snapshot.maxFrameMs:0.00} ms | mem {snapshot.memoryDeltaMb:0.00} MB",
                    GUILayout.MinWidth(360f));

                if (GUILayout.Button("Reveal", GUILayout.Width(72f)))
                {
                    EditorUtility.RevealInFinder(path);
                }
            }
        }

        private static void DrawPerformanceComparison()
        {
            FixPerformanceSnapshot baseline = FixPerformanceCaptureController.LoadSnapshot(
                FixPerformanceCaptureController.BaselineSnapshotPath);
            FixPerformanceSnapshot last = FixPerformanceCaptureController.LoadSnapshot(
                FixPerformanceCaptureController.LastSnapshotPath);

            if (baseline == null || last == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Compare Last - Baseline", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Average frame: {FormatSigned(last.averageFrameMs - baseline.averageFrameMs)} ms");
            EditorGUILayout.LabelField($"P95 frame: {FormatSigned(last.p95FrameMs - baseline.p95FrameMs)} ms");
            EditorGUILayout.LabelField($"Max frame: {FormatSigned(last.maxFrameMs - baseline.maxFrameMs)} ms");
            EditorGUILayout.LabelField($"Memory delta: {FormatSigned(last.memoryDeltaMb - baseline.memoryDeltaMb)} MB");
        }

        private static string FormatSigned(double value)
        {
            return value >= 0d ? $"+{value:0.00}" : value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void RunSingle(TestMode testMode)
        {
            string label = testMode == TestMode.EditMode ? "EditMode" : "PlayMode";
            Run(label, new Filter { testMode = testMode });
        }

        private void RunAll()
        {
            Run(
                "EditMode + PlayMode",
                new Filter { testMode = TestMode.EditMode },
                new Filter { testMode = TestMode.PlayMode });
        }

        private void Run(string label, params Filter[] filters)
        {
            if (isRunning)
            {
                Debug.LogWarning("Fix Test Runner is already running.");
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(WindowTitle, "Stop Play Mode before starting a test run.", "OK");
                return;
            }

            EnsureApi();
            ResetRunState(label);

            ExecutionSettings settings = new ExecutionSettings(filters);
            currentRunGuid = testRunnerApi.Execute(settings);
            Debug.Log($"Fix Test Runner started: {label} ({currentRunGuid})");
            Repaint();
        }

        private static void EnsureApi()
        {
            if (testRunnerApi == null)
            {
                testRunnerApi = CreateInstance<TestRunnerApi>();
            }

            if (callbacks != null)
            {
                testRunnerApi.UnregisterCallbacks(callbacks);
            }

            callbacks = new FixTestRunnerCallbacks();
            testRunnerApi.RegisterCallbacks(callbacks);
        }

        private static void ResetRunState(string label)
        {
            isRunning = true;
            currentRunGuid = string.Empty;
            currentRunLabel = label;
            lastStatus = null;
            lastPassCount = 0;
            lastFailCount = 0;
            lastSkipCount = 0;
            lastInconclusiveCount = 0;
            lastDurationSeconds = 0d;
            lastResultFilePath = string.Empty;
            Failures.Clear();
        }

        private static string SaveResult(ITestResultAdaptor result)
        {
            Directory.CreateDirectory(ResultsDirectory);

            string safeLabel = currentRunLabel
                .Replace(" ", string.Empty)
                .Replace("+", "And");
            string fileName = $"FixTestRunner_{safeLabel}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
            string relativePath = Path.Combine(ResultsDirectory, fileName);

            TestRunnerApi.SaveResultToFile(result, relativePath);
            return Path.GetFullPath(relativePath);
        }

        private static void RecordFailure(ITestResultAdaptor result)
        {
            if (result.TestStatus != ApiTestStatus.Failed || result.Test.IsSuite)
            {
                return;
            }

            if (Failures.Count >= MaxFailureRows)
            {
                return;
            }

            string message = string.IsNullOrWhiteSpace(result.Message)
                ? result.FullName
                : $"{result.FullName}\n{result.Message}";
            Failures.Add(message);
        }

        private static void RepaintOpenWindows()
        {
            foreach (FixTestRunnerWindow window in Resources.FindObjectsOfTypeAll<FixTestRunnerWindow>())
            {
                window.Repaint();
            }
        }

        private sealed class FixTestRunnerCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"Fix Test Runner running {testsToRun.TestCaseCount} tests: {currentRunLabel}");
                RepaintOpenWindows();
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                isRunning = false;
                lastStatus = result.TestStatus;
                lastPassCount = result.PassCount;
                lastFailCount = result.FailCount;
                lastSkipCount = result.SkipCount;
                lastInconclusiveCount = result.InconclusiveCount;
                lastDurationSeconds = result.Duration;
                lastResultFilePath = SaveResult(result);

                string summary =
                    $"Fix Test Runner finished: {currentRunLabel} => {result.TestStatus}. " +
                    $"Passed {result.PassCount}, Failed {result.FailCount}, Skipped {result.SkipCount}, " +
                    $"Inconclusive {result.InconclusiveCount}. Results: {lastResultFilePath}";

                if (result.TestStatus == ApiTestStatus.Failed)
                {
                    Debug.LogError(summary);
                }
                else
                {
                    Debug.Log(summary);
                }

                testRunnerApi.UnregisterCallbacks(this);
                RepaintOpenWindows();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                RecordFailure(result);
                RepaintOpenWindows();
            }
        }
    }

    [Serializable]
    internal sealed class FixPerformanceSnapshot
    {
        public string label;
        public string sceneName;
        public string scenePath;
        public string timestamp;
        public float warmupSeconds;
        public float captureSeconds;
        public int frameCount;
        public int activeGameObjectCount;
        public double averageFps;
        public double minFrameMs;
        public double averageFrameMs;
        public double p95FrameMs;
        public double maxFrameMs;
        public double memoryStartMb;
        public double memoryEndMb;
        public double memoryDeltaMb;
    }

    [InitializeOnLoad]
    internal static class FixPerformanceCaptureController
    {
        private const string PerformanceDirectory = "TestResults/Performance";
        private const string LastSnapshotPathKey = "CaseStudy.FixPerformance.LastSnapshotPath";
        private const string BaselineSnapshotPathKey = "CaseStudy.FixPerformance.BaselineSnapshotPath";
        private const string PendingPlayCaptureKey = "CaseStudy.FixPerformance.PendingPlayCapture";
        private const string PendingLabelKey = "CaseStudy.FixPerformance.PendingLabel";
        private const string PendingWarmupKey = "CaseStudy.FixPerformance.PendingWarmup";
        private const string PendingDurationKey = "CaseStudy.FixPerformance.PendingDuration";

        private static readonly List<double> FrameTimesMs = new List<double>(4096);

        private static string captureLabel;
        private static float warmupSeconds;
        private static float captureSeconds;
        private static bool autoExitPlayMode;
        private static bool isCapturing;
        private static bool isRecordingFrames;
        private static double startTime;
        private static double captureStartTime;
        private static long memoryStartBytes;
        private static long memoryEndBytes;

        static FixPerformanceCaptureController()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (EditorApplication.isPlaying && SessionState.GetBool(PendingPlayCaptureKey, false))
            {
                EditorApplication.delayCall += BeginPendingPlayCapture;
            }
        }

        internal static bool IsCapturing => isCapturing;

        internal static float ProgressPercent
        {
            get
            {
                if (!isCapturing)
                {
                    return 0f;
                }

                double total = Math.Max(0.001d, warmupSeconds + captureSeconds);
                double elapsed = Math.Max(0d, CurrentTime - startTime);
                return Mathf.Clamp01((float)(elapsed / total)) * 100f;
            }
        }

        internal static string LastSnapshotPath => EditorPrefs.GetString(LastSnapshotPathKey, string.Empty);

        internal static string BaselineSnapshotPath => EditorPrefs.GetString(BaselineSnapshotPathKey, string.Empty);

        private static double CurrentTime => Time.realtimeSinceStartup;

        internal static void RequestPlayAndCapture(string label, float warmup, float duration)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SessionState.SetBool(PendingPlayCaptureKey, true);
            SessionState.SetString(PendingLabelKey, SanitizeLabel(label));
            SessionState.SetString(PendingWarmupKey, warmup.ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(PendingDurationKey, duration.ToString(CultureInfo.InvariantCulture));
            EditorApplication.isPlaying = true;
        }

        internal static void BeginCapture(string label, float warmup, float duration, bool exitPlayModeWhenDone)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Performance Snapshot", "Enter Play Mode before capturing performance.", "OK");
                return;
            }

            captureLabel = SanitizeLabel(label);
            warmupSeconds = Mathf.Max(0f, warmup);
            captureSeconds = Mathf.Max(1f, duration);
            autoExitPlayMode = exitPlayModeWhenDone;
            isCapturing = true;
            isRecordingFrames = false;
            startTime = CurrentTime;
            captureStartTime = 0d;
            memoryStartBytes = 0L;
            memoryEndBytes = 0L;
            FrameTimesMs.Clear();

            Debug.Log($"Performance capture started: {captureLabel}, warmup {warmupSeconds:0.00}s, capture {captureSeconds:0.00}s.");
            RepaintOpenWindows();
        }

        internal static void CancelCapture()
        {
            if (!isCapturing)
            {
                return;
            }

            isCapturing = false;
            isRecordingFrames = false;
            autoExitPlayMode = false;
            FrameTimesMs.Clear();
            Debug.Log("Performance capture canceled.");
            RepaintOpenWindows();
        }

        internal static void SetLastAsBaseline()
        {
            if (string.IsNullOrWhiteSpace(LastSnapshotPath) || !File.Exists(LastSnapshotPath))
            {
                return;
            }

            EditorPrefs.SetString(BaselineSnapshotPathKey, LastSnapshotPath);
            Debug.Log($"Performance baseline set: {LastSnapshotPath}");
            RepaintOpenWindows();
        }

        internal static FixPerformanceSnapshot LoadSnapshot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<FixPerformanceSnapshot>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read performance snapshot: {path}\n{exception.Message}");
                return null;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingPlayCaptureKey, false))
            {
                BeginPendingPlayCapture();
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode && isCapturing)
            {
                CancelCapture();
            }
        }

        private static void BeginPendingPlayCapture()
        {
            if (!SessionState.GetBool(PendingPlayCaptureKey, false))
            {
                return;
            }

            SessionState.EraseBool(PendingPlayCaptureKey);
            string label = SessionState.GetString(PendingLabelKey, "FixPerformance");
            float warmup = ReadSessionFloat(PendingWarmupKey, 2f);
            float duration = ReadSessionFloat(PendingDurationKey, 15f);

            BeginCapture(label, warmup, duration, true);
        }

        private static float ReadSessionFloat(string key, float fallback)
        {
            string value = SessionState.GetString(key, string.Empty);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;
        }

        private static void Update()
        {
            if (!isCapturing)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                CancelCapture();
                return;
            }

            double now = CurrentTime;
            if (!isRecordingFrames)
            {
                if (now - startTime < warmupSeconds)
                {
                    RepaintOpenWindows();
                    return;
                }

                isRecordingFrames = true;
                captureStartTime = now;
                memoryStartBytes = GC.GetTotalMemory(false);
                memoryEndBytes = memoryStartBytes;
            }

            double frameMs = Time.unscaledDeltaTime * 1000d;
            if (frameMs > 0d)
            {
                FrameTimesMs.Add(frameMs);
            }

            memoryEndBytes = GC.GetTotalMemory(false);

            if (now - captureStartTime >= captureSeconds)
            {
                FinishCapture();
            }
            else
            {
                RepaintOpenWindows();
            }
        }

        private static void FinishCapture()
        {
            FixPerformanceSnapshot snapshot = BuildSnapshot();
            string path = SaveSnapshot(snapshot);
            EditorPrefs.SetString(LastSnapshotPathKey, path);

            isCapturing = false;
            isRecordingFrames = false;
            FrameTimesMs.Clear();

            Debug.Log(
                $"Performance capture finished: {path}. " +
                $"Average {snapshot.averageFrameMs:0.00} ms, p95 {snapshot.p95FrameMs:0.00} ms, max {snapshot.maxFrameMs:0.00} ms, memory delta {snapshot.memoryDeltaMb:0.00} MB.");

            bool shouldExit = autoExitPlayMode;
            autoExitPlayMode = false;
            RepaintOpenWindows();

            if (shouldExit)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static FixPerformanceSnapshot BuildSnapshot()
        {
            List<double> sorted = new List<double>(FrameTimesMs);
            sorted.Sort();

            Scene scene = SceneManager.GetActiveScene();
            double averageFrameMs = Average(FrameTimesMs);

            return new FixPerformanceSnapshot
            {
                label = captureLabel,
                sceneName = scene.name,
                scenePath = scene.path,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                warmupSeconds = warmupSeconds,
                captureSeconds = captureSeconds,
                frameCount = FrameTimesMs.Count,
                activeGameObjectCount = CountActiveGameObjects(),
                averageFps = averageFrameMs > 0d ? 1000d / averageFrameMs : 0d,
                minFrameMs = sorted.Count > 0 ? sorted[0] : 0d,
                averageFrameMs = averageFrameMs,
                p95FrameMs = Percentile(sorted, 0.95d),
                maxFrameMs = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0d,
                memoryStartMb = BytesToMb(memoryStartBytes),
                memoryEndMb = BytesToMb(memoryEndBytes),
                memoryDeltaMb = BytesToMb(memoryEndBytes - memoryStartBytes)
            };
        }

        private static string SaveSnapshot(FixPerformanceSnapshot snapshot)
        {
            Directory.CreateDirectory(PerformanceDirectory);
            string fileName = $"{SanitizeLabel(snapshot.label)}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string relativePath = Path.Combine(PerformanceDirectory, fileName);
            File.WriteAllText(relativePath, JsonUtility.ToJson(snapshot, true));
            return Path.GetFullPath(relativePath);
        }

        private static int CountActiveGameObjects()
        {
            return UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
        }

        private static double Average(List<double> values)
        {
            if (values.Count == 0)
            {
                return 0d;
            }

            double total = 0d;
            for (int i = 0; i < values.Count; i++)
            {
                total += values[i];
            }

            return total / values.Count;
        }

        private static double Percentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0d;
            }

            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(percentile * sortedValues.Count)) - 1,
                0,
                sortedValues.Count - 1);
            return sortedValues[index];
        }

        private static double BytesToMb(long bytes)
        {
            return bytes / 1024d / 1024d;
        }

        private static string SanitizeLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "FixPerformance";
            }

            char[] chars = label.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static void RepaintOpenWindows()
        {
            foreach (FixTestRunnerWindow window in Resources.FindObjectsOfTypeAll<FixTestRunnerWindow>())
            {
                window.Repaint();
            }
        }
    }

    internal static class FixTestRunnerToolbar
    {
        internal const string ToolbarElementPath = "CaseStudy/Fix Test Runner";

        [MainToolbarElementAttribute(
            ToolbarElementPath,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = 2)]
        public static MainToolbarElement CreateFixTestRunnerButton()
        {
            return new MainToolbarButton(
                new MainToolbarContent("Fix Tests", "Open the Fix Test Runner window."),
                () => FixTestRunnerWindow.OpenWindow());
        }
    }
}
