using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Converts the open scene from the <c>RoomCameraTrigger</c> + portal system to
/// <see cref="RoomCameraDirector"/> + <see cref="RoomCameraVolume"/> + Confiner2D.
///
/// Run on a scene you can throw away first. Everything is registered with Undo, but this
/// touches dozens of objects, so a scene copy is the safer workflow.
///
/// Menu: GameObject > Camera Area > Migrate Scene To Room Camera Director
/// </summary>
public static class RoomCameraDirectorMigration
{
    private const string MenuPath = "GameObject/Camera Area/Migrate Scene To Room Camera Director";
    private const string BoundsPrefix = "CameraBounds_";
    private const string FollowCameraName = "CN_FollowCam";
    private const float TargetAspect = RoomCameraBoundsUtility.DefaultAspect;

    // Cut, not a pan.
    //
    // A standard room here is 38.3 units wide and the view is 42.7, so neighbouring room
    // cameras sit about 38 units apart - roughly 90% of a screen width. Interpolating that
    // sweeps almost the entire image across the screen, and there is no duration that reads
    // well: 2.4s was a slow nauseating drift, and 0.25s is a 150 unit/second whip. The motion
    // itself is the problem, not its speed, so the transition is removed instead of retimed.
    private const CinemachineBlendDefinition.Styles RoomBlendStyle =
        CinemachineBlendDefinition.Styles.Cut;

    [MenuItem(MenuPath, false, 20)]
    private static void Migrate()
    {
        if (!EditorUtility.DisplayDialog(
                "Migrate camera system",
                "Converts every RoomCameraTrigger in the open scene to RoomCameraVolume, adds a " +
                "CinemachineConfiner2D to each room camera, and disables the old portal objects.\n\n" +
                "Run this on a disposable copy of the scene first.\n\nContinue?",
                "Migrate",
                "Cancel"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Migrate Room Camera System");

        var report = new StringBuilder();
        var warnings = new List<string>();

        try
        {
            RunMigration(report, warnings);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkAllScenesDirty();
        }

        if (warnings.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"要確認 ({warnings.Count}):");
            for (int i = 0; i < warnings.Count; i++)
            {
                report.AppendLine($"  - {warnings[i]}");
            }
        }

        Debug.Log($"[RoomCameraDirectorMigration]\n{report}");
        EditorUtility.DisplayDialog("Migration finished", report.ToString(), "OK");
    }

    private static void RunMigration(StringBuilder report, List<string> warnings)
    {
        RoomCameraTrigger[] triggers = Object.FindObjectsByType<RoomCameraTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (triggers.Length == 0)
        {
            report.AppendLine("RoomCameraTrigger が見つかりません。移行済みの可能性があります。");
            return;
        }

        CinemachineCamera followCamera = FindCameraByName(FollowCameraName);
        Transform trackingTarget = followCamera != null ? followCamera.Target.TrackingTarget : null;
        if (trackingTarget == null)
        {
            warnings.Add($"{FollowCameraName} の TrackingTarget が未設定のため、追従軸を持つ部屋は手動設定が必要です。");
        }

        // A camera can hold only one confiner shape, so rooms that share a camera each need
        // their own copy. Cinemachine's own documentation recommends exactly this over
        // swapping one confiner's bounding shape at runtime.
        Dictionary<CinemachineCamera, List<RoomCameraTrigger>> byCamera = GroupByCamera(triggers, followCamera);

        int volumes = 0;
        int duplicated = 0;
        int confiners = 0;
        var tooSmall = new List<string>();

        foreach (KeyValuePair<CinemachineCamera, List<RoomCameraTrigger>> pair in byCamera)
        {
            CinemachineCamera sourceCamera = pair.Key;
            List<RoomCameraTrigger> rooms = pair.Value;
            bool shared = rooms.Count > 1;
            bool isFollowCamera = sourceCamera == followCamera;

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomCameraTrigger trigger = rooms[i];
                string roomId = StripColliderPrefix(trigger.gameObject.name);

                // Decide whether this room gets its own copy of the camera.
                //   dedicated camera          -> use it as is
                //   shared by several rooms   -> first room keeps the original, the rest get copies
                //   CN_FollowCam              -> always a copy, never the original, because
                //                                CameraManager resolves it by name and story and
                //                                boss code depend on that object staying put
                CinemachineCamera roomCamera;
                if (isFollowCamera)
                {
                    roomCamera = DuplicateCamera(sourceCamera, $"CN_{roomId}_Room");
                    duplicated++;
                }
                else if (shared && i > 0)
                {
                    roomCamera = DuplicateCamera(sourceCamera, $"CN_{roomId}_Room");
                    duplicated++;
                }
                else
                {
                    roomCamera = sourceCamera;
                }

                if (roomCamera == null)
                {
                    warnings.Add($"{trigger.gameObject.name}: カメラを解決できませんでした。");
                    continue;
                }

                Bounds roomBounds = GetTriggerBounds(trigger);
                if (roomBounds.size.sqrMagnitude <= Mathf.Epsilon)
                {
                    warnings.Add($"{trigger.gameObject.name}: エリアコライダーが見つかりません。");
                    continue;
                }

                float ortho = roomCamera.Lens.OrthographicSize;
                if (ortho <= 0f)
                {
                    warnings.Add($"{trigger.gameObject.name}: OrthographicSize が 0 以下です。");
                    continue;
                }

                // Report rooms the camera cannot contain, which is where the player
                // currently walks off screen.
                if (!RoomCameraBoundsUtility.IsFullyPinned(roomBounds, ortho, TargetAspect))
                {
                    float needed = RoomCameraBoundsUtility.GetOrthographicSizeToFit(roomBounds, TargetAspect);
                    if (trackingTarget == null)
                    {
                        tooSmall.Add($"{roomId}: 部屋 {roomBounds.size.x:F1}x{roomBounds.size.y:F1} が視野より大きく、追従が必要 (収めるなら ortho {needed:F1})");
                    }
                }

                Collider2D boundsCollider = CreateOrUpdateBoundsCollider(roomCamera, roomId, roomBounds, ortho);
                ApplyConfiner(roomCamera, boundsCollider);
                confiners++;

                // Only cameras with a following axis need a tracking target. A fully pinned
                // camera is held at the shape centre regardless, so leaving its target alone
                // avoids disturbing authored setups.
                bool fullyPinned = RoomCameraBoundsUtility.IsFullyPinned(roomBounds, ortho, TargetAspect);
                if (!fullyPinned)
                {
                    if (roomCamera.Target.TrackingTarget == null && trackingTarget != null)
                    {
                        CameraTarget target = roomCamera.Target;
                        target.TrackingTarget = trackingTarget;
                        roomCamera.Target = target;
                    }

                    EnsureBodyComponent(roomCamera);
                    ApplyFollowDamping(roomCamera, trigger, roomBounds, ortho);
                }

                CreateVolume(trigger, roomCamera, boundsCollider);
                volumes++;
            }
        }

        EnsureDirector(followCamera);
        int portals = DisableLegacyPortals();
        int retained = CountRetainedTriggers(triggers);
        ApplyBrainBlend(report);

        report.AppendLine($"RoomCameraVolume 作成: {volumes}");
        report.AppendLine($"カメラ複製 (共有解消): {duplicated}");
        report.AppendLine($"Confiner2D 設定: {confiners}");
        report.AppendLine($"旧ポータル無効化: {portals}");
        report.AppendLine($"RoomCameraTrigger 保持: {retained}（Fog / 敵活動 / ストーリーが部屋IDとして使用。カメラ制御のみ無効化）");

        if (tooSmall.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("追従軸を持つ部屋 (Confiner がプレイヤー追従に切り替わります):");
            for (int i = 0; i < tooSmall.Count; i++)
            {
                report.AppendLine($"  - {tooSmall[i]}");
            }
        }
    }

