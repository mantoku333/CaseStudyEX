using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Joins a run of adjoining rooms onto one following camera, so the camera stops changing
/// between them.
///
/// Why this exists: between two static room cameras there is about 38 units of separation,
/// roughly 90% of a screen width. Any transition across that distance is wrong at every
/// duration - 2.4s drifts and nauseates, 0.35s whips, and a cut is too abrupt. The distance
/// is the problem, so the fix is to remove it: one camera, clamped to the whole run, following
/// the player. Crossing an internal seam then produces no transition whatsoever.
///
/// Menu: GameObject > Camera Area > Make Camera Run From Selection
/// </summary>
public static class CameraRunSetup
{
    private const string MenuPath = "GameObject/Camera Area/Make Camera Run From Selection";
    private const string BoundsPrefix = "CameraRunBounds_";
    private const float Aspect = RoomCameraBoundsUtility.DefaultAspect;

    [MenuItem(MenuPath, false, 21)]
    private static void MakeRun()
    {
        List<RoomCameraTrigger> rooms = CollectSelectedRooms();
        if (rooms.Count < 2)
        {
            EditorUtility.DisplayDialog(
                "Camera run",
                "RoomCameraTrigger を持つオブジェクトを2つ以上選んでください。\n" +
                "（例: Col_101 と Col_102 と Col_103）",
                "OK");
            return;
        }

        if (!TryGetUnionBounds(rooms, out Bounds union))
        {
            EditorUtility.DisplayDialog("Camera run", "選択した部屋のエリアコライダーが見つかりません。", "OK");
            return;
        }

        CinemachineCamera source = FindSourceCamera(rooms);
        if (source == null)
        {
            EditorUtility.DisplayDialog(
                "Camera run",
                "元にするカメラが見つかりません。選択した部屋のどれかに Room Camera か " +
                "Horizontal Follow Camera を割り当ててください。",
                "OK");
            return;
        }

        float ortho = source.Lens.OrthographicSize;
        Vector2 view = RoomCameraBoundsUtility.GetViewSize(ortho, Aspect);
        string runName = BuildRunName(rooms);

        var report = new StringBuilder();
        report.AppendLine($"連なり: {runName}");
        report.AppendLine($"部屋数: {rooms.Count}");
        report.AppendLine($"全体サイズ: {union.size.x:F1} x {union.size.y:F1}");
        report.AppendLine($"視野: {view.x:F1} x {view.y:F1}（ortho {ortho:F1}）");
        report.AppendLine();

        if (union.size.x <= view.x)
        {
            report.AppendLine(
                "注意: 連なり全体が画面幅に収まっています。横追従する余地がないので、" +
                "カメラは中央に固定されたままになります。");
        }

        bool verticalFits = union.size.y <= view.y;
        report.AppendLine(verticalFits
            ? "縦: 画面に収まるので Y は固定されます（廊下と同じ挙動）。"
            : $"縦: 連なりが画面より高い（{union.size.y:F1} > {view.y:F1}）ため、" +
              "Y が固定できません。上下に見切れます。連なりは同じ高さの部屋だけにしてください。");

        if (!EditorUtility.DisplayDialog(
                "Make camera run",
                report + "\nこの連なりを1台のカメラにまとめますか？",
                "まとめる",
                "キャンセル"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Make camera run");

        CinemachineCamera runCamera = CreateRunCamera(source, runName);
        Collider2D runBounds = CreateRunBounds(runCamera, runName, union, view, verticalFits);

        for (int i = 0; i < rooms.Count; i++)
        {
            ApplyRunToRoom(rooms[i], runCamera, runBounds);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkAllScenesDirty();

        string message =
            $"{rooms.Count} 部屋を 1 台のカメラ（{runCamera.name}）にまとめました。\n" +
            $"内部の継ぎ目 {rooms.Count - 1} 箇所でカメラが切り替わらなくなります。\n\n" +
            "元の部屋ごとのカメラは残してあります（Priority が上がらなくなるだけ）。";
        Debug.Log($"[CameraRunSetup] {message}", runCamera);
        EditorUtility.DisplayDialog("Camera run", message, "OK");
        Selection.activeGameObject = runCamera.gameObject;
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMakeRun()
    {
        return CollectSelectedRooms().Count >= 2;
    }

    private static List<RoomCameraTrigger> CollectSelectedRooms()
    {
        var rooms = new List<RoomCameraTrigger>();
        GameObject[] selection = Selection.gameObjects;
        for (int i = 0; i < selection.Length; i++)
        {
            var trigger = selection[i].GetComponentInChildren<RoomCameraTrigger>();
            if (trigger != null && !rooms.Contains(trigger))
            {
                rooms.Add(trigger);
            }
        }

        rooms.Sort((a, b) =>
        {
            a.TryGetAreaBounds(out Bounds ba);
            b.TryGetAreaBounds(out Bounds bb);
            return ba.center.x.CompareTo(bb.center.x);
        });
        return rooms;
    }

    private static bool TryGetUnionBounds(List<RoomCameraTrigger> rooms, out Bounds union)
    {
        union = default;
        bool any = false;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (!rooms[i].TryGetAreaBounds(out Bounds bounds))
            {
                continue;
            }

            if (!any)
            {
                union = bounds;
                any = true;
                continue;
            }

            union.Encapsulate(bounds);
        }

        return any;
    }

    private static CinemachineCamera FindSourceCamera(List<RoomCameraTrigger> rooms)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            var so = new SerializedObject(rooms[i]);
            if (so.FindProperty("_horizontalFollowCamera")?.objectReferenceValue is CinemachineCamera horizontal)
            {
                return horizontal;
            }

            if (so.FindProperty("_roomCamera")?.objectReferenceValue is CinemachineCamera room)
            {
                return room;
            }
        }

        return null;
    }

    private static string BuildRunName(List<RoomCameraTrigger> rooms)
    {
        string first = Strip(rooms[0].gameObject.name);
        string last = Strip(rooms[rooms.Count - 1].gameObject.name);
        return $"{first}_{last}";
    }

    private static string Strip(string name)
    {
        return name.StartsWith("Col_") ? name.Substring(4) : name;
    }

    private static CinemachineCamera CreateRunCamera(CinemachineCamera source, string runName)
    {
        GameObject copy = Object.Instantiate(
            source.gameObject,
            source.transform.position,
            source.transform.rotation,
            source.transform.parent);
        copy.name = $"CN_Run_{runName}";
        Undo.RegisterCreatedObjectUndo(copy, "Create run camera");

        for (int i = copy.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = copy.transform.GetChild(i);
            if (child.name.StartsWith(BoundsPrefix))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        var camera = copy.GetComponent<CinemachineCamera>();
        if (camera != null)
        {
            camera.Priority.Value = 0;
            camera.Priority.Enabled = true;
        }

        return camera;
    }

    private static Collider2D CreateRunBounds(
        CinemachineCamera camera,
        string runName,
        Bounds union,
        Vector2 view,
        bool verticalFits)
    {
        var host = new GameObject(BoundsPrefix + runName);
        Undo.RegisterCreatedObjectUndo(host, "Create run bounds");
        host.transform.SetParent(camera.transform.parent, false);
        host.transform.rotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;

        // The vertical size is what locks Y: when the run fits on screen the camera settles at
        // the shape's centre, which is the corridor behaviour that was wanted here.
        float sizeY = verticalFits ? view.y : union.size.y;
        host.transform.position = new Vector3(union.center.x, union.center.y, 0f);

        BoxCollider2D box = Undo.AddComponent<BoxCollider2D>(host);
        Undo.RecordObject(box, "Configure run bounds");
        box.isTrigger = true;
        box.enabled = false; // Shape only; it must never take part in physics.
        box.offset = Vector2.zero;
        box.size = new Vector2(union.size.x, sizeY);
        return box;
    }

    private static void ApplyRunToRoom(
        RoomCameraTrigger room,
        CinemachineCamera runCamera,
        Collider2D runBounds)
    {
        var so = new SerializedObject(room);
        SetBool(so, "_useHorizontalFollowCameraWhenEntered", true);
        SetBool(so, "_useDefaultCameraWhenEntered", false);
        SetBool(so, "_clampHorizontalFollowXToArea", true);
        SetBool(so, "_useManualYForHorizontalFollow", false);
        SetReference(so, "_horizontalFollowCamera", runCamera);
        SetReference(so, "_horizontalFollowClampBounds", runBounds);
        so.ApplyModifiedProperties();
    }

    private static void SetBool(SerializedObject so, string path, bool value)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetReference(SerializedObject so, string path, Object value)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }
}
