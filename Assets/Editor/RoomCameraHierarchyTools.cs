using System.Collections.Generic;
using GameName.Enemy;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

public static class RoomCameraHierarchyTools
{
    private const string AreaRootName = "Area";
    private const string GlobalGatesRootName = "CameraGates";
    private const string LocalGatesRootName = "Gates";
    private const string ColliderPrefix = "Col_";
    private const string CameraPrefix = "CN_";
    private const string GatePrefix = "Gate_";
    private const string PortalPrefix = "Portal_";
    private const string FallPortalPrefix = "FallPortal_";
    private const string VerticalPortalPrefix = "VerticalPortal_";
    private const string SwitchPortalPrefix = "SwitchPortal_";
    private const string BossCameraSuffix = "_Boss";
    private const string BossCameraTargetName = "BossCameraTarget";

    [MenuItem("GameObject/Camera Area/Setup Camera Area", false, 10)]
    private static void SetupCameraArea()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            CreateCompleteAreaWithConnectionGates();
            return;
        }

        if (Selection.gameObjects.Length == 1 &&
            Selection.activeTransform != null &&
            Selection.activeTransform.name == AreaRootName)
        {
            RenameAreasToDisplayNumberScheme(Selection.activeTransform);
            SetupChildrenWithConnectionGates(Selection.activeTransform);
            return;
        }

        for (int i = 0; i < Selection.gameObjects.Length; i++)
        {
            Transform areaRoot = ResolveAreaRoot(Selection.gameObjects[i].transform);
            RenameAreasToDisplayNumberScheme(areaRoot);
            SetupAreaObject(Selection.gameObjects[i]);
            SetupConnectionGatesForSingleArea(Selection.gameObjects[i].transform);
        }

        Debug.Log($"[RoomCameraHierarchyTools] Set up {Selection.gameObjects.Length} selected camera area(s).");
    }

    [MenuItem("GameObject/Camera Area/Setup Camera Area", true)]
    private static bool ValidateSetupCameraArea()
    {
        return true;
    }

    [MenuItem("GameObject/Camera Area/Setup Selected As Boss Area", false, 11)]
    private static void SetupSelectedAsBossArea()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("[RoomCameraHierarchyTools] Select an area object like 301 before setting up a boss area.");
            return;
        }

        int setupCount = 0;
        for (int i = 0; i < Selection.gameObjects.Length; i++)
        {
            Transform areaTransform = ResolveSelectedAreaTransform(Selection.gameObjects[i].transform);
            if (areaTransform == null || areaTransform.name == AreaRootName)
            {
                continue;
            }

            Transform areaRoot = ResolveAreaRoot(areaTransform);
            RenameAreasToDisplayNumberScheme(areaRoot);
            SetupBossArea(areaTransform);
            setupCount++;
        }

        Debug.Log($"[RoomCameraHierarchyTools] Set up {setupCount} boss area(s). Assign Boss Root if it was not auto-detected.");
    }

    [MenuItem("GameObject/Camera Area/Setup Selected As Boss Area", true)]
    private static bool ValidateSetupSelectedAsBossArea()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    [MenuItem("GameObject/Camera Area/Create Camera Portal", false, 12)]
    private static void CreateCameraPortal()
    {
        Transform roomA = null;
        Transform roomB = null;

        if (Selection.gameObjects != null && Selection.gameObjects.Length >= 2)
        {
            roomA = ResolveSelectedAreaTransform(Selection.gameObjects[0].transform);
            roomB = ResolveSelectedAreaTransform(Selection.gameObjects[1].transform);
        }

        Transform areaRoot = ResolveAreaRoot(roomA != null ? roomA : Selection.activeTransform);
        bool createdPortal = false;
        GameObject portalObject = ResolveOrCreateCameraPortal(areaRoot, roomA, roomB, out createdPortal);
        if (createdPortal)
        {
            portalObject.transform.position = CreatePortalPosition(roomA, roomB);
        }

        BoxCollider2D portalCollider = portalObject.GetComponent<BoxCollider2D>();
        if (portalCollider == null)
        {
            portalCollider = Undo.AddComponent<BoxCollider2D>(portalObject);
        }

        portalCollider.isTrigger = true;
        if (createdPortal)
        {
            portalCollider.size = CreatePortalColliderSize(roomA, roomB);
        }

        RoomCameraPortal portal = portalObject.GetComponent<RoomCameraPortal>();
        if (portal == null)
        {
            portal = Undo.AddComponent<RoomCameraPortal>(portalObject);
        }

        SerializedObject serializedPortal = new SerializedObject(portal);
        SetObject(serializedPortal, "roomA", roomA != null ? ResolveRoomTrigger(roomA.gameObject) : null);
        SetObject(serializedPortal, "roomB", roomB != null ? ResolveRoomTrigger(roomB.gameObject) : null);
        SetFloat(serializedPortal, "targetRoomHoldSeconds", 0.2f);
        SetBool(serializedPortal, "limitBossRoomPreview", true);
        SetBool(serializedPortal, "treatTargetAsBossRoom", false);
        SetFloat(serializedPortal, "bossRoomTargetWeight", 0.18f);
        SetFloat(serializedPortal, "bossRoomZoomWeight", 0.18f);
        SetFloat(serializedPortal, "bossRoomOrthographicSizeMultiplier", 0.8f);
        SetFloat(serializedPortal, "bossRoomRevealDistance", 1.5f);
        SetString(serializedPortal, "playerTag", "Player");
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeObject = portalObject;
        string action = createdPortal ? "Created" : "Repaired";
        Debug.Log($"[RoomCameraHierarchyTools] {action} camera portal {portalObject.name}. Place it between the two areas.");
    }

    [MenuItem("GameObject/Camera Area/Create Vertical Camera Portal", false, 13)]
    private static void CreateVerticalCameraPortal()
    {
        Transform upperRoom = null;
        Transform lowerRoom = null;

        if (Selection.gameObjects != null && Selection.gameObjects.Length >= 2)
        {
            upperRoom = ResolveSelectedAreaTransform(Selection.gameObjects[0].transform);
            lowerRoom = ResolveSelectedAreaTransform(Selection.gameObjects[1].transform);
        }

        Transform areaRoot = ResolveAreaRoot(upperRoom != null ? upperRoom : Selection.activeTransform);
        bool createdPortal = false;
        GameObject portalObject = ResolveOrCreateVerticalCameraPortal(areaRoot, upperRoom, lowerRoom, out createdPortal);
        if (createdPortal)
        {
            portalObject.transform.position = CreatePortalPosition(upperRoom, lowerRoom);
        }

        BoxCollider2D portalCollider = portalObject.GetComponent<BoxCollider2D>();
        if (portalCollider == null)
        {
            portalCollider = Undo.AddComponent<BoxCollider2D>(portalObject);
        }

        portalCollider.isTrigger = true;
        if (createdPortal)
        {
            portalCollider.size = new Vector2(6f, 1.5f);
        }

        VerticalRoomCameraPortal portal = portalObject.GetComponent<VerticalRoomCameraPortal>();
        if (portal == null)
        {
            portal = Undo.AddComponent<VerticalRoomCameraPortal>(portalObject);
        }

        SerializedObject serializedPortal = new SerializedObject(portal);
        SetObject(serializedPortal, "upperRoom", upperRoom != null ? ResolveRoomTrigger(upperRoom.gameObject) : null);
        SetObject(serializedPortal, "lowerRoom", lowerRoom != null ? ResolveRoomTrigger(lowerRoom.gameObject) : null);
        SetInt(serializedPortal, "transitionPriority", 30);
        SetEnum(serializedPortal, "direction", 0);
        SetFloat(serializedPortal, "portalRoomWeight", 0.35f);
        SetFloat(serializedPortal, "smoothTime", 0.12f);
        SetBool(serializedPortal, "commitWhenPlayerFullyInsideTargetRoom", false);
        SetBool(serializedPortal, "commitByPortalExitSide", true);
        SetString(serializedPortal, "playerTag", "Player");
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeObject = portalObject;
        string action = createdPortal ? "Created" : "Repaired";
        Debug.Log($"[RoomCameraHierarchyTools] {action} vertical camera portal {portalObject.name}. Place it on the boundary between the upper and lower areas.");
    }

    [MenuItem("GameObject/Camera Area/Create Fall Camera Portal", false, 14)]
    private static void CreateFallCameraPortal()
    {
        Transform upperRoom = null;
        Transform lowerRoom = null;

        if (Selection.gameObjects != null && Selection.gameObjects.Length >= 2)
        {
            upperRoom = ResolveSelectedAreaTransform(Selection.gameObjects[0].transform);
            lowerRoom = ResolveSelectedAreaTransform(Selection.gameObjects[1].transform);
        }

        Transform areaRoot = ResolveAreaRoot(upperRoom != null ? upperRoom : Selection.activeTransform);
        bool createdPortal = false;
        GameObject portalObject = ResolveOrCreateFallCameraPortal(areaRoot, upperRoom, lowerRoom, out createdPortal);
        if (createdPortal)
        {
            portalObject.transform.position = CreatePortalPosition(upperRoom, lowerRoom);
        }

        BoxCollider2D portalCollider = portalObject.GetComponent<BoxCollider2D>();
        if (portalCollider == null)
        {
            portalCollider = Undo.AddComponent<BoxCollider2D>(portalObject);
        }

        portalCollider.isTrigger = true;
        if (createdPortal)
        {
            portalCollider.size = new Vector2(6f, 1.5f);
        }

        FallRoomCameraPortal portal = portalObject.GetComponent<FallRoomCameraPortal>();
        if (portal == null)
        {
            portal = Undo.AddComponent<FallRoomCameraPortal>(portalObject);
        }

        SerializedObject serializedPortal = new SerializedObject(portal);
        SetObject(serializedPortal, "upperRoom", upperRoom != null ? ResolveRoomTrigger(upperRoom.gameObject) : null);
        SetObject(serializedPortal, "lowerRoom", lowerRoom != null ? ResolveRoomTrigger(lowerRoom.gameObject) : null);
        SetString(serializedPortal, "playerTag", "Player");
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeObject = portalObject;
        string action = createdPortal ? "Created" : "Repaired";
        Debug.Log($"[RoomCameraHierarchyTools] {action} fall camera portal {portalObject.name}. Move it slightly below the drop opening.");
    }

    [MenuItem("GameObject/Camera Area/Create Camera Switch Portal", false, 15)]
    private static void CreateCameraSwitchPortal()
    {
        Transform targetArea = Selection.activeTransform != null
            ? ResolveSelectedAreaTransform(Selection.activeTransform)
            : null;
        Transform areaRoot = ResolveAreaRoot(targetArea != null ? targetArea : Selection.activeTransform);

        string portalName = CreateCameraSwitchPortalName(areaRoot, targetArea);
        GameObject portalObject = new GameObject(portalName);
        Undo.RegisterCreatedObjectUndo(portalObject, "Create Camera Switch Portal");
        portalObject.transform.SetParent(areaRoot, false);
        portalObject.transform.position = targetArea != null && TryGetAreaCenter(targetArea, out Vector3 targetCenter)
            ? targetCenter
            : GetCreationPosition();

        BoxCollider2D portalCollider = Undo.AddComponent<BoxCollider2D>(portalObject);
        portalCollider.isTrigger = true;
        portalCollider.size = new Vector2(3f, 4f);

        RoomCameraSwitchPortal portal = Undo.AddComponent<RoomCameraSwitchPortal>(portalObject);
        SerializedObject serializedPortal = new SerializedObject(portal);
        SetObject(serializedPortal, "targetRoom", targetArea != null ? ResolveRoomTrigger(targetArea.gameObject) : null);
        SetString(serializedPortal, "playerTag", "Player");
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeObject = portalObject;
        Debug.Log($"[RoomCameraHierarchyTools] Created camera switch portal {portalObject.name}. Set Target Room if needed, then place it where touching should switch cameras.");
    }

    [MenuItem("GameObject/Camera Area/Create Vertical Camera Portal", true)]
    private static bool ValidateCreateVerticalCameraPortal()
    {
        return CanCreateVerticalCameraPortal();
    }

    [MenuItem("GameObject/Camera Area/Create Fall Camera Portal", true)]
    private static bool ValidateCreateFallCameraPortal()
    {
        return CanCreateVerticalCameraPortal();
    }

    private static bool CanCreateVerticalCameraPortal()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length < 2)
        {
            return false;
        }

        return ResolveSelectedAreaTransform(Selection.gameObjects[0].transform) != null &&
               ResolveSelectedAreaTransform(Selection.gameObjects[1].transform) != null;
    }

    private static void SetupChildrenWithConnectionGates(Transform root)
    {
        List<Transform> areaChildren = GetSortedAreaChildren(root);

        int setupCount = 0;
        for (int i = 0; i < areaChildren.Count; i++)
        {
            Transform child = areaChildren[i];
            SetupAreaObject(child.gameObject);
            setupCount++;
        }

        int gateCount = SetupGatesFromMinimapLinks(root);
        if (gateCount == 0)
        {
            gateCount = SetupSequentialConnectionGates(areaChildren);
        }

        Debug.Log($"[RoomCameraHierarchyTools] Set up {setupCount} camera areas and {gateCount} connection gate(s) under {root.name}.");
    }

    private static void CreateCompleteAreaWithConnectionGates()
    {
        Transform areaRoot = ResolveAreaRoot();
        RenameAreasToDisplayNumberScheme(areaRoot);
        string areaName = CreateNextAreaName(areaRoot);
        GameObject areaObject = new GameObject(areaName);
        Undo.RegisterCreatedObjectUndo(areaObject, "Create Complete Camera Area");
        areaObject.transform.SetParent(areaRoot, false);
        areaObject.transform.position = GetCreationPosition();

        SetupAreaObject(areaObject);
        SetupConnectionGatesForSingleArea(areaObject.transform);

        Selection.activeObject = areaObject;
    }

    private static void SetupAreaObject(GameObject areaObject)
    {
        if (areaObject == null)
        {
            return;
        }

        GameObject triggerHost = ResolveOrCreateTriggerHost(areaObject);
        RoomCameraTrigger roomTrigger = triggerHost.GetComponent<RoomCameraTrigger>();
        if (roomTrigger == null)
        {
            roomTrigger = Undo.AddComponent<RoomCameraTrigger>(triggerHost);
        }

        Collider2D areaBounds = ResolveOrCreateAreaBounds(triggerHost);
        CinemachineCamera roomCamera = ResolveOrCreateRoomCamera(areaObject.transform);

        SerializedObject serializedTrigger = new SerializedObject(roomTrigger);
        serializedTrigger.FindProperty("_roomCamera").objectReferenceValue = roomCamera;
        SetObjectReferenceArray(serializedTrigger.FindProperty("_areaColliders2D"), areaBounds);
        serializedTrigger.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeObject = areaObject;
    }

    private static void SetupBossArea(Transform areaTransform)
    {
        if (areaTransform == null)
        {
            return;
        }

        SetupAreaObject(areaTransform.gameObject);

        GameObject triggerHost = ResolveOrCreateTriggerHost(areaTransform.gameObject);
        Collider2D areaBounds = ResolveOrCreateAreaBounds(triggerHost);
        BossAreaController bossArea = triggerHost.GetComponent<BossAreaController>();
        if (bossArea == null)
        {
            bossArea = Undo.AddComponent<BossAreaController>(triggerHost);
        }

        CinemachineCamera bossCamera = ResolveOrCreateBossCamera(areaTransform, areaBounds);
        DualTargetCameraTarget bossCameraTarget = ResolveOrCreateBossCameraTarget(areaTransform, areaBounds, bossCamera);
        Transform bossRoot = ResolveBossRoot(areaTransform);
        StageBgmController stageBgm = UnityEngine.Object.FindFirstObjectByType<StageBgmController>(FindObjectsInactive.Include);

        SerializedObject serializedBossArea = new SerializedObject(bossArea);
        SetString(serializedBossArea, "playerTag", "Player");
        SetBool(serializedBossArea, "disableTriggerAfterStart", true);
        SetString(serializedBossArea, "bossDisplayName", bossRoot != null ? bossRoot.name : "Boss");
        SetObject(serializedBossArea, "bossRoot", bossRoot);
        SetObject(serializedBossArea, "stageBossAttack", bossRoot != null ? bossRoot.GetComponent<StageBossAttack>() : null);
        SetObject(serializedBossArea, "lastBossController", bossRoot != null ? bossRoot.GetComponent<LastBossController>() : null);
        SetString(serializedBossArea, "bossDefeatedFlagKey", CreateBossDefeatedFlagKey(areaTransform.name));
        SetBool(serializedBossArea, "hideBossWhenDefeated", true);
        // 自動セットアップ時もStageBoss専用イントロの標準値を入れておく。
        SetBool(serializedBossArea, "playStageBossIntro", true);
        SetBool(serializedBossArea, "hideStageBossUntilIntro", true);
        SetFloat(serializedBossArea, "stageBossEntryDelaySeconds", 2f);
        SetFloat(serializedBossArea, "stageBossNormalBgmFadeOutSeconds", 1f);
        SetBool(serializedBossArea, "waitForStageBossPlayerGroundedBeforeLock", true);
        SetFloat(serializedBossArea, "stageBossRevealDuration", 3f);
        SetFloat(serializedBossArea, "stageBossHpLeadInSeconds", 1f);
        SetBool(serializedBossArea, "lockPlayerFacingStageBoss", true);
        SetFloat(serializedBossArea, "stageBossMirageAmplitude", 0.08f);
        SetFloat(serializedBossArea, "stageBossMirageFrequency", 8f);
        SetObject(serializedBossArea, "fixedBossCamera", bossCamera);
        SetObject(serializedBossArea, "dualTargetCameraTarget", bossCameraTarget);
        SetInt(serializedBossArea, "activeCameraPriority", 50);
        SetInt(serializedBossArea, "inactiveCameraPriority", 0);
        SetObject(serializedBossArea, "stageBgm", stageBgm);
        SetBool(serializedBossArea, "returnToNormalAfterBoss", true);
        SetBool(serializedBossArea, "confineInsideArea", true);
        SetBool(serializedBossArea, "confinePlayerInsideArea", true);
        SetBool(serializedBossArea, "confineBossInsideArea", true);
        SetBool(serializedBossArea, "confineX", true);
        SetBool(serializedBossArea, "confineY", true);
        SetBool(serializedBossArea, "confineYForDynamicBodies", false);
        serializedBossArea.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(bossArea);
        Selection.activeObject = areaTransform.gameObject;
    }

    private static CinemachineCamera ResolveOrCreateBossCamera(Transform areaTransform, Collider2D areaBounds)
    {
        string cameraName = $"{CameraPrefix}{areaTransform.name}{BossCameraSuffix}";
        Transform existingCamera = areaTransform.Find(cameraName);
        CinemachineCamera bossCamera;
        if (existingCamera != null)
        {
            bossCamera = existingCamera.GetComponent<CinemachineCamera>();
            if (bossCamera == null)
            {
                bossCamera = Undo.AddComponent<CinemachineCamera>(existingCamera.gameObject);
            }
        }
        else
        {
            GameObject cameraObject = new GameObject(cameraName);
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Boss Area Camera");
            cameraObject.transform.SetParent(areaTransform, false);
            cameraObject.transform.position = CreateCameraPosition(areaTransform, areaBounds);
            bossCamera = cameraObject.AddComponent<CinemachineCamera>();
        }

        bossCamera.Priority.Enabled = true;
        bossCamera.Priority.Value = 0;
        EnsureBossCameraFollowComponent(bossCamera);

        LensSettings lens = bossCamera.Lens;
        lens.OrthographicSize = 12f;
        bossCamera.Lens = lens;
        EditorUtility.SetDirty(bossCamera);
        return bossCamera;
    }

    private static DualTargetCameraTarget ResolveOrCreateBossCameraTarget(
        Transform areaTransform,
        Collider2D areaBounds,
        CinemachineCamera bossCamera)
    {
        Transform targetTransform = areaTransform.Find(BossCameraTargetName);
        if (targetTransform == null)
        {
            GameObject targetObject = new GameObject(BossCameraTargetName);
            Undo.RegisterCreatedObjectUndo(targetObject, "Create Boss Camera Target");
            targetObject.transform.SetParent(areaTransform, false);
            targetObject.transform.position = CreateTargetPosition(areaTransform, areaBounds);
            targetTransform = targetObject.transform;
        }
        else
        {
            targetTransform.position = CreateTargetPosition(areaTransform, areaBounds);
        }

        DualTargetCameraTarget target = targetTransform.GetComponent<DualTargetCameraTarget>();
        if (target == null)
        {
            target = Undo.AddComponent<DualTargetCameraTarget>(targetTransform.gameObject);
        }

        SerializedObject serializedTarget = new SerializedObject(target);
        SetFloat(serializedTarget, "secondaryWeight", 0.5f);
        SetFloat(serializedTarget, "smoothTime", 0.35f);
        SetFloat(serializedTarget, "maxSpeed", 80f);
        SetBool(serializedTarget, "keepCurrentZ", true);
        SetBool(serializedTarget, "startFromCurrentMainCamera", true);
        SetBool(serializedTarget, "preferZoomOverCenterMovement", true);
        SetFloat(serializedTarget, "innerFrame", 0.82f);
        SetObject(serializedTarget, "controlledCamera", bossCamera);
        SetBool(serializedTarget, "assignSelfAsTrackingTarget", true);
        SetBool(serializedTarget, "confineCameraToBounds", true);
        SetObject(serializedTarget, "cameraBounds2D", areaBounds);
        SetBool(serializedTarget, "limitZoomToBounds", true);
        SetVector2(serializedTarget, "boundsInset", Vector2.zero);
        SetBool(serializedTarget, "adjustOrthographicSize", true);
        SetBool(serializedTarget, "useControlledCameraSizeAsMinimum", true);
        SetFloat(serializedTarget, "minOrthographicSize", 12f);
        SetFloat(serializedTarget, "maxOrthographicSize", 16f);
        SetFloat(serializedTarget, "horizontalPadding", 4f);
        SetFloat(serializedTarget, "verticalPadding", 3f);
        SetFloat(serializedTarget, "zoomSmoothTime", 0.3f);
        serializedTarget.ApplyModifiedPropertiesWithoutUndo();

        bossCamera.Follow = targetTransform;
        EnsureBossCameraFollowComponent(bossCamera);
        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(bossCamera);
        return target;
    }

    private static void EnsureBossCameraFollowComponent(CinemachineCamera bossCamera)
    {
        if (bossCamera == null)
        {
            return;
        }

        CinemachineFollow follow = bossCamera.GetComponent<CinemachineFollow>();
        if (follow == null)
        {
            follow = Undo.AddComponent<CinemachineFollow>(bossCamera.gameObject);
        }

        follow.FollowOffset = new Vector3(0f, 0f, -10f);
        var trackerSettings = follow.TrackerSettings;
        trackerSettings.PositionDamping = Vector3.zero;
        trackerSettings.RotationDamping = Vector3.zero;
        follow.TrackerSettings = trackerSettings;
        EditorUtility.SetDirty(follow);
    }

    private static Transform ResolveSelectedAreaTransform(Transform selected)
    {
        if (selected == null)
        {
            return null;
        }

        if (selected.parent != null && selected.parent.name == AreaRootName)
        {
            return selected;
        }

        Transform current = selected;
        while (current != null)
        {
            if (current.parent != null && current.parent.name == AreaRootName)
            {
                return current;
            }

            current = current.parent;
        }

        return selected;
    }

    private static Vector3 CreateCameraPosition(Transform areaTransform, Collider2D areaBounds)
    {
        Vector3 position = areaBounds != null ? areaBounds.bounds.center : areaTransform.position;
        position.z = -10f;
        return position;
    }

    private static Vector3 CreateTargetPosition(Transform areaTransform, Collider2D areaBounds)
    {
        Vector3 position = areaBounds != null ? areaBounds.bounds.center : areaTransform.position;
        position.z = 0f;
        return position;
    }

    private static Transform ResolveBossRoot(Transform areaTransform)
    {
        if (areaTransform == null)
        {
            return null;
        }

        StageBossAttack stageBoss = areaTransform.GetComponentInChildren<StageBossAttack>(true);
        if (stageBoss != null)
        {
            return stageBoss.transform;
        }

        LastBossController lastBoss = areaTransform.GetComponentInChildren<LastBossController>(true);
        if (lastBoss != null)
        {
            return lastBoss.transform;
        }

        return null;
    }

    private static string CreateBossDefeatedFlagKey(string areaName)
    {
        return string.IsNullOrWhiteSpace(areaName)
            ? "boss_area_defeated"
            : $"boss_{areaName}_defeated";
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }

    private static void SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
        }
    }

    private static string CreatePortalName(Transform areaRoot, Transform roomA, Transform roomB)
    {
        string baseName = roomA != null && roomB != null
            ? $"{PortalPrefix}{roomA.name}_{roomB.name}"
            : $"{PortalPrefix}Area_Area";

        if (areaRoot == null || areaRoot.Find(baseName) == null)
        {
            return baseName;
        }

        return CreateUniqueChildName(areaRoot, baseName);
    }

    private static string CreateFallPortalName(Transform areaRoot, Transform upperRoom, Transform lowerRoom)
    {
        string baseName = upperRoom != null && lowerRoom != null
            ? $"{FallPortalPrefix}{upperRoom.name}_{lowerRoom.name}"
            : $"{FallPortalPrefix}Upper_Lower";

        if (areaRoot == null || areaRoot.Find(baseName) == null)
        {
            return baseName;
        }

        return CreateUniqueChildName(areaRoot, baseName);
    }

    private static string CreateVerticalPortalName(Transform areaRoot, Transform upperRoom, Transform lowerRoom)
    {
        string baseName = upperRoom != null && lowerRoom != null
            ? $"{VerticalPortalPrefix}{upperRoom.name}_{lowerRoom.name}"
            : $"{VerticalPortalPrefix}Upper_Lower";

        if (areaRoot == null || areaRoot.Find(baseName) == null)
        {
            return baseName;
        }

        return CreateUniqueChildName(areaRoot, baseName);
    }

    private static string CreateCameraSwitchPortalName(Transform areaRoot, Transform targetArea)
    {
        string baseName = targetArea != null
            ? $"{SwitchPortalPrefix}{targetArea.name}"
            : $"{SwitchPortalPrefix}Target";

        if (areaRoot == null || areaRoot.Find(baseName) == null)
        {
            return baseName;
        }

        return CreateUniqueChildName(areaRoot, baseName);
    }

    private static GameObject ResolveOrCreateCameraPortal(
        Transform areaRoot,
        Transform roomA,
        Transform roomB,
        out bool createdPortal)
    {
        createdPortal = false;
        Transform existingPortal = FindExistingPortal(areaRoot, roomA, roomB);
        if (existingPortal != null)
        {
            return existingPortal.gameObject;
        }

        string portalName = CreatePortalName(areaRoot, roomA, roomB);
        GameObject portalObject = new GameObject(portalName);
        Undo.RegisterCreatedObjectUndo(portalObject, "Create Camera Portal");
        portalObject.transform.SetParent(areaRoot, false);
        createdPortal = true;
        return portalObject;
    }

    private static GameObject ResolveOrCreateFallCameraPortal(
        Transform areaRoot,
        Transform upperRoom,
        Transform lowerRoom,
        out bool createdPortal)
    {
        createdPortal = false;
        Transform existingPortal = FindExistingFallPortal(areaRoot, upperRoom, lowerRoom);
        if (existingPortal != null)
        {
            return existingPortal.gameObject;
        }

        string portalName = CreateFallPortalName(areaRoot, upperRoom, lowerRoom);
        GameObject portalObject = new GameObject(portalName);
        Undo.RegisterCreatedObjectUndo(portalObject, "Create Fall Camera Portal");
        portalObject.transform.SetParent(areaRoot, false);
        createdPortal = true;
        return portalObject;
    }

    private static GameObject ResolveOrCreateVerticalCameraPortal(
        Transform areaRoot,
        Transform upperRoom,
        Transform lowerRoom,
        out bool createdPortal)
    {
        createdPortal = false;
        Transform existingPortal = FindExistingVerticalPortal(areaRoot, upperRoom, lowerRoom);
        if (existingPortal != null)
        {
            return existingPortal.gameObject;
        }

        string portalName = CreateVerticalPortalName(areaRoot, upperRoom, lowerRoom);
        GameObject portalObject = new GameObject(portalName);
        Undo.RegisterCreatedObjectUndo(portalObject, "Create Vertical Camera Portal");
        portalObject.transform.SetParent(areaRoot, false);
        createdPortal = true;
        return portalObject;
    }

    private static Transform FindExistingPortal(Transform areaRoot, Transform roomA, Transform roomB)
    {
        if (areaRoot == null || roomA == null || roomB == null)
        {
            return null;
        }

        Transform direct = areaRoot.Find($"{PortalPrefix}{roomA.name}_{roomB.name}");
        if (direct != null)
        {
            return direct;
        }

        Transform reverse = areaRoot.Find($"{PortalPrefix}{roomB.name}_{roomA.name}");
        if (reverse != null)
        {
            return reverse;
        }

        RoomCameraTrigger triggerA = ResolveRoomTrigger(roomA.gameObject);
        RoomCameraTrigger triggerB = ResolveRoomTrigger(roomB.gameObject);
        for (int i = 0; i < areaRoot.childCount; i++)
        {
            Transform child = areaRoot.GetChild(i);
            RoomCameraPortal portal = child.GetComponent<RoomCameraPortal>();
            if (portal == null)
            {
                continue;
            }

            SerializedObject serializedPortal = new SerializedObject(portal);
            RoomCameraTrigger portalRoomA = serializedPortal.FindProperty("roomA")?.objectReferenceValue as RoomCameraTrigger;
            RoomCameraTrigger portalRoomB = serializedPortal.FindProperty("roomB")?.objectReferenceValue as RoomCameraTrigger;
            bool isSamePair =
                portalRoomA == triggerA && portalRoomB == triggerB ||
                portalRoomA == triggerB && portalRoomB == triggerA;
            if (isSamePair)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindExistingFallPortal(Transform areaRoot, Transform upperRoom, Transform lowerRoom)
    {
        if (areaRoot == null || upperRoom == null || lowerRoom == null)
        {
            return null;
        }

        Transform direct = areaRoot.Find($"{FallPortalPrefix}{upperRoom.name}_{lowerRoom.name}");
        if (direct != null)
        {
            return direct;
        }

        RoomCameraTrigger upperTrigger = ResolveRoomTrigger(upperRoom.gameObject);
        RoomCameraTrigger lowerTrigger = ResolveRoomTrigger(lowerRoom.gameObject);
        for (int i = 0; i < areaRoot.childCount; i++)
        {
            Transform child = areaRoot.GetChild(i);
            FallRoomCameraPortal portal = child.GetComponent<FallRoomCameraPortal>();
            if (portal == null)
            {
                continue;
            }

            SerializedObject serializedPortal = new SerializedObject(portal);
            RoomCameraTrigger portalUpperRoom =
                serializedPortal.FindProperty("upperRoom")?.objectReferenceValue as RoomCameraTrigger;
            RoomCameraTrigger portalLowerRoom =
                serializedPortal.FindProperty("lowerRoom")?.objectReferenceValue as RoomCameraTrigger;

            if (portalUpperRoom == upperTrigger && portalLowerRoom == lowerTrigger)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindExistingVerticalPortal(Transform areaRoot, Transform upperRoom, Transform lowerRoom)
    {
        if (areaRoot == null || upperRoom == null || lowerRoom == null)
        {
            return null;
        }

        Transform direct = areaRoot.Find($"{VerticalPortalPrefix}{upperRoom.name}_{lowerRoom.name}");
        if (direct != null)
        {
            return direct;
        }

        Transform reverse = areaRoot.Find($"{VerticalPortalPrefix}{lowerRoom.name}_{upperRoom.name}");
        if (reverse != null)
        {
            return reverse;
        }

        RoomCameraTrigger upperTrigger = ResolveRoomTrigger(upperRoom.gameObject);
        RoomCameraTrigger lowerTrigger = ResolveRoomTrigger(lowerRoom.gameObject);
        for (int i = 0; i < areaRoot.childCount; i++)
        {
            Transform child = areaRoot.GetChild(i);
            VerticalRoomCameraPortal portal = child.GetComponent<VerticalRoomCameraPortal>();
            if (portal == null)
            {
                continue;
            }

            SerializedObject serializedPortal = new SerializedObject(portal);
            RoomCameraTrigger portalUpperRoom =
                serializedPortal.FindProperty("upperRoom")?.objectReferenceValue as RoomCameraTrigger;
            RoomCameraTrigger portalLowerRoom =
                serializedPortal.FindProperty("lowerRoom")?.objectReferenceValue as RoomCameraTrigger;

            bool isSamePair =
                portalUpperRoom == upperTrigger && portalLowerRoom == lowerTrigger ||
                portalUpperRoom == lowerTrigger && portalLowerRoom == upperTrigger;
            if (isSamePair)
            {
                return child;
            }
        }

        return null;
    }

    private static Vector3 CreatePortalPosition(Transform roomA, Transform roomB)
    {
        if (TryGetAreaCenter(roomA, out Vector3 centerA) &&
            TryGetAreaCenter(roomB, out Vector3 centerB))
        {
            Vector3 position = Vector3.Lerp(centerA, centerB, 0.5f);
            position.z = 0f;
            return position;
        }

        return GetCreationPosition();
    }

    private static Vector2 CreatePortalColliderSize(Transform roomA, Transform roomB)
    {
        return ShouldUseVerticalPortal(roomA, roomB)
            ? new Vector2(4f, 3f)
            : new Vector2(3f, 4f);
    }

    private static bool ShouldUseVerticalPortal(Transform roomA, Transform roomB)
    {
        if (!TryGetAreaCenter(roomA, out Vector3 centerA) ||
            !TryGetAreaCenter(roomB, out Vector3 centerB))
        {
            return false;
        }

        Vector3 delta = centerB - centerA;
        return Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
    }

    private static void RenameAreasToDisplayNumberScheme(Transform areaRoot)
    {
        if (areaRoot == null)
        {
            return;
        }

        List<Transform> areaChildren = GetSortedAreaChildren(areaRoot);
        int renameCount = 0;
        for (int i = 0; i < areaChildren.Count; i++)
        {
            Transform areaTransform = areaChildren[i];
            if (!TryCreateDisplayAreaName(areaTransform.name, out string nextAreaName) ||
                string.Equals(areaTransform.name, nextAreaName, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (areaRoot.Find(nextAreaName) != null)
            {
                Debug.LogWarning($"[RoomCameraHierarchyTools] Cannot rename {areaTransform.name} to {nextAreaName} because that area already exists.", areaTransform);
                continue;
            }

            RenameAreaAndKnownChildren(areaTransform, nextAreaName);
            renameCount++;
        }

        RenameConnectionGates(areaRoot);
        RenameMinimapLinks();
        if (renameCount > 0)
        {
            Debug.Log($"[RoomCameraHierarchyTools] Renamed {renameCount} area(s) to the 101/201/301 display scheme.");
        }
    }

    private static void RenameAreaAndKnownChildren(Transform areaTransform, string nextAreaName)
    {
        string previousAreaName = areaTransform.name;
        RenameObject(areaTransform.gameObject, nextAreaName);

        RenameDirectChild(areaTransform, $"{ColliderPrefix}{previousAreaName}", $"{ColliderPrefix}{nextAreaName}");
        RenameDirectChild(areaTransform, $"{CameraPrefix}{previousAreaName}", $"{CameraPrefix}{nextAreaName}");
        UpdateMinimapRoomIdentity(areaTransform, previousAreaName, nextAreaName);
    }

    private static void RenameDirectChild(Transform parent, string previousName, string nextName)
    {
        if (parent == null)
        {
            return;
        }

        Transform child = parent.Find(previousName);
        if (child == null || parent.Find(nextName) != null)
        {
            return;
        }

        RenameObject(child.gameObject, nextName);
    }

    private static void UpdateMinimapRoomIdentity(Transform areaTransform, string previousAreaName, string nextAreaName)
    {
        MinimapRoom[] rooms = areaTransform.GetComponentsInChildren<MinimapRoom>(true);
        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty roomIdProperty = serializedRoom.FindProperty("roomId");
            SerializedProperty displayNameProperty = serializedRoom.FindProperty("displayName");

            string previousRoomId = $"{ColliderPrefix}{previousAreaName}";
            string nextRoomId = $"{ColliderPrefix}{nextAreaName}";
            string currentRoomId = roomIdProperty.stringValue;
            if (string.IsNullOrWhiteSpace(currentRoomId) ||
                string.Equals(currentRoomId, previousAreaName, System.StringComparison.Ordinal) ||
                string.Equals(currentRoomId, previousRoomId, System.StringComparison.Ordinal))
            {
                roomIdProperty.stringValue = nextRoomId;
            }

            string currentDisplayName = displayNameProperty.stringValue;
            if (string.IsNullOrWhiteSpace(currentDisplayName) ||
                string.Equals(currentDisplayName, previousAreaName, System.StringComparison.Ordinal) ||
                string.Equals(currentDisplayName, previousRoomId, System.StringComparison.Ordinal))
            {
                displayNameProperty.stringValue = nextAreaName;
            }

            serializedRoom.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void RenameConnectionGates(Transform areaRoot)
    {
        List<Transform> areaChildren = GetSortedAreaChildren(areaRoot);
        for (int i = 0; i < areaChildren.Count; i++)
        {
            Transform gatesRoot = areaChildren[i].Find(LocalGatesRootName);
            if (gatesRoot == null)
            {
                continue;
            }

            for (int gateIndex = 0; gateIndex < gatesRoot.childCount; gateIndex++)
            {
                Transform gateTransform = gatesRoot.GetChild(gateIndex);
                if (!TryCreateDisplayGateName(gateTransform.name, out string nextGateName) ||
                    string.Equals(gateTransform.name, nextGateName, System.StringComparison.Ordinal) ||
                    gatesRoot.Find(nextGateName) != null)
                {
                    continue;
                }

                RenameObject(gateTransform.gameObject, nextGateName);
            }
        }
    }

    private static void RenameMinimapLinks()
    {
        MinimapLink[] links = Object.FindObjectsByType<MinimapLink>(FindObjectsSortMode.None);
        for (int i = 0; i < links.Length; i++)
        {
            MinimapLink link = links[i];
            if (link == null || !link.IsValid)
            {
                continue;
            }

            string nextName = $"MinimapLink_{link.FromRoom.RoomId}_{link.ToRoom.RoomId}";
            RenameObject(link.gameObject, nextName);

            SerializedObject serializedLink = new SerializedObject(link);
            SerializedProperty linkIdProperty = serializedLink.FindProperty("linkId");
            linkIdProperty.stringValue = nextName;
            serializedLink.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void RenameObject(GameObject gameObject, string nextName)
    {
        if (gameObject == null ||
            string.IsNullOrWhiteSpace(nextName) ||
            string.Equals(gameObject.name, nextName, System.StringComparison.Ordinal))
        {
            return;
        }

        Undo.RecordObject(gameObject, "Rename Camera Area Object");
        gameObject.name = nextName;
        EditorUtility.SetDirty(gameObject);
    }

    private static RoomCameraTrigger ResolveRoomTrigger(GameObject areaObject)
    {
        if (areaObject == null)
        {
            return null;
        }

        RoomCameraTrigger direct = areaObject.GetComponent<RoomCameraTrigger>();
        if (direct != null)
        {
            return direct;
        }

        RoomCameraTrigger child = areaObject.GetComponentInChildren<RoomCameraTrigger>(true);
        if (child != null)
        {
            return child;
        }

        return areaObject.GetComponentInParent<RoomCameraTrigger>();
    }

    private static GameObject ResolveOrCreateTriggerHost(GameObject areaObject)
    {
        Transform namedCollider = areaObject.transform.Find($"{ColliderPrefix}{areaObject.name}");
        if (namedCollider != null)
        {
            return namedCollider.gameObject;
        }

        RoomCameraTrigger existingTrigger = areaObject.GetComponentInChildren<RoomCameraTrigger>(true);
        if (existingTrigger != null)
        {
            return existingTrigger.gameObject;
        }

        Transform colliderChild = FindFirstChildWithComponent<Collider2D>(areaObject.transform);
        if (colliderChild != null)
        {
            return colliderChild.gameObject;
        }

        Transform collider3DChild = FindFirstChildWithComponent<Collider>(areaObject.transform);
        if (collider3DChild != null)
        {
            return collider3DChild.gameObject;
        }

        GameObject colliderObject = new GameObject($"{ColliderPrefix}{areaObject.name}");
        Undo.RegisterCreatedObjectUndo(colliderObject, "Create Area Collider");
        colliderObject.transform.SetParent(areaObject.transform, false);
        return colliderObject;
    }

    private static Collider2D ResolveOrCreateAreaBounds(GameObject triggerHost)
    {
        Collider2D existingCollider = triggerHost.GetComponent<Collider2D>();
        if (existingCollider != null)
        {
            existingCollider.isTrigger = true;
            return existingCollider;
        }

        BoxCollider2D boundsCollider = triggerHost.AddComponent<BoxCollider2D>();
        boundsCollider.isTrigger = true;
        boundsCollider.size = new Vector2(18f, 10f);
        return boundsCollider;
    }

    private static CinemachineCamera ResolveOrCreateRoomCamera(Transform areaTransform)
    {
        string cameraName = $"{CameraPrefix}{areaTransform.name}";
        Transform existingCamera = areaTransform.Find(cameraName);
        if (existingCamera != null)
        {
            CinemachineCamera existingCinemachineCamera = existingCamera.GetComponent<CinemachineCamera>();
            if (existingCinemachineCamera != null)
            {
                return existingCinemachineCamera;
            }

            return Undo.AddComponent<CinemachineCamera>(existingCamera.gameObject);
        }

        Transform cameraChild = FindFirstChildWithComponent<CinemachineCamera>(areaTransform);
        if (cameraChild != null)
        {
            return cameraChild.GetComponent<CinemachineCamera>();
        }

        GameObject cameraObject = new GameObject(cameraName);
        Undo.RegisterCreatedObjectUndo(cameraObject, "Create Area Camera");
        cameraObject.transform.SetParent(areaTransform, false);
        cameraObject.transform.position = new Vector3(
            areaTransform.position.x,
            areaTransform.position.y,
            -10f);

        CinemachineCamera roomCamera = cameraObject.AddComponent<CinemachineCamera>();
        roomCamera.Priority.Enabled = true;
        roomCamera.Priority.Value = 0;
        return roomCamera;
    }

    private static RoomCameraGate EnsureLocalGate(
        Transform areaTransform,
        RoomCameraTrigger targetRoom,
        string targetAreaName,
        Vector2 localPosition,
        Vector2 colliderSize)
    {
        if (areaTransform == null || targetRoom == null || string.IsNullOrWhiteSpace(targetAreaName))
        {
            return null;
        }

        Transform gatesRoot = FindOrCreateChild(areaTransform, LocalGatesRootName);
        string gateName = $"{GatePrefix}{areaTransform.name}_to_{targetAreaName}";
        Transform existingGate = gatesRoot.Find(gateName);
        if (existingGate != null)
        {
            RoomCameraGate existingComponent = existingGate.GetComponent<RoomCameraGate>();
            if (existingComponent != null)
            {
                SerializedObject serializedExistingGate = new SerializedObject(existingComponent);
                serializedExistingGate.FindProperty("targetRoom").objectReferenceValue = targetRoom;
                serializedExistingGate.ApplyModifiedPropertiesWithoutUndo();
                ResizeGateCollider(existingGate.gameObject, colliderSize);
                return existingComponent;
            }

            RoomCameraGate addedComponent = Undo.AddComponent<RoomCameraGate>(existingGate.gameObject);
            SerializedObject existingSerializedGate = new SerializedObject(addedComponent);
            existingSerializedGate.FindProperty("targetRoom").objectReferenceValue = targetRoom;
            existingSerializedGate.ApplyModifiedPropertiesWithoutUndo();
            ResizeGateCollider(existingGate.gameObject, colliderSize);
            return addedComponent;
        }

        GameObject gateObject = new GameObject(gateName);
        Undo.RegisterCreatedObjectUndo(gateObject, "Create Area Gate");
        gateObject.transform.SetParent(gatesRoot, false);
        gateObject.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

        BoxCollider2D gateCollider = gateObject.AddComponent<BoxCollider2D>();
        gateCollider.isTrigger = true;
        gateCollider.size = colliderSize;

        RoomCameraGate gate = gateObject.AddComponent<RoomCameraGate>();
        SerializedObject serializedGate = new SerializedObject(gate);
        serializedGate.FindProperty("targetRoom").objectReferenceValue = targetRoom;
        serializedGate.ApplyModifiedPropertiesWithoutUndo();
        return gate;
    }

    private static void SetupConnectionGatesForSingleArea(Transform areaTransform)
    {
        if (areaTransform == null)
        {
            return;
        }

        Transform areaRoot = ResolveAreaRoot(areaTransform);
        List<Transform> siblings = GetSortedAreaChildren(areaRoot);
        int index = siblings.IndexOf(areaTransform);
        if (index < 0)
        {
            return;
        }

        int gateCount = 0;
        Transform previous = FindPreviousSequentialArea(siblings, index);
        if (previous != null)
        {
            SetupAreaObject(previous.gameObject);
            EnsureGateToArea(areaTransform, previous);
            gateCount++;
        }

        Transform next = FindNextSequentialArea(siblings, index);
        if (next != null)
        {
            SetupAreaObject(next.gameObject);
            EnsureGateToArea(areaTransform, next);
            gateCount++;
        }

        if (gateCount == 0)
        {
            RoomCameraTrigger roomTrigger = ResolveRoomTrigger(areaTransform.gameObject);
            EnsureLocalGate(areaTransform, roomTrigger, areaTransform.name, new Vector2(9f, 0f), new Vector2(2f, 4f));
        }
    }

    private static int SetupSequentialConnectionGates(List<Transform> areaChildren)
    {
        int gateCount = 0;
        for (int i = 0; i < areaChildren.Count; i++)
        {
            Transform current = areaChildren[i];
            Transform previous = FindPreviousSequentialArea(areaChildren, i);
            if (previous != null)
            {
                EnsureGateToArea(current, previous);
                gateCount++;
            }

            Transform next = FindNextSequentialArea(areaChildren, i);
            if (next != null)
            {
                EnsureGateToArea(current, next);
                gateCount++;
            }
        }

        return gateCount;
    }

    private static int SetupGatesFromMinimapLinks(Transform areaRoot)
    {
        MinimapLink[] links = Object.FindObjectsByType<MinimapLink>(FindObjectsSortMode.None);
        int gateCount = 0;
        for (int i = 0; i < links.Length; i++)
        {
            MinimapLink link = links[i];
            if (link == null || !link.IsValid)
            {
                continue;
            }

            Transform fromArea = FindAreaByRoomId(areaRoot, link.FromRoom.RoomId);
            Transform toArea = FindAreaByRoomId(areaRoot, link.ToRoom.RoomId);
            if (fromArea == null || toArea == null)
            {
                continue;
            }

            EnsureGateToArea(fromArea, toArea);
            EnsureGateToArea(toArea, fromArea);
            gateCount += 2;
        }

        return gateCount;
    }

    private static RoomCameraGate EnsureGateToArea(Transform sourceArea, Transform targetArea)
    {
        if (sourceArea == null || targetArea == null)
        {
            return null;
        }

        RoomCameraTrigger targetRoom = ResolveRoomTrigger(targetArea.gameObject);
        Vector2 localPosition = GetGateLocalPosition(sourceArea, targetArea);
        Vector2 colliderSize = Mathf.Abs(localPosition.x) >= Mathf.Abs(localPosition.y)
            ? new Vector2(2f, 4f)
            : new Vector2(4f, 2f);

        return EnsureLocalGate(sourceArea, targetRoom, targetArea.name, localPosition, colliderSize);
    }

    private static void ResizeGateCollider(GameObject gateObject, Vector2 colliderSize)
    {
        BoxCollider2D gateCollider = gateObject.GetComponent<BoxCollider2D>();
        if (gateCollider == null)
        {
            gateCollider = Undo.AddComponent<BoxCollider2D>(gateObject);
        }

        gateCollider.isTrigger = true;
        gateCollider.size = colliderSize;
    }

    private static Vector2 GetGateLocalPosition(Transform sourceArea, Transform targetArea)
    {
        if (TryGetAreaCenter(sourceArea, out Vector3 sourceCenter) &&
            TryGetAreaCenter(targetArea, out Vector3 targetCenter))
        {
            Vector2 direction = targetCenter - sourceCenter;
            if (direction.sqrMagnitude > 0.01f)
            {
                if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                {
                    return direction.x >= 0f ? new Vector2(9f, 0f) : new Vector2(-9f, 0f);
                }

                return direction.y >= 0f ? new Vector2(0f, 5f) : new Vector2(0f, -5f);
            }
        }

        return CompareAreaNames(sourceArea, targetArea) <= 0 ? new Vector2(9f, 0f) : new Vector2(-9f, 0f);
    }

    private static bool TryGetAreaCenter(Transform areaTransform, out Vector3 center)
    {
        center = Vector3.zero;
        if (areaTransform == null)
        {
            return false;
        }

        Collider2D collider2D = areaTransform.GetComponentInChildren<Collider2D>(true);
        if (collider2D != null)
        {
            center = collider2D.bounds.center;
            return true;
        }

        Collider collider = areaTransform.GetComponentInChildren<Collider>(true);
        if (collider != null)
        {
            center = collider.bounds.center;
            return true;
        }

        center = areaTransform.position;
        return true;
    }

    private static bool ShouldSkipAreaChild(Transform child)
    {
        return child == null ||
               child.name == GlobalGatesRootName ||
               child.name.StartsWith("Camera", System.StringComparison.OrdinalIgnoreCase) ||
               child.name.StartsWith(PortalPrefix, System.StringComparison.OrdinalIgnoreCase) ||
               child.name.StartsWith(FallPortalPrefix, System.StringComparison.OrdinalIgnoreCase) ||
               child.name.StartsWith(VerticalPortalPrefix, System.StringComparison.OrdinalIgnoreCase) ||
               child.GetComponent<RoomCameraPortal>() != null ||
               child.GetComponent<FallRoomCameraPortal>() != null ||
               child.GetComponent<VerticalRoomCameraPortal>() != null;
    }

    private static List<Transform> GetSortedAreaChildren(Transform root)
    {
        var areaChildren = new List<Transform>();
        if (root == null)
        {
            return areaChildren;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!ShouldSkipAreaChild(child))
            {
                areaChildren.Add(child);
            }
        }

        areaChildren.Sort(CompareAreaNames);
        return areaChildren;
    }

    private static int CompareAreaNames(Transform left, Transform right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        bool leftIsGrid = TryParseAreaName(left.name, out int leftMajor, out int leftMinor);
        bool rightIsGrid = TryParseAreaName(right.name, out int rightMajor, out int rightMinor);
        if (leftIsGrid && rightIsGrid)
        {
            int majorCompare = leftMajor.CompareTo(rightMajor);
            return majorCompare != 0 ? majorCompare : leftMinor.CompareTo(rightMinor);
        }

        if (leftIsGrid)
        {
            return -1;
        }

        if (rightIsGrid)
        {
            return 1;
        }

        return string.CompareOrdinal(left.name, right.name);
    }

    private static Transform FindPreviousSequentialArea(List<Transform> areaChildren, int index)
    {
        if (areaChildren == null || index <= 0 || index >= areaChildren.Count)
        {
            return null;
        }

        Transform current = areaChildren[index];
        Transform previous = areaChildren[index - 1];
        return IsSequentialNeighbor(current, previous) ? previous : null;
    }

    private static Transform FindNextSequentialArea(List<Transform> areaChildren, int index)
    {
        if (areaChildren == null || index < 0 || index >= areaChildren.Count - 1)
        {
            return null;
        }

        Transform current = areaChildren[index];
        Transform next = areaChildren[index + 1];
        return IsSequentialNeighbor(current, next) ? next : null;
    }

    private static bool IsSequentialNeighbor(Transform source, Transform target)
    {
        if (source == null || target == null)
        {
            return false;
        }

        bool sourceIsGrid = TryParseAreaName(source.name, out int sourceMajor, out int sourceMinor);
        bool targetIsGrid = TryParseAreaName(target.name, out int targetMajor, out int targetMinor);
        if (!sourceIsGrid || !targetIsGrid)
        {
            return true;
        }

        return sourceMajor == targetMajor && Mathf.Abs(sourceMinor - targetMinor) == 1;
    }

    private static Transform FindAreaByRoomId(Transform areaRoot, string roomId)
    {
        if (areaRoot == null || string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        string areaName = roomId.StartsWith(ColliderPrefix, System.StringComparison.OrdinalIgnoreCase)
            ? roomId.Substring(ColliderPrefix.Length)
            : roomId;

        Transform direct = areaRoot.Find(areaName);
        if (direct != null)
        {
            return direct;
        }

        if (TryCreateDisplayAreaName(areaName, out string displayAreaName))
        {
            direct = areaRoot.Find(displayAreaName);
            if (direct != null)
            {
                return direct;
            }
        }

        if (TryParseNumericAreaName(areaName, out int major, out int minor))
        {
            return areaRoot.Find($"{major}-{minor}");
        }

        return null;
    }

    private static void SetObjectReferenceArray(SerializedProperty property, Object value)
    {
        property.arraySize = value != null ? 1 : 0;
        if (value != null)
        {
            property.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }
    }

    private static Transform ResolveAreaRoot(Transform preferred = null)
    {
        Transform current = preferred != null ? preferred : Selection.activeTransform;
        Transform areaRoot = FindAncestor(current, AreaRootName);
        if (areaRoot != null)
        {
            return areaRoot;
        }

        if (current != null && current.name == GlobalGatesRootName && current.parent != null)
        {
            return current.parent;
        }

        Transform childArea = FindDirectChild(current, AreaRootName);
        if (childArea != null)
        {
            return childArea;
        }

        if (current != null)
        {
            return current;
        }

        GameObject areaObject = GameObject.Find(AreaRootName);
        if (areaObject != null)
        {
            return areaObject.transform;
        }

        GameObject root = new GameObject(AreaRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Area");
        return root.transform;
    }

    private static Transform FindAncestor(Transform transform, string name)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == name)
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform transform, string name)
    {
        if (transform == null)
        {
            return null;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform FindFirstChildWithComponent<T>(Transform parent) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.GetComponent<T>() != null)
            {
                return child;
            }
        }

        return null;
    }

    private static string CreateUniqueChildName(Transform parent, string baseName)
    {
        int index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_{index:00}";
            index++;
        }
        while (parent.Find(candidate) != null);

        return candidate;
    }

    private static string CreateNextAreaName(Transform areaRoot)
    {
        Transform selectedArea = Selection.activeTransform;
        if (selectedArea != null && selectedArea.parent == areaRoot)
        {
            if (TryCreateNextGridName(areaRoot, selectedArea.name, out string nextGridName))
            {
                return nextGridName;
            }
        }

        if (TryCreateNextGridNameFromChildren(areaRoot, out string childGridName))
        {
            return childGridName;
        }

        return CreateUniqueChildName(areaRoot, "Area");
    }

    private static bool TryCreateNextGridNameFromChildren(Transform areaRoot, out string nextName)
    {
        int bestMajor = int.MinValue;
        int bestMinor = int.MinValue;

        for (int i = 0; i < areaRoot.childCount; i++)
        {
            Transform child = areaRoot.GetChild(i);
            if (!TryParseAreaName(child.name, out int major, out int minor))
            {
                continue;
            }

            if (major > bestMajor || major == bestMajor && minor > bestMinor)
            {
                bestMajor = major;
                bestMinor = minor;
            }
        }

        if (bestMajor == int.MinValue)
        {
            nextName = string.Empty;
            return false;
        }

        nextName = CreateNextAvailableGridName(areaRoot, bestMajor, bestMinor + 1);
        return true;
    }

    private static bool TryCreateNextGridName(Transform areaRoot, string currentName, out string nextName)
    {
        if (!TryParseAreaName(currentName, out int major, out int minor))
        {
            nextName = string.Empty;
            return false;
        }

        nextName = CreateNextAvailableGridName(areaRoot, major, minor + 1);
        return true;
    }

    private static string CreateNextAvailableGridName(Transform areaRoot, int major, int firstMinor)
    {
        int minor = Mathf.Max(1, firstMinor);
        bool useDisplayNumber = UsesDisplayNumberScheme(areaRoot);
        string candidate;
        do
        {
            candidate = FormatAreaName(major, minor, useDisplayNumber);
            minor++;
        }
        while (areaRoot.Find(candidate) != null);

        return candidate;
    }

    private static bool UsesDisplayNumberScheme(Transform areaRoot)
    {
        if (areaRoot == null)
        {
            return true;
        }

        for (int i = 0; i < areaRoot.childCount; i++)
        {
            Transform child = areaRoot.GetChild(i);
            if (child != null && TryParseNumericAreaName(child.name, out _, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatAreaName(int major, int minor, bool useDisplayNumber)
    {
        return useDisplayNumber ? $"{major}{minor:00}" : $"{major}-{minor}";
    }

    private static bool TryCreateDisplayAreaName(string name, out string displayName)
    {
        if (TryParseAreaName(name, out int major, out int minor))
        {
            displayName = FormatAreaName(major, minor, true);
            return true;
        }

        displayName = string.Empty;
        return false;
    }

    private static bool TryCreateDisplayGateName(string gateName, out string displayGateName)
    {
        displayGateName = string.Empty;
        if (string.IsNullOrWhiteSpace(gateName) ||
            !gateName.StartsWith(GatePrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string body = gateName.Substring(GatePrefix.Length);
        string[] parts = body.Split(new[] { "_to_" }, System.StringSplitOptions.None);
        if (parts.Length != 2 ||
            !TryCreateDisplayAreaName(parts[0], out string fromDisplayName) ||
            !TryCreateDisplayAreaName(parts[1], out string toDisplayName))
        {
            return false;
        }

        displayGateName = $"{GatePrefix}{fromDisplayName}_to_{toDisplayName}";
        return true;
    }

    private static bool TryParseAreaName(string name, out int major, out int minor)
    {
        return TryParseGridName(name, out major, out minor) ||
               TryParseNumericAreaName(name, out major, out minor);
    }

    private static bool TryParseGridName(string name, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string[] parts = name.Split('-');
        return parts.Length == 2 &&
               int.TryParse(parts[0], out major) &&
               int.TryParse(parts[1], out minor);
    }

    private static bool TryParseNumericAreaName(string name, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrWhiteSpace(name) ||
            name.Length != 3 ||
            !int.TryParse(name, out int value))
        {
            return false;
        }

        major = value / 100;
        minor = value % 100;
        return major > 0 && minor > 0;
    }

    private static Vector3 GetCreationPosition()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return Vector3.zero;
        }

        Vector3 position = sceneView.pivot;
        position.z = 0f;
        return position;
    }
}