    private static Dictionary<CinemachineCamera, List<RoomCameraTrigger>> GroupByCamera(
        RoomCameraTrigger[] triggers,
        CinemachineCamera followCamera)
    {
        var map = new Dictionary<CinemachineCamera, List<RoomCameraTrigger>>();
        for (int i = 0; i < triggers.Length; i++)
        {
            RoomCameraTrigger trigger = triggers[i];
            CinemachineCamera camera = ResolveTriggerCamera(trigger, followCamera);
            if (camera == null)
            {
                continue;
            }

            if (!map.TryGetValue(camera, out List<RoomCameraTrigger> list))
            {
                list = new List<RoomCameraTrigger>();
                map[camera] = list;
            }

            list.Add(trigger);
        }

        return map;
    }

    private static CinemachineCamera ResolveTriggerCamera(RoomCameraTrigger trigger, CinemachineCamera followCamera)
    {
        var so = new SerializedObject(trigger);
        var room = so.FindProperty("_roomCamera");
        var horizontal = so.FindProperty("_horizontalFollowCamera");

        if (room != null && room.objectReferenceValue is CinemachineCamera roomCamera)
        {
            return roomCamera;
        }

        if (horizontal != null && horizontal.objectReferenceValue is CinemachineCamera horizontalCamera)
        {
            return horizontalCamera;
        }

        return followCamera;
    }

    private static Bounds GetTriggerBounds(RoomCameraTrigger trigger)
    {
        if (trigger.TryGetAreaBounds(out Bounds bounds))
        {
            return bounds;
        }

        return new Bounds(trigger.transform.position, Vector3.zero);
    }

