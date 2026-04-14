using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CaseStudy.Editor.CI
{
    public static class JenkinsBuild
    {
        private const string DefaultBuildPath = "Build/Windows/CaseStudy.exe";

        public static void PerformWindows64Build()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "No enabled scenes were found in EditorBuildSettings. Enable at least one scene before building.");
            }

            var outputPath = Environment.GetEnvironmentVariable("UNITY_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = DefaultBuildPath;
            }

            var fullOutputPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(fullOutputPath);

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException($"Could not determine output directory from path: {fullOutputPath}");
            }

            Directory.CreateDirectory(outputDirectory);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = fullOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            };

            Debug.Log($"Starting Jenkins build. Output path: {fullOutputPath}");
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            Debug.Log($"Build result: {summary.result}");
            Debug.Log($"Output path: {summary.outputPath}");
            Debug.Log($"Total size: {summary.totalSize} bytes");
            Debug.Log($"Total time: {summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Jenkins build failed with result: {summary.result}");
            }
        }
    }
}
