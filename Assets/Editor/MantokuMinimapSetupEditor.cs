using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MantokuMinimapSetupEditor
{
    private static readonly Regex TriggerNamePattern = new Regex(@"^Col_(\d+)-(\d+)$", RegexOptions.Compiled);

    [MenuItem("Tools/Minimap/Setup Story_Mantoku Rooms")]
    private static void SetupStoryMantokuRooms()
    {
        RoomCameraTrigger[] triggers = Object.FindObjectsByType<RoomCameraTrigger>(FindObjectsSortMode.None);
        if (triggers == null || triggers.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Minimap Setup",
                "No RoomCameraTrigger was found. Open the Story_Mantoku scene and run the tool again.",
                "OK");
            return;
        }

        int attachedCount = 0;

        for (int i = 0; i < triggers.Length; i++)
        {
            RoomCameraTrigger trigger = triggers[i];
            if (trigger == null)
            {
                continue;
            }

            MinimapRoom room = trigger.GetComponent<MinimapRoom>();
            if (room == null)
            {
                room = Undo.AddComponent<MinimapRoom>(trigger.gameObject);
                attachedCount++;
            }

            Undo.RecordObject(room, "Setup Mantoku Minimap Room");
            room.ConfigureAuthoringFields(
                trigger.gameObject.name,
                trigger.gameObject.name,
                GuessInitialMapPosition(trigger.gameObject.name),
                Vector2Int.one);
            EditorUtility.SetDirty(room);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog(
            "Minimap Setup",
            $"Checked {triggers.Length} room triggers. Added MinimapRoom to {attachedCount} objects.\n\nNext step: adjust Map Layout X / Y in the Inspector.",
            "OK");
    }

    // If a trigger is named like Col_2-3, use that as a readable first guess.
    // Planners can fine tune the final map shape in the Inspector afterwards.
    private static Vector2Int GuessInitialMapPosition(string triggerObjectName)
    {
        if (string.IsNullOrWhiteSpace(triggerObjectName))
        {
            return Vector2Int.zero;
        }

        Match match = TriggerNamePattern.Match(triggerObjectName);
        if (!match.Success)
        {
            return Vector2Int.zero;
        }

        int row = int.Parse(match.Groups[1].Value);
        int column = int.Parse(match.Groups[2].Value);
        return new Vector2Int(column - 1, -(row - 1));
    }
}