    private static CinemachineCamera DuplicateCamera(CinemachineCamera source, string newName)
    {
        GameObject copy = Object.Instantiate(
            source.gameObject,
            source.transform.position,
            source.transform.rotation,
            source.transform.parent);
        copy.name = newName;
        Undo.RegisterCreatedObjectUndo(copy, "Duplicate room camera");

        // Remove a confiner inherited from the source: a fresh one is configured per room.
        var inherited = copy.GetComponent<CinemachineConfiner2D>();
        if (inherited != null)
        {
            Object.DestroyImmediate(inherited);
        }

        // Children of the source camera (e.g. a previous bounds collider) are not wanted.
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

    private static Collider2D CreateOrUpdateBoundsCollider(
        CinemachineCamera camera,
        string roomId,
        Bounds roomBounds,
        float orthographicSize)
    {
        Bounds confiner = RoomCameraBoundsUtility.Build(
            roomBounds,
            camera.transform.position,
            orthographicSize,
            TargetAspect);

        string objectName = BoundsPrefix + roomId;
        Transform existing = camera.transform.Find(objectName);
        GameObject host;
        if (existing != null)
        {
            host = existing.gameObject;
        }
        else
        {
            host = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(host, "Create camera bounds");
            host.transform.SetParent(camera.transform, false);
        }

        // Keep the shape axis-aligned and unscaled: the confiner cache survives translation
        // and uniform scale, but a non-uniform scale or rotation forces an expensive re-bake.
        host.transform.rotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;
        host.transform.position = new Vector3(confiner.center.x, confiner.center.y, camera.transform.position.z);

        var box = host.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider2D>(host);
        }

        Undo.RecordObject(box, "Configure camera bounds");
        box.isTrigger = true;
        box.enabled = false; // Shape only. It must never take part in physics.
        box.offset = Vector2.zero;
        box.size = new Vector2(confiner.size.x, confiner.size.y);

        return box;
    }

    private static void ApplyConfiner(CinemachineCamera camera, Collider2D bounds)
    {
        var confiner = camera.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            confiner = Undo.AddComponent<CinemachineConfiner2D>(camera.gameObject);
        }

        Undo.RecordObject(confiner, "Configure confiner");
        confiner.BoundingShape2D = bounds;
        confiner.Damping = 0.2f;
        confiner.SlowingDistance = 0f;

