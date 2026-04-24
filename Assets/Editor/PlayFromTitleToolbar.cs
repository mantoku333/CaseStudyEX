using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine.SceneManagement;

namespace CaseStudy.EditorTools
{
    [InitializeOnLoad]
    internal static class PlayFromTitleToolbar
    {
        internal const string ToolbarElementPath = "CaseStudy/Play From Title";

        private const string TitleScenePath = "Assets/Scenes/Title.unity";
        private const string PreviousScenePathKey = "CaseStudy.PlayFromTitle.PreviousScenePath";
        private const string RestoreSceneKey = "CaseStudy.PlayFromTitle.RestoreScene";

        internal static bool HasTitleScene => File.Exists(TitleScenePath);

        static PlayFromTitleToolbar()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MainToolbarElementAttribute(
            ToolbarElementPath,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = 1)]
        public static MainToolbarElement CreatePlayFromTitleButton()
        {
            MainToolbarButton button = new MainToolbarButton(
                new MainToolbarContent("Title Play", "Open the title scene and enter Play Mode."),
                PlayFromTitle);

            button.enabled = HasTitleScene;
            return button;
        }

        [MenuItem("Tools/Play/Play From Title")]
        private static void PlayFromTitleMenu()
        {
            PlayFromTitle();
        }

        internal static void PlayFromTitle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!HasTitleScene)
            {
                EditorUtility.DisplayDialog(
                    "Play From Title",
                    $"Title scene was not found.\nPath: {TitleScenePath}",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string activeScenePath = activeScene.path;
            bool canRestoreScene =
                !string.IsNullOrWhiteSpace(activeScenePath) &&
                !string.Equals(activeScenePath, TitleScenePath, StringComparison.OrdinalIgnoreCase);

            if (canRestoreScene)
            {
                SessionState.SetString(PreviousScenePathKey, activeScenePath);
                SessionState.SetBool(RestoreSceneKey, true);
            }
            else
            {
                ClearRestoreState();
            }

            if (!string.Equals(activeScenePath, TitleScenePath, StringComparison.OrdinalIgnoreCase))
            {
                EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            }

            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            if (!SessionState.GetBool(RestoreSceneKey, false))
            {
                return;
            }

            string previousScenePath = SessionState.GetString(PreviousScenePathKey, string.Empty);
            ClearRestoreState();

            if (string.IsNullOrWhiteSpace(previousScenePath) || !File.Exists(previousScenePath))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        private static void ClearRestoreState()
        {
            SessionState.EraseString(PreviousScenePathKey);
            SessionState.EraseBool(RestoreSceneKey);
        }
    }
}
