using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace CaseStudy.EditorTools
{
    internal static class WorkspaceLinksToolbar
    {
        internal const string ToolbarElementPath = "CaseStudy/Workspace Links";

        [MainToolbarElementAttribute(
            ToolbarElementPath,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = 0)]
        public static IEnumerable<MainToolbarElement> CreateWorkspaceLinks()
        {
            yield return CreateServiceButton("GitHub", () => WorkspaceLinksPreferences.GitHubUrl, () => WorkspaceLinksPreferences.GitHubIcon);
            yield return CreateServiceButton("Jira", () => WorkspaceLinksPreferences.JiraUrl, () => WorkspaceLinksPreferences.JiraIcon);
            yield return CreateServiceButton("Confluence", () => WorkspaceLinksPreferences.ConfluenceUrl, () => WorkspaceLinksPreferences.ConfluenceIcon);
            yield return CreateServiceButton("GSheet", () => WorkspaceLinksPreferences.SpreadsheetUrl, () => WorkspaceLinksPreferences.SpreadsheetIcon);
        }

        private static MainToolbarElement CreateServiceButton(string label, Func<string> urlProvider, Func<Texture2D> iconProvider)
        {
            Texture2D icon = iconProvider();
            MainToolbarContent content = icon != null
                ? new MainToolbarContent(string.Empty, icon, $"Open {label}")
                : new MainToolbarContent(label, $"Open {label}");

            string url = urlProvider();
            return new MainToolbarButton(content, () => OpenUrl(urlProvider(), label))
            {
                enabled = !string.IsNullOrWhiteSpace(url)
            };
        }

        private static void OpenUrl(string url, string serviceName)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                WorkspaceLinksSettingsWindow.ShowUrlNotSetDialog(serviceName);
                return;
            }

            Application.OpenURL(url);
        }
    }

    internal static class WorkspaceLinksPreferences
    {
        private const string Prefix = "CaseStudy.WorkspaceLinks.";
        private const string GitHubKey = Prefix + "GitHub";
        private const string JiraKey = Prefix + "Jira";
        private const string ConfluenceKey = Prefix + "Confluence";
        private const string SpreadsheetKey = Prefix + "Spreadsheet";
        private const string GitHubIconKey = Prefix + "GitHubIcon";
        private const string JiraIconKey = Prefix + "JiraIcon";
        private const string ConfluenceIconKey = Prefix + "ConfluenceIcon";
        private const string SpreadsheetIconKey = Prefix + "SpreadsheetIcon";

        private const string DefaultGitHubUrl = "https://github.com/mantoku333/CaseStudyEX";
        private const string DefaultJiraUrl = "https://casestudyfuyunoteam.atlassian.net/jira/software/projects/CASE/boards/1?jql=assignee%20%3D%20712020%3A20cd4f38-0f88-4b43-9011-7b4fc20a8bb5";
        private const string DefaultConfluenceUrl = "https://casestudyfuyunoteam.atlassian.net/wiki/spaces/fyG7TzdUAMK6/overview";
        private const string DefaultSpreadsheetUrl = "https://docs.google.com/spreadsheets/d/1BtmtvViI2GR-6N1-A7bUL7RVvXtu1JI6eQ25IuvZMAo/edit?gid=0#gid=0";

        internal static string GitHubUrl
        {
            get => GetStringOrDefault(GitHubKey, DefaultGitHubUrl);
            set => EditorPrefs.SetString(GitHubKey, value?.Trim() ?? string.Empty);
        }

        internal static string JiraUrl
        {
            get => GetStringOrDefault(JiraKey, DefaultJiraUrl);
            set => EditorPrefs.SetString(JiraKey, value?.Trim() ?? string.Empty);
        }

        internal static string ConfluenceUrl
        {
            get => GetStringOrDefault(ConfluenceKey, DefaultConfluenceUrl);
            set => EditorPrefs.SetString(ConfluenceKey, value?.Trim() ?? string.Empty);
        }

        internal static string SpreadsheetUrl
        {
            get => GetStringOrDefault(SpreadsheetKey, DefaultSpreadsheetUrl);
            set => EditorPrefs.SetString(SpreadsheetKey, value?.Trim() ?? string.Empty);
        }

        internal static Texture2D GitHubIcon
        {
            get => GetIcon(GitHubIconKey);
            set => SetIcon(GitHubIconKey, value);
        }

        internal static Texture2D JiraIcon
        {
            get => GetIcon(JiraIconKey);
            set => SetIcon(JiraIconKey, value);
        }

        internal static Texture2D ConfluenceIcon
        {
            get => GetIcon(ConfluenceIconKey);
            set => SetIcon(ConfluenceIconKey, value);
        }

        internal static Texture2D SpreadsheetIcon
        {
            get => GetIcon(SpreadsheetIconKey);
            set => SetIcon(SpreadsheetIconKey, value);
        }

        private static string GetStringOrDefault(string key, string defaultValue)
        {
            string value = EditorPrefs.GetString(key, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static Texture2D GetIcon(string key)
        {
            string guid = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void SetIcon(string key, Texture2D icon)
        {
            if (icon == null)
            {
                EditorPrefs.SetString(key, string.Empty);
                return;
            }

            string path = AssetDatabase.GetAssetPath(icon);
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorPrefs.SetString(key, string.Empty);
                return;
            }

            EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(path));
        }
    }

    internal sealed class WorkspaceLinksSettingsWindow : EditorWindow
    {
        private string githubUrl;
        private string jiraUrl;
        private string confluenceUrl;
        private string spreadsheetUrl;
        private Texture2D githubIcon;
        private Texture2D jiraIcon;
        private Texture2D confluenceIcon;
        private Texture2D spreadsheetIcon;

        [MenuItem("Tools/Workspace Links/Settings")]
        private static void OpenWindow()
        {
            OpenWindowFromMenu();
        }

        internal static void OpenWindowFromMenu()
        {
            WorkspaceLinksSettingsWindow window = GetWindow<WorkspaceLinksSettingsWindow>("Workspace Links");
            window.minSize = new Vector2(480f, 320f);
            window.Show();
        }

        [MenuItem("Tools/Workspace Links/Open GitHub")]
        private static void OpenGitHub() => OpenConfiguredUrl(WorkspaceLinksPreferences.GitHubUrl, "GitHub");

        [MenuItem("Tools/Workspace Links/Open Jira")]
        private static void OpenJira() => OpenConfiguredUrl(WorkspaceLinksPreferences.JiraUrl, "Jira");

        [MenuItem("Tools/Workspace Links/Open Confluence")]
        private static void OpenConfluence() => OpenConfiguredUrl(WorkspaceLinksPreferences.ConfluenceUrl, "Confluence");

        [MenuItem("Tools/Workspace Links/Open Spreadsheet")]
        private static void OpenSpreadsheet() => OpenConfiguredUrl(WorkspaceLinksPreferences.SpreadsheetUrl, "Spreadsheet");

        [MenuItem("Tools/Workspace Links/Open GitHub", true)]
        [MenuItem("Tools/Workspace Links/Open Jira", true)]
        [MenuItem("Tools/Workspace Links/Open Confluence", true)]
        [MenuItem("Tools/Workspace Links/Open Spreadsheet", true)]
        private static bool ValidateOpenMenuItems() => true;

        private static void OpenConfiguredUrl(string url, string serviceName)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                ShowUrlNotSetDialog(serviceName);
                return;
            }

            Application.OpenURL(url);
        }

        internal static void ShowUrlNotSetDialog(string serviceName)
        {
            EditorUtility.DisplayDialog(
                "Workspace Links",
                $"{serviceName} URL is not set.\nSet it from Tools/Workspace Links/Settings.",
                "OK");
        }

        private void OnEnable()
        {
            LoadFromPrefs();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Set URLs and optional icons for top-toolbar link buttons.",
                MessageType.Info);

            EditorGUILayout.Space(4f);

            githubUrl = EditorGUILayout.TextField("GitHub URL", githubUrl);
            githubIcon = (Texture2D)EditorGUILayout.ObjectField("GitHub Icon", githubIcon, typeof(Texture2D), false);

            jiraUrl = EditorGUILayout.TextField("Jira URL", jiraUrl);
            jiraIcon = (Texture2D)EditorGUILayout.ObjectField("Jira Icon", jiraIcon, typeof(Texture2D), false);

            confluenceUrl = EditorGUILayout.TextField("Confluence URL", confluenceUrl);
            confluenceIcon = (Texture2D)EditorGUILayout.ObjectField("Confluence Icon", confluenceIcon, typeof(Texture2D), false);

            spreadsheetUrl = EditorGUILayout.TextField("Spreadsheet URL", spreadsheetUrl);
            spreadsheetIcon = (Texture2D)EditorGUILayout.ObjectField("Spreadsheet Icon", spreadsheetIcon, typeof(Texture2D), false);

            GUILayout.FlexibleSpace();

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload"))
                {
                    LoadFromPrefs();
                }

                if (GUILayout.Button("Save"))
                {
                    SaveToPrefs();
                }
            }
        }

        private void LoadFromPrefs()
        {
            githubUrl = WorkspaceLinksPreferences.GitHubUrl;
            jiraUrl = WorkspaceLinksPreferences.JiraUrl;
            confluenceUrl = WorkspaceLinksPreferences.ConfluenceUrl;
            spreadsheetUrl = WorkspaceLinksPreferences.SpreadsheetUrl;
            githubIcon = WorkspaceLinksPreferences.GitHubIcon;
            jiraIcon = WorkspaceLinksPreferences.JiraIcon;
            confluenceIcon = WorkspaceLinksPreferences.ConfluenceIcon;
            spreadsheetIcon = WorkspaceLinksPreferences.SpreadsheetIcon;
        }

        private void SaveToPrefs()
        {
            WorkspaceLinksPreferences.GitHubUrl = githubUrl;
            WorkspaceLinksPreferences.JiraUrl = jiraUrl;
            WorkspaceLinksPreferences.ConfluenceUrl = confluenceUrl;
            WorkspaceLinksPreferences.SpreadsheetUrl = spreadsheetUrl;
            WorkspaceLinksPreferences.GitHubIcon = githubIcon;
            WorkspaceLinksPreferences.JiraIcon = jiraIcon;
            WorkspaceLinksPreferences.ConfluenceIcon = confluenceIcon;
            WorkspaceLinksPreferences.SpreadsheetIcon = spreadsheetIcon;
            MainToolbar.Refresh(WorkspaceLinksToolbar.ToolbarElementPath);
        }
    }
}