        // The bounding shape is always strictly larger than the view on pinned axes, so the
        // oversize-window bake is never needed. Leaving it off keeps the bake cheap.
        confiner.OversizeWindow = new CinemachineConfiner2D.OversizeWindowSettings
        {
            Enabled = false,
            MaxWindowSize = 0f,
            Padding = 0f
        };
        confiner.InvalidateBoundingShapeCache();
    }

    /// <summary>
    /// Carries the authored follow feel over to the composer.
    ///
    /// Rooms that used the old horizontal-follow path had their responsiveness set by
    /// <c>_horizontalFollowSmoothTime</c> (0.15 in this scene) and drove the transform by hand,
    /// which left the composer's own Damping as dead configuration - 1.0 on the long hall camera,
    /// almost seven times slower. Copying the old value across keeps corridors feeling the way
    /// they were tuned instead of silently becoming sluggish.
    ///
    /// Damping on a pinned axis is zeroed because the confiner holds that axis anyway; leaving a
    /// value there only adds a slow drift the designer cannot see the cause of.
    /// </summary>
    private static void ApplyFollowDamping(
        CinemachineCamera camera,
        RoomCameraTrigger trigger,
        Bounds roomBounds,
        float orthographicSize)
    {
        var composer = camera.GetComponent<CinemachinePositionComposer>();
        if (composer == null)
        {
            return;
        }

        var so = new SerializedObject(trigger);
        bool usedHorizontalPath = ReadBool(so, "_useHorizontalFollowCameraWhenEntered", false);
        float authored = ReadFloat(so, "_horizontalFollowSmoothTime", 0.15f);
        float damping = usedHorizontalPath ? Mathf.Max(0f, authored) : composer.Damping.x;

        Vector2 view = RoomCameraBoundsUtility.GetViewSize(orthographicSize, TargetAspect);
        bool pinnedX = roomBounds.size.x <= view.x;
        bool pinnedY = roomBounds.size.y <= view.y;

        Undo.RecordObject(composer, "Configure follow damping");
        composer.Damping = new Vector3(
            pinnedX ? 0f : damping,
            pinnedY ? 0f : damping,
            0f);
    }

    private static void EnsureBodyComponent(CinemachineCamera camera)
    {
        if (camera.GetComponent<CinemachinePositionComposer>() != null ||
            camera.GetComponent<CinemachineFollow>() != null)
        {
            return;
        }

        var composer = Undo.AddComponent<CinemachinePositionComposer>(camera.gameObject);
        Undo.RecordObject(composer, "Configure composer");
        composer.CameraDistance = 10f;
        composer.Damping = new Vector3(0.6f, 0.6f, 0f);
        composer.CenterOnActivate = true;
    }

    private static void CreateVolume(
        RoomCameraTrigger trigger,
        CinemachineCamera camera,
        Collider2D boundsCollider)
    {
        GameObject host = trigger.gameObject;
        var volume = host.GetComponent<RoomCameraVolume>();
        if (volume == null)
        {
            volume = Undo.AddComponent<RoomCameraVolume>(host);
        }

        var so = new SerializedObject(trigger);
        int active = ReadInt(so, "_activePriority", 20);
        int inactive = ReadInt(so, "_inactivePriority", 0);
        Collider2D[] areaColliders = ReadColliders(so, "_areaColliders2D");

        Undo.RecordObject(volume, "Configure room volume");
        volume.EditorAssign(camera, areaColliders, boundsCollider, active, inactive);
    }

    private static void EnsureDirector(CinemachineCamera followCamera)
    {
        var director = Object.FindFirstObjectByType<RoomCameraDirector>();
        if (director == null)
        {
            var existing = GameObject.Find("CameraManager");
            GameObject host = existing != null ? existing : new GameObject("RoomCameraDirector");
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(host, "Create room camera director");
            }

            director = Undo.AddComponent<RoomCameraDirector>(host);
        }

        Undo.RecordObject(director, "Configure room camera director");
        director.EditorAssignFallback(followCamera);
    }

    private static int DisableLegacyPortals()
    {
        int count = 0;
        count += DisableAll<RoomCameraPortal>();
        count += DisableAll<VerticalRoomCameraPortal>();
        count += DisableAll<FallRoomCameraPortal>();
        count += DisableAll<RoomCameraSwitchPortal>();
        count += DisableAll<RoomCameraGate>();
        return count;
    }

    private static int DisableAll<T>() where T : MonoBehaviour
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Undo.RecordObject(found[i], "Disable legacy portal");
            found[i].enabled = false;
        }

        return found.Length;
    }

    /// <summary>
    /// RoomCameraTrigger is deliberately kept.
    ///
    /// It is not only a camera trigger: RoomFogRevealManager, RoomEnemyActivityManager and
    /// StoryEventController all use it as the room identity and subscribe to its
    /// ActiveRoomChanged event. Deleting it would silently break fog reveal, enemy sleep/wake
    /// and story-event room restore. RoomCameraDirector sets
    /// RoomCameraTrigger.ExternalCameraAuthority instead, which disables only the
    /// camera-driving half and republishes room changes through the same event.
    /// </summary>
    private static int CountRetainedTriggers(RoomCameraTrigger[] triggers)
    {
        int retained = 0;
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null)
            {
                retained++;
            }
        }

        return retained;
    }

    private static void ApplyBrainBlend(StringBuilder report)
    {
        var brain = Object.FindFirstObjectByType<CinemachineBrain>();
        if (brain == null)
        {
            report.AppendLine("CinemachineBrain が見つからないため、ブレンド時間は未変更です。");
            return;
        }

        Undo.RecordObject(brain, "Configure brain blend");
        CinemachineBlendDefinition blend = brain.DefaultBlend;
        string previous = $"{blend.Style} {blend.Time}s";
        blend.Style = RoomBlendStyle;
        blend.Time = 0f;
        brain.DefaultBlend = blend;
        report.AppendLine($"CinemachineBrain.DefaultBlend: {previous} -> Cut");
    }

    private static CinemachineCamera FindCameraByName(string name)
    {
        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].gameObject.name == name)
            {
                return cameras[i];
            }
        }

        return null;
    }

    private static string StripColliderPrefix(string objectName)
    {
        return objectName.StartsWith("Col_") ? objectName.Substring(4) : objectName;
    }

    private static int ReadInt(SerializedObject so, string path, int fallback)
    {
        SerializedProperty property = so.FindProperty(path);
        return property != null ? property.intValue : fallback;
    }

    private static bool ReadBool(SerializedObject so, string path, bool fallback)
    {
        SerializedProperty property = so.FindProperty(path);
        return property != null ? property.boolValue : fallback;
    }

    private static float ReadFloat(SerializedObject so, string path, float fallback)
    {
        SerializedProperty property = so.FindProperty(path);
        return property != null ? property.floatValue : fallback;
    }

    private static Collider2D[] ReadColliders(SerializedObject so, string path)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property == null || !property.isArray)
        {
            return System.Array.Empty<Collider2D>();
        }

        var list = new List<Collider2D>(property.arraySize);
        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue is Collider2D collider)
            {
                list.Add(collider);
            }
        }

        return list.ToArray();
    }
}
