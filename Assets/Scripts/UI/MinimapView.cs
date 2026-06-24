using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MinimapView : MonoBehaviour
{
    private const string MiniMapBackgroundName = "MiniMapBackGround";
    private const float MainCameraRefreshInterval = 0.5f;
    private const float PlayerReferenceSearchInterval = 0.25f;
    private static readonly List<RectTransform> RectTransformSearchBuffer = new List<RectTransform>(32);

    [Header("Minimap Panel")]
    [SerializeField] private Vector2 miniMapSize = new Vector2(290f, 170f);
    [SerializeField] private Vector2 miniMapOffset = new Vector2(-80f, -70f);
    [SerializeField, Min(0f)] private float miniMapContentPadding = 14f;
    [SerializeField, Min(0f)] private float miniBoardScale = 42f;
    [SerializeField, Min(0f)] private float miniLineThickness = 3f;
    [SerializeField, Min(0f)] private float miniRoomBorderThickness = 3f;
    [SerializeField, Min(0f)] private float miniMarkerDiameter = 12f;

    [Header("Full Map Panel")]
    [SerializeField] private Vector2 fullMapSize = new Vector2(1620f, 800f);
    [SerializeField] private float fullMapScale = 0.8f;
    [SerializeField, Min(0f)] private float fullMapContentPadding = 22f;
    [SerializeField, Min(0f)] private float fullBoardScale = 87f;
    [SerializeField, Range(0.1f, 1f)] private float fullMapFitViewportRatio = 0.92f;
    [SerializeField, Min(0f)] private float fullLineThickness = 6f;
    [SerializeField, Min(0f)] private float fullRoomBorderThickness = 6f;
    [SerializeField, Min(0f)] private float fullMarkerDiameter = 24f;

    [Header("Shared")]
    [SerializeField, Min(0f)] private float connectorEndInset = 4f;
    [SerializeField] private Color panelColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
    [SerializeField] private Color fullMapPanelColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);
    [SerializeField] private Color visitedColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color currentRoomBorderColor = new Color(0.12f, 0.95f, 0.72f, 1f);
    [SerializeField] private Color currentRoomFillColor = new Color(0.04f, 0.42f, 0.32f, 0.78f);
    [SerializeField] private Color currentMarkerColor = Color.white;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.72f);
    [SerializeField] private float minimapFollowSmoothTime = 0.22f;

    [Header("Player Overlap Fade")]
    [SerializeField] private bool enablePlayerOverlapFade = true;
    [SerializeField, Range(0f, 1f)] private float occludedAlpha = 0.25f;
    [SerializeField, Min(0f)] private float fadeSpeed = 1f;
    [SerializeField, Min(0f)] private float overlapPaddingPixels = 80f;

    private readonly List<GameObject> generatedObjects = new List<GameObject>();
    private readonly List<MinimapRoomDefinition> fullMapAreaRooms = new List<MinimapRoomDefinition>();
    private readonly Vector3[] miniMapWorldCorners = new Vector3[4];
    private MinimapManager manager;
    private RectTransform miniMapPanel;
    private RectTransform miniMapContent;
    private RectTransform fullMapPanel;
    private RectTransform fullMapContent;
    private CanvasGroup miniMapCanvasGroup;
    private CanvasGroup miniMapBackgroundCanvasGroup;
    private PlayerController cachedPlayer;
    private Collider2D cachedPlayerCollider;
    private Camera cachedMainCamera;
    private float nextMainCameraRefreshTime;
    private float nextPlayerReferenceSearchTime;
    private Sprite whiteSprite;
    private Sprite circleSprite;
    private Vector2 miniMapOrigin;
    private Vector2 miniMapOriginVelocity;
    private Vector2 miniMapTargetOrigin;
    private Rect miniMapBounds;
    private bool hasMiniMapOrigin;
    private Vector2 lastDrawnMiniMapOrigin;
    private bool hasDrawnMiniMapOrigin;

    public bool IsMiniMapVisible => miniMapPanel != null && miniMapPanel.gameObject.activeSelf;
    public bool IsFullMapVisible => fullMapPanel != null && fullMapPanel.gameObject.activeSelf;

    public void Initialize(MinimapManager minimapManager)
    {
        if (manager == minimapManager && miniMapPanel != null && fullMapPanel != null)
        {
            return;
        }

        if (manager != null)
        {
            manager.Changed -= Refresh;
        }

        manager = minimapManager;
        manager.Changed += Refresh;

        EnsureSprite();
        EnsureCircleSprite();
        BuildCanvasObjects();
        Refresh();
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.Changed -= Refresh;
        }
    }

    private void Update()
    {
        if (!hasMiniMapOrigin || miniMapContent == null)
        {
            return;
        }

        miniMapOrigin = Vector2.SmoothDamp(
            miniMapOrigin,
            miniMapTargetOrigin,
            ref miniMapOriginVelocity,
            minimapFollowSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if ((miniMapOrigin - miniMapTargetOrigin).sqrMagnitude < 0.01f)
        {
            miniMapOrigin = miniMapTargetOrigin;
            miniMapOriginVelocity = Vector2.zero;
        }

        DrawMiniMapAtCurrentOrigin();
    }

    private void LateUpdate()
    {
        UpdateMiniMapOverlapFade();
    }

    private void OnValidate()
    {
        miniMapSize = ClampPositiveSize(miniMapSize);
        fullMapSize = ClampPositiveSize(fullMapSize);
        fullMapScale = Mathf.Max(0.01f, fullMapScale);

        ApplyGeneratedLayout();
        if (Application.isPlaying && manager != null && miniMapContent != null && fullMapContent != null)
        {
            hasDrawnMiniMapOrigin = false;
            Refresh();
        }
    }

    public void ToggleFullMap()
    {
        if (fullMapPanel == null)
        {
            return;
        }

        SetFullMapVisible(!fullMapPanel.gameObject.activeSelf);
    }

    public void SetFullMapVisible(bool visible)
    {
        SetPanelVisibility(!visible, visible);
    }

    public void SetPanelVisibility(bool miniVisible, bool fullVisible)
    {
        bool changed = false;

        if (miniMapPanel != null && miniMapPanel.gameObject.activeSelf != miniVisible)
        {
            miniMapPanel.gameObject.SetActive(miniVisible);
            changed = true;
        }

        if (fullMapPanel != null && fullMapPanel.gameObject.activeSelf != fullVisible)
        {
            fullMapPanel.gameObject.SetActive(fullVisible);
            changed = true;
        }

        if (SetMiniMapBackgroundVisible(miniVisible))
        {
            changed = true;
        }

        if (changed || fullVisible)
        {
            Refresh();
        }
    }

    private void BuildCanvasObjects()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            canvas = CreateCanvas();
        }

        RectTransform root = CreateRect("GeneratedMapUI", canvas.transform);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        miniMapPanel = CreatePanel("MiniMapPanel", root, new Vector2(1f, 1f), new Vector2(1f, 1f), miniMapSize, miniMapOffset);
        miniMapPanel.gameObject.AddComponent<RectMask2D>();
        miniMapCanvasGroup = EnsureCanvasGroup(miniMapPanel.gameObject);
        miniMapBackgroundCanvasGroup = FindMiniMapBackgroundCanvasGroup(canvas);
        miniMapContent = CreateRect("Content", miniMapPanel);
        Stretch(miniMapContent, miniMapContentPadding);

        fullMapPanel = CreatePanel("FullMapPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), fullMapSize, Vector2.zero, fullMapPanelColor);
        fullMapPanel.localScale = new Vector3(fullMapScale, fullMapScale, 1f);
        Canvas fullMapOverrideCanvas = fullMapPanel.gameObject.AddComponent<Canvas>();
        fullMapOverrideCanvas.overrideSorting = true;
        fullMapOverrideCanvas.sortingOrder = 300;
        fullMapPanel.gameObject.AddComponent<GraphicRaycaster>();
        fullMapContent = CreateRect("Content", fullMapPanel);
        Stretch(fullMapContent, fullMapContentPadding);
        fullMapPanel.gameObject.SetActive(false);
        ApplyGeneratedLayout();
    }

    private void ApplyGeneratedLayout()
    {
        if (miniMapPanel != null)
        {
            miniMapPanel.sizeDelta = miniMapSize;
            miniMapPanel.anchoredPosition = miniMapOffset;
        }

        if (miniMapContent != null)
        {
            Stretch(miniMapContent, miniMapContentPadding);
        }

        if (fullMapPanel != null)
        {
            fullMapPanel.sizeDelta = fullMapSize;
            fullMapPanel.localScale = new Vector3(fullMapScale, fullMapScale, 1f);
        }

        if (fullMapContent != null)
        {
            Stretch(fullMapContent, fullMapContentPadding);
        }
    }

    private void Refresh()
    {
        if (manager == null || miniMapContent == null || fullMapContent == null)
        {
            return;
        }

        ClearGenerated();
        UpdateMiniMapTargetOrigin();
        DrawMiniMapAtCurrentOrigin();

        if (fullMapPanel != null && fullMapPanel.gameObject.activeSelf)
        {
            DrawMap(
                fullMapContent,
                ResolveFullMapRooms(),
                fullBoardScale,
                fullLineThickness,
                fullRoomBorderThickness,
                false,
                fullMarkerDiameter);
        }
    }

    private void DrawMiniMapAtCurrentOrigin()
    {
        if (manager == null || miniMapContent == null || !hasMiniMapOrigin)
        {
            return;
        }

        if (hasDrawnMiniMapOrigin && (lastDrawnMiniMapOrigin - miniMapOrigin).sqrMagnitude < 0.0001f)
        {
            return;
        }

        ClearGeneratedUnder(miniMapContent);
        DrawMapAtOrigin(
            miniMapContent,
            manager.RoomDefinitions,
            miniBoardScale,
            miniLineThickness,
            miniRoomBorderThickness,
            miniMarkerDiameter,
            miniMapBounds,
            miniMapOrigin);

        lastDrawnMiniMapOrigin = miniMapOrigin;
        hasDrawnMiniMapOrigin = true;
    }

    private void UpdateMiniMapTargetOrigin()
    {
        IReadOnlyList<MinimapRoomDefinition> rooms = manager.RoomDefinitions;
        if (rooms == null || rooms.Count == 0)
        {
            return;
        }

        miniMapBounds = CalculateBounds(rooms);
        miniMapTargetOrigin = CalculateOrigin(miniMapBounds, miniBoardScale, true);

        if (!hasMiniMapOrigin)
        {
            miniMapOrigin = miniMapTargetOrigin;
            miniMapOriginVelocity = Vector2.zero;
            hasMiniMapOrigin = true;
        }

        hasDrawnMiniMapOrigin = false;
    }

    private void DrawMap(
        RectTransform parent,
        IReadOnlyList<MinimapRoomDefinition> rooms,
        float boardScale,
        float lineThickness,
        float roomBorderThickness,
        bool centerOnCurrentRoom,
        float markerDiameter)
    {
        if (rooms == null || rooms.Count == 0)
        {
            return;
        }

        Rect bounds = CalculateDrawableBounds(rooms);
        float resolvedBoardScale = CalculateFittedBoardScale(parent, bounds, boardScale);
        Vector2 origin = CalculateOrigin(bounds, resolvedBoardScale, centerOnCurrentRoom);
        DrawMapAtOrigin(
            parent,
            rooms,
            resolvedBoardScale,
            lineThickness,
            roomBorderThickness,
            markerDiameter,
            bounds,
            origin);
    }

    private void DrawMapAtOrigin(
        RectTransform parent,
        IReadOnlyList<MinimapRoomDefinition> rooms,
        float boardScale,
        float lineThickness,
        float roomBorderThickness,
        float markerDiameter,
        Rect bounds,
        Vector2 origin)
    {
        if (rooms == null || rooms.Count == 0)
        {
            return;
        }

        DrawConnections(parent, rooms, bounds, origin, boardScale, lineThickness);

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId) || !ShouldDrawRoom(room))
            {
                continue;
            }

            DrawBorderRoom(parent, room, origin, bounds, boardScale, roomBorderThickness);
        }

        DrawCurrentMarker(parent, rooms, origin, bounds, boardScale, markerDiameter);
    }

    private IReadOnlyList<MinimapRoomDefinition> ResolveFullMapRooms()
    {
        IReadOnlyList<MinimapRoomDefinition> rooms = manager.RoomDefinitions;
        if (rooms == null || rooms.Count == 0)
        {
            return rooms;
        }

        if (!TryGetAreaKey(manager.CurrentRoomId, out char currentAreaKey))
        {
            return rooms;
        }

        fullMapAreaRooms.Clear();
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (RoomBelongsToArea(room, currentAreaKey))
            {
                fullMapAreaRooms.Add(room);
            }
        }

        return fullMapAreaRooms.Count > 0 ? fullMapAreaRooms : rooms;
    }

    private static bool RoomBelongsToArea(MinimapRoomDefinition room, char areaKey)
    {
        return room != null &&
            TryGetAreaKey(room.RoomId, out char roomAreaKey) &&
            roomAreaKey == areaKey;
    }

    private static bool TryGetAreaKey(string roomId, out char areaKey)
    {
        areaKey = '\0';
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        string trimmedRoomId = roomId.Trim();
        for (int i = 0; i < trimmedRoomId.Length; i++)
        {
            char candidate = trimmedRoomId[i];
            if (char.IsDigit(candidate))
            {
                areaKey = candidate;
                return true;
            }
        }

        return false;
    }

    private bool ShouldDrawRoom(MinimapRoomDefinition room)
    {
        return manager.IsVisited(room.RoomId) || manager.IsCurrent(room.RoomId);
    }

    private void DrawBorderRoom(
        RectTransform parent,
        MinimapRoomDefinition room,
        Vector2 origin,
        Rect bounds,
        float boardScale,
        float borderThickness)
    {
        Vector2 center = AreaToAnchored(room.AreaCenter, origin, bounds, boardScale);
        Vector2 size = RoomVisualSize(room, boardScale);
        Color color = RoomColor(room);

        if (manager.IsCurrent(room.RoomId))
        {
            RectTransform fill = CreateImage("Fill_" + room.RoomId, parent, currentRoomFillColor);
            fill.anchoredPosition = center;
            fill.sizeDelta = size;
            generatedObjects.Add(fill.gameObject);
        }

        DrawBorderLine(parent, "Top_" + room.RoomId, color, center + new Vector2(0f, size.y * 0.5f), new Vector2(size.x, borderThickness));
        DrawBorderLine(parent, "Bottom_" + room.RoomId, color, center + new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, borderThickness));
        DrawBorderLine(parent, "Left_" + room.RoomId, color, center + new Vector2(-size.x * 0.5f, 0f), new Vector2(borderThickness, size.y));
        DrawBorderLine(parent, "Right_" + room.RoomId, color, center + new Vector2(size.x * 0.5f, 0f), new Vector2(borderThickness, size.y));
    }

    private void DrawBorderLine(RectTransform parent, string objectName, Color color, Vector2 position, Vector2 size)
    {
        RectTransform line = CreateImage(objectName, parent, color);
        line.anchoredPosition = position;
        line.sizeDelta = size;
        generatedObjects.Add(line.gameObject);
    }

    private void DrawCurrentMarker(
        RectTransform parent,
        IReadOnlyList<MinimapRoomDefinition> rooms,
        Vector2 origin,
        Rect bounds,
        float boardScale,
        float markerDiameter)
    {
        if (string.IsNullOrWhiteSpace(manager.CurrentRoomId) ||
            !TryFindDrawRoom(rooms, manager.CurrentRoomId, out MinimapRoomDefinition currentRoom) ||
            !ShouldDrawRoom(currentRoom))
        {
            return;
        }

        RectTransform marker = CreateImage("CurrentRoomMarker", parent, currentMarkerColor, circleSprite);
        marker.anchoredPosition = AreaToAnchored(currentRoom.AreaCenter, origin, bounds, boardScale);
        marker.sizeDelta = new Vector2(markerDiameter, markerDiameter);
        generatedObjects.Add(marker.gameObject);
    }

    private void DrawConnections(
        RectTransform parent,
        IReadOnlyList<MinimapRoomDefinition> rooms,
        Rect bounds,
        Vector2 origin,
        float boardScale,
        float lineThickness)
    {
        IReadOnlyList<MinimapLinkDefinition> links = manager.LinkDefinitions;
        if (links == null || links.Count == 0)
        {
            return;
        }

        for (int i = 0; i < links.Count; i++)
        {
            MinimapLinkDefinition link = links[i];
            if (link == null ||
                !TryFindDrawRoom(rooms, link.FromRoomId, out MinimapRoomDefinition fromRoom) ||
                !TryFindDrawRoom(rooms, link.ToRoomId, out MinimapRoomDefinition toRoom) ||
                !ShouldDrawRoom(fromRoom) ||
                !ShouldDrawRoom(toRoom))
            {
                continue;
            }

            List<Vector2> boardPoints = BuildBoardPath(link, fromRoom, toRoom);
            for (int pointIndex = 0; pointIndex < boardPoints.Count - 1; pointIndex++)
            {
                Vector2 start = AreaToAnchored(boardPoints[pointIndex], origin, bounds, boardScale);
                Vector2 end = AreaToAnchored(boardPoints[pointIndex + 1], origin, bounds, boardScale);

                Vector2 delta = end - start;
                if (delta.sqrMagnitude <= 0.01f)
                {
                    continue;
                }

                Vector2 direction = delta.normalized;
                Vector2 startInset = pointIndex == 0 ? start + (direction * connectorEndInset) : start;
                Vector2 endInset = pointIndex == boardPoints.Count - 2 ? end - (direction * connectorEndInset) : end;
                DrawLineSegment(parent, "Link_" + i + "_" + pointIndex, startInset, endInset, lineThickness, lineColor);
            }
        }
    }

    private static bool TryFindDrawRoom(
        IReadOnlyList<MinimapRoomDefinition> rooms,
        string roomId,
        out MinimapRoomDefinition room)
    {
        room = null;
        if (rooms == null || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition candidate = rooms[i];
            if (candidate == null ||
                !string.Equals(candidate.RoomId, roomId, StringComparison.Ordinal))
            {
                continue;
            }

            room = candidate;
            return true;
        }

        return false;
    }

    private List<Vector2> BuildBoardPath(
        MinimapLinkDefinition link,
        MinimapRoomDefinition fromRoom,
        MinimapRoomDefinition toRoom)
    {
        var points = new List<Vector2>();
        IReadOnlyList<Vector2> middlePoints = link.PathPoints;

        Vector2 startTarget = middlePoints != null && middlePoints.Count > 0
            ? middlePoints[0]
            : toRoom.AreaCenter;
        Vector2 endTarget = middlePoints != null && middlePoints.Count > 0
            ? middlePoints[middlePoints.Count - 1]
            : fromRoom.AreaCenter;

        points.Add(ClosestPointOnRoom(fromRoom, startTarget));

        if (middlePoints != null)
        {
            for (int i = 0; i < middlePoints.Count; i++)
            {
                points.Add(middlePoints[i]);
            }
        }

        points.Add(ClosestPointOnRoom(toRoom, endTarget));
        return points;
    }

    private void DrawLineSegment(
        RectTransform parent,
        string objectName,
        Vector2 start,
        Vector2 end,
        float thickness,
        Color color)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.1f)
        {
            return;
        }

        RectTransform line = CreateImage(objectName, parent, color);
        line.anchoredPosition = (start + end) * 0.5f;
        line.sizeDelta = new Vector2(length, thickness);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        generatedObjects.Add(line.gameObject);
    }

    private Vector2 ClosestPointOnRoom(MinimapRoomDefinition room, Vector2 target)
    {
        float minX = room.AreaPosition.x;
        float maxX = room.AreaPosition.x + room.AreaSize.x;
        float minY = room.AreaPosition.y;
        float maxY = room.AreaPosition.y + room.AreaSize.y;

        if (target.x < minX)
        {
            return new Vector2(minX, Mathf.Clamp(target.y, minY, maxY));
        }

        if (target.x > maxX)
        {
            return new Vector2(maxX, Mathf.Clamp(target.y, minY, maxY));
        }

        if (target.y < minY)
        {
            return new Vector2(Mathf.Clamp(target.x, minX, maxX), minY);
        }

        if (target.y > maxY)
        {
            return new Vector2(Mathf.Clamp(target.x, minX, maxX), maxY);
        }

        float leftDistance = Mathf.Abs(target.x - minX);
        float rightDistance = Mathf.Abs(maxX - target.x);
        float topDistance = Mathf.Abs(target.y - minY);
        float bottomDistance = Mathf.Abs(maxY - target.y);
        float minimumDistance = Mathf.Min(leftDistance, rightDistance, topDistance, bottomDistance);

        if (Mathf.Approximately(minimumDistance, leftDistance))
        {
            return new Vector2(minX, target.y);
        }

        if (Mathf.Approximately(minimumDistance, rightDistance))
        {
            return new Vector2(maxX, target.y);
        }

        if (Mathf.Approximately(minimumDistance, topDistance))
        {
            return new Vector2(target.x, minY);
        }

        return new Vector2(target.x, maxY);
    }

    private Color RoomColor(MinimapRoomDefinition room)
    {
        if (manager.IsCurrent(room.RoomId))
        {
            return currentRoomBorderColor;
        }

        if (manager.IsVisited(room.RoomId))
        {
            return visitedColor;
        }

        return Color.clear;
    }

    private Rect CalculateBounds(IReadOnlyList<MinimapRoomDefinition> rooms)
    {
        bool hasRoom = false;
        float minX = 0f;
        float minY = 0f;
        float maxX = 0f;
        float maxY = 0f;

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Vector2 position = room.AreaPosition;
            Vector2 size = room.AreaSize;

            if (!hasRoom)
            {
                minX = position.x;
                minY = position.y;
                maxX = position.x + size.x;
                maxY = position.y + size.y;
                hasRoom = true;
                continue;
            }

            minX = Mathf.Min(minX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxX = Mathf.Max(maxX, position.x + size.x);
            maxY = Mathf.Max(maxY, position.y + size.y);
        }

        if (!hasRoom)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private Rect CalculateDrawableBounds(IReadOnlyList<MinimapRoomDefinition> rooms)
    {
        bool hasRoom = false;
        float minX = 0f;
        float minY = 0f;
        float maxX = 0f;
        float maxY = 0f;

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null || !ShouldDrawRoom(room))
            {
                continue;
            }

            Vector2 position = room.AreaPosition;
            Vector2 size = room.AreaSize;

            if (!hasRoom)
            {
                minX = position.x;
                minY = position.y;
                maxX = position.x + size.x;
                maxY = position.y + size.y;
                hasRoom = true;
                continue;
            }

            minX = Mathf.Min(minX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxX = Mathf.Max(maxX, position.x + size.x);
            maxY = Mathf.Max(maxY, position.y + size.y);
        }

        return hasRoom ? Rect.MinMaxRect(minX, minY, maxX, maxY) : CalculateBounds(rooms);
    }

    private float CalculateFittedBoardScale(RectTransform parent, Rect bounds, float preferredBoardScale)
    {
        if (parent == null || bounds.width <= 0f || bounds.height <= 0f)
        {
            return preferredBoardScale;
        }

        Rect parentRect = parent.rect;
        if (parentRect.width <= 0f || parentRect.height <= 0f)
        {
            return preferredBoardScale;
        }

        float fitScale = Mathf.Min(
            parentRect.width * fullMapFitViewportRatio / bounds.width,
            parentRect.height * fullMapFitViewportRatio / bounds.height);

        return Mathf.Min(preferredBoardScale, fitScale);
    }

    private Vector2 CalculateOrigin(Rect bounds, float boardScale, bool centerOnCurrentRoom)
    {
        if (centerOnCurrentRoom &&
            !string.IsNullOrWhiteSpace(manager.CurrentRoomId) &&
            manager.TryGetRoom(manager.CurrentRoomId, out MinimapRoomDefinition currentRoom))
        {
            Vector2 currentCenter = currentRoom.AreaCenter;
            return new Vector2(
                -((currentCenter.x - bounds.xMin) * boardScale),
                (currentCenter.y - bounds.yMin) * boardScale);
        }

        Vector2 mapSize = new Vector2(bounds.width * boardScale, bounds.height * boardScale);
        return new Vector2(-mapSize.x * 0.5f, mapSize.y * 0.5f);
    }

    private Vector2 AreaToAnchored(Vector2 boardPoint, Vector2 origin, Rect bounds, float boardScale)
    {
        float x = origin.x + ((boardPoint.x - bounds.xMin) * boardScale);
        float y = origin.y - ((boardPoint.y - bounds.yMin) * boardScale);
        return new Vector2(x, y);
    }

    private Vector2 RoomVisualSize(MinimapRoomDefinition room, float boardScale)
    {
        return new Vector2(
            Mathf.Max(12f, room.AreaSize.x * boardScale),
            Mathf.Max(8f, room.AreaSize.y * boardScale));
    }

    private void UpdateMiniMapOverlapFade()
    {
        if (miniMapPanel == null || miniMapCanvasGroup == null)
        {
            return;
        }

        float targetAlpha = 1f;
        if (enablePlayerOverlapFade && IsPlayerTouchingMiniMap())
        {
            targetAlpha = Mathf.Clamp01(occludedAlpha);
        }

        if (fadeSpeed <= 0f)
        {
            SetMiniMapFadeAlpha(targetAlpha);
            return;
        }

        float alpha = Mathf.MoveTowards(
            miniMapCanvasGroup.alpha,
            targetAlpha,
            fadeSpeed * Time.unscaledDeltaTime);
        SetMiniMapFadeAlpha(alpha);
    }

    private bool IsPlayerTouchingMiniMap()
    {
        if (!TryGetMainCamera(out Camera mainCamera) ||
            !TryResolvePlayerCollider(out Collider2D playerCollider))
        {
            return false;
        }

        Rect playerScreenRect = CalculatePlayerScreenRect(playerCollider.bounds, mainCamera);
        Rect miniMapScreenRect = CalculateMiniMapScreenRect();
        miniMapScreenRect = ExpandRect(miniMapScreenRect, overlapPaddingPixels);
        return RectsTouchOrOverlap(playerScreenRect, miniMapScreenRect);
    }

    private void SetMiniMapFadeAlpha(float alpha)
    {
        miniMapCanvasGroup.alpha = alpha;

        if (miniMapBackgroundCanvasGroup == null)
        {
            miniMapBackgroundCanvasGroup = FindMiniMapBackgroundCanvasGroup();
        }

        if (miniMapBackgroundCanvasGroup != null)
        {
            miniMapBackgroundCanvasGroup.alpha = alpha;
        }
    }

    private bool SetMiniMapBackgroundVisible(bool visible)
    {
        if (miniMapBackgroundCanvasGroup == null)
        {
            miniMapBackgroundCanvasGroup = FindMiniMapBackgroundCanvasGroup();
        }

        if (miniMapBackgroundCanvasGroup == null ||
            miniMapBackgroundCanvasGroup.gameObject.activeSelf == visible)
        {
            return false;
        }

        miniMapBackgroundCanvasGroup.gameObject.SetActive(visible);
        return true;
    }

    private bool TryResolvePlayerCollider(out Collider2D playerCollider)
    {
        if (cachedPlayer == null || !cachedPlayer.isActiveAndEnabled)
        {
            cachedPlayer = null;
            cachedPlayerCollider = null;

            if (Time.unscaledTime < nextPlayerReferenceSearchTime)
            {
                playerCollider = null;
                return false;
            }

            nextPlayerReferenceSearchTime = Time.unscaledTime + PlayerReferenceSearchInterval;
            cachedPlayer = global::PlayerReferenceCache.GetController();
        }

        if (cachedPlayer == null)
        {
            playerCollider = null;
            return false;
        }

        if (cachedPlayerCollider == null)
        {
            cachedPlayerCollider = cachedPlayer.GetComponent<Collider2D>();
        }

        playerCollider = cachedPlayerCollider;
        return playerCollider != null && playerCollider.enabled;
    }

    private bool TryGetMainCamera(out Camera mainCamera)
    {
        if (cachedMainCamera != null &&
            cachedMainCamera.isActiveAndEnabled &&
            Time.unscaledTime < nextMainCameraRefreshTime)
        {
            mainCamera = cachedMainCamera;
            return true;
        }

        nextMainCameraRefreshTime = Time.unscaledTime + MainCameraRefreshInterval;
        cachedMainCamera = MainCameraCache.Get();
        mainCamera = cachedMainCamera;
        return mainCamera != null;
    }

    private Rect CalculatePlayerScreenRect(Bounds bounds, Camera mainCamera)
    {
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        AddWorldPointToScreenRect(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z), mainCamera, ref minX, ref minY, ref maxX, ref maxY);
        AddWorldPointToScreenRect(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z), mainCamera, ref minX, ref minY, ref maxX, ref maxY);
        AddWorldPointToScreenRect(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z), mainCamera, ref minX, ref minY, ref maxX, ref maxY);
        AddWorldPointToScreenRect(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z), mainCamera, ref minX, ref minY, ref maxX, ref maxY);

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static void AddWorldPointToScreenRect(
        Vector3 worldPoint,
        Camera mainCamera,
        ref float minX,
        ref float minY,
        ref float maxX,
        ref float maxY)
    {
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
        minX = Mathf.Min(minX, screenPoint.x);
        minY = Mathf.Min(minY, screenPoint.y);
        maxX = Mathf.Max(maxX, screenPoint.x);
        maxY = Mathf.Max(maxY, screenPoint.y);
    }

    private Rect CalculateMiniMapScreenRect()
    {
        Rect screenRect = CalculateScreenRect(miniMapPanel);

        if (miniMapBackgroundCanvasGroup == null)
        {
            miniMapBackgroundCanvasGroup = FindMiniMapBackgroundCanvasGroup();
        }

        RectTransform backgroundRect = miniMapBackgroundCanvasGroup != null
            ? miniMapBackgroundCanvasGroup.transform as RectTransform
            : null;
        if (backgroundRect != null)
        {
            screenRect = UnionRects(screenRect, CalculateScreenRect(backgroundRect));
        }

        return screenRect;
    }

    private Rect CalculateScreenRect(RectTransform rectTransform)
    {
        rectTransform.GetWorldCorners(miniMapWorldCorners);

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera uiCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            if (canvas.worldCamera != null)
            {
                uiCamera = canvas.worldCamera;
            }
            else if (TryGetMainCamera(out Camera mainCamera))
            {
                uiCamera = mainCamera;
            }
        }

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < miniMapWorldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, miniMapWorldCorners[i]);
            minX = Mathf.Min(minX, screenPoint.x);
            minY = Mathf.Min(minY, screenPoint.y);
            maxX = Mathf.Max(maxX, screenPoint.x);
            maxY = Mathf.Max(maxY, screenPoint.y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static Rect UnionRects(Rect a, Rect b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(a.xMin, b.xMin),
            Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax),
            Mathf.Max(a.yMax, b.yMax));
    }

    private static Rect ExpandRect(Rect rect, float padding)
    {
        if (padding <= 0f)
        {
            return rect;
        }

        return Rect.MinMaxRect(
            rect.xMin - padding,
            rect.yMin - padding,
            rect.xMax + padding,
            rect.yMax + padding);
    }

    private static bool RectsTouchOrOverlap(Rect a, Rect b)
    {
        return a.xMin <= b.xMax &&
            a.xMax >= b.xMin &&
            a.yMin <= b.yMax &&
            a.yMax >= b.yMin;
    }

    private static Vector2 ClampPositiveSize(Vector2 size)
    {
        return new Vector2(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y));
    }

    private CanvasGroup FindMiniMapBackgroundCanvasGroup()
    {
        if (miniMapPanel == null)
        {
            return null;
        }

        return FindMiniMapBackgroundCanvasGroup(miniMapPanel.GetComponentInParent<Canvas>());
    }

    private CanvasGroup FindMiniMapBackgroundCanvasGroup(Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        RectTransform background = FindChildRectTransform(canvas.transform, MiniMapBackgroundName);
        if (background == null)
        {
            return null;
        }

        return EnsureCanvasGroup(background.gameObject);
    }

    private static RectTransform FindChildRectTransform(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        RectTransformSearchBuffer.Clear();
        root.GetComponentsInChildren(true, RectTransformSearchBuffer);
        for (int i = 0; i < RectTransformSearchBuffer.Count; i++)
        {
            RectTransform child = RectTransformSearchBuffer[i];
            if (child != null && child.gameObject.name == objectName)
            {
                RectTransformSearchBuffer.Clear();
                return child;
            }
        }

        RectTransformSearchBuffer.Clear();
        return null;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return null;
        }

        if (!targetObject.TryGetComponent(out CanvasGroup canvasGroup))
        {
            canvasGroup = targetObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private Canvas FindCanvas()
    {
        GameObject hud = GameObject.Find("PlayerHUDCanvas");
        if (hud != null && hud.TryGetComponent(out Canvas hudCanvas))
        {
            return hudCanvas;
        }

        return FindFirstObjectByType<Canvas>();
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("GeneratedHUDCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPosition)
    {
        return CreatePanel(objectName, parent, anchorMin, anchorMax, size, anchoredPosition, panelColor);
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        return CreatePanel(objectName, parent, anchorMin, anchorMax, size, anchoredPosition, color, whiteSprite);
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPosition, Sprite sprite)
    {
        return CreatePanel(objectName, parent, anchorMin, anchorMax, size, anchoredPosition, panelColor, sprite);
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPosition, Color color, Sprite sprite)
    {
        RectTransform rect = CreateImage(objectName, parent, color);
        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private RectTransform CreateImage(string objectName, Transform parent, Color color)
    {
        return CreateImage(objectName, parent, color, whiteSprite);
    }

    private RectTransform CreateImage(string objectName, Transform parent, Color color, Sprite sprite)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private void ClearGenerated()
    {
        for (int i = 0; i < generatedObjects.Count; i++)
        {
            if (generatedObjects[i] != null)
            {
                Destroy(generatedObjects[i]);
            }
        }

        generatedObjects.Clear();
    }

    private void ClearGeneratedUnder(Transform parent)
    {
        for (int i = generatedObjects.Count - 1; i >= 0; i--)
        {
            GameObject generatedObject = generatedObjects[i];
            if (generatedObject == null)
            {
                generatedObjects.RemoveAt(i);
                continue;
            }

            if (generatedObject.transform.parent != parent)
            {
                continue;
            }

            Destroy(generatedObject);
            generatedObjects.RemoveAt(i);
        }
    }

    private void EnsureSprite()
    {
        if (whiteSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "GeneratedMinimapWhitePixel";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        whiteSprite.name = "GeneratedMinimapWhiteSprite";
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void EnsureCircleSprite()
    {
        if (circleSprite != null)
        {
            return;
        }

        const int size = 96;
        float radius = (size - 2) * 0.5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GeneratedMinimapCircleMask";

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        circleSprite.name = "GeneratedMinimapCircleSprite";
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
    }
}
