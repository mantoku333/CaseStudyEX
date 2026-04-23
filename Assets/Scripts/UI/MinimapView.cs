using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MinimapView : MonoBehaviour
{
    private const float MiniCellSize = 42f;
    private const float FullCellSize = 87f;
    private const float MiniLineThickness = 3f;
    private const float FullLineThickness = 6f;
    private const float MiniRoomBorderThickness = 3f;
    private const float FullRoomBorderThickness = 6f;
    private const float MiniRoomGap = 18f;
    private const float FullRoomGap = 24f;
    private const float MiniMarkerDiameter = 12f;
    private const float FullMarkerDiameter = 24f;
    private const float RoomVisualWidthRatio = 0.72f;
    private const float RoomVisualHeightRatio = 0.42f;
    private const float ConnectorEndInset = 4f;

    [SerializeField] private Color panelColor = new Color(0.02f, 0.04f, 0.05f, 0.82f);
    [SerializeField] private Color visitedColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color currentRoomBorderColor = new Color(0.12f, 0.95f, 0.72f, 1f);
    [SerializeField] private Color currentRoomFillColor = new Color(0.04f, 0.42f, 0.32f, 0.78f);
    [SerializeField] private Color currentMarkerColor = Color.white;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.72f);
    [SerializeField] private float minimapFollowSmoothTime = 0.22f;

    private readonly List<GameObject> generatedObjects = new List<GameObject>();
    private MinimapManager manager;
    private RectTransform miniMapPanel;
    private RectTransform miniMapContent;
    private RectTransform fullMapPanel;
    private RectTransform fullMapContent;
    private Sprite whiteSprite;
    private Sprite circleSprite;
    private Vector2 miniMapOrigin;
    private Vector2 miniMapOriginVelocity;
    private Vector2 miniMapTargetOrigin;
    private RectInt miniMapBounds;
    private bool hasMiniMapOrigin;
    private Vector2 lastDrawnMiniMapOrigin;
    private bool hasDrawnMiniMapOrigin;

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
        if (fullMapPanel != null)
        {
            fullMapPanel.gameObject.SetActive(visible);
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

        miniMapPanel = CreatePanel("MiniMapPanel", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(280f, 160f), new Vector2(-22f, -22f));
        miniMapPanel.gameObject.AddComponent<RectMask2D>();
        miniMapContent = CreateRect("Content", miniMapPanel);
        Stretch(miniMapContent, 14f);

        fullMapPanel = CreatePanel("FullMapPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1140f, 690f), Vector2.zero);
        fullMapContent = CreateRect("Content", fullMapPanel);
        Stretch(fullMapContent, 22f);
        fullMapPanel.gameObject.SetActive(false);
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
            DrawMap(fullMapContent, FullCellSize, FullLineThickness, FullRoomBorderThickness, FullRoomGap, false, FullMarkerDiameter);
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
            MiniCellSize,
            MiniLineThickness,
            MiniRoomBorderThickness,
            MiniRoomGap,
            MiniMarkerDiameter,
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
        miniMapTargetOrigin = CalculateOrigin(miniMapBounds, MiniCellSize, true);

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
        float cellSize,
        float lineThickness,
        float roomBorderThickness,
        float roomGap,
        bool centerOnCurrentRoom,
        float markerDiameter)
    {
        IReadOnlyList<MinimapRoomDefinition> rooms = manager.RoomDefinitions;
        if (rooms == null || rooms.Count == 0)
        {
            return;
        }

        RectInt bounds = CalculateBounds(rooms);
        Vector2 origin = CalculateOrigin(bounds, cellSize, centerOnCurrentRoom);

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                continue;
            }

            if (!ShouldDrawRoom(room))
            {
                continue;
            }

            DrawConnections(parent, rooms, room, origin, bounds, cellSize, lineThickness, roomGap);
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                continue;
            }

            if (!ShouldDrawRoom(room))
            {
                continue;
            }

            DrawBorderRoom(parent, room, origin, bounds, cellSize, roomBorderThickness, roomGap);
        }

        DrawCurrentMarker(parent, origin, bounds, cellSize, markerDiameter);
    }

    private void DrawMapAtOrigin(
        RectTransform parent,
        float cellSize,
        float lineThickness,
        float roomBorderThickness,
        float roomGap,
        float markerDiameter,
        RectInt bounds,
        Vector2 origin)
    {
        IReadOnlyList<MinimapRoomDefinition> rooms = manager.RoomDefinitions;
        if (rooms == null || rooms.Count == 0)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId) || !ShouldDrawRoom(room))
            {
                continue;
            }

            DrawConnections(parent, rooms, room, origin, bounds, cellSize, lineThickness, roomGap);
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId) || !ShouldDrawRoom(room))
            {
                continue;
            }

            DrawBorderRoom(parent, room, origin, bounds, cellSize, roomBorderThickness, roomGap);
        }

        DrawCurrentMarker(parent, origin, bounds, cellSize, markerDiameter);
    }

    private bool ShouldDrawRoom(MinimapRoomDefinition room)
    {
        return manager.IsVisited(room.RoomId) || manager.IsCurrent(room.RoomId);
    }

    private void DrawBorderRoom(
        RectTransform parent,
        MinimapRoomDefinition room,
        Vector2 origin,
        RectInt bounds,
        float cellSize,
        float borderThickness,
        float roomGap)
    {
        Vector2 center = GridToAnchored(room, origin, bounds, cellSize);
        Vector2 size = RoomVisualSize(room, cellSize, roomGap);
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

    private void DrawCurrentMarker(RectTransform parent, Vector2 origin, RectInt bounds, float cellSize, float markerDiameter)
    {
        if (string.IsNullOrWhiteSpace(manager.CurrentRoomId) ||
            !manager.TryGetRoom(manager.CurrentRoomId, out MinimapRoomDefinition currentRoom) ||
            !ShouldDrawRoom(currentRoom))
        {
            return;
        }

        RectTransform marker = CreateImage("CurrentRoomMarker", parent, currentMarkerColor, circleSprite);
        marker.anchoredPosition = GridToAnchored(currentRoom, origin, bounds, cellSize);
        marker.sizeDelta = new Vector2(markerDiameter, markerDiameter);
        generatedObjects.Add(marker.gameObject);
    }

    private void DrawConnections(
        RectTransform parent,
        IReadOnlyList<MinimapRoomDefinition> rooms,
        MinimapRoomDefinition room,
        Vector2 origin,
        RectInt bounds,
        float cellSize,
        float lineThickness,
        float roomGap)
    {
        TryDrawConnection(parent, rooms, room, MinimapConnection.Right, origin, bounds, cellSize, lineThickness, roomGap);
        TryDrawConnection(parent, rooms, room, MinimapConnection.Left, origin, bounds, cellSize, lineThickness, roomGap);
        TryDrawConnection(parent, rooms, room, MinimapConnection.Up, origin, bounds, cellSize, lineThickness, roomGap);
        TryDrawConnection(parent, rooms, room, MinimapConnection.Down, origin, bounds, cellSize, lineThickness, roomGap);
    }

    private void TryDrawConnection(
        RectTransform parent,
        IReadOnlyList<MinimapRoomDefinition> rooms,
        MinimapRoomDefinition room,
        MinimapConnection direction,
        Vector2 origin,
        RectInt bounds,
        float cellSize,
        float lineThickness,
        float roomGap)
    {
        if ((room.Connections & direction) == 0)
        {
            return;
        }

        MinimapRoomDefinition neighbor = FindNeighbor(rooms, room, direction);
        if (neighbor == null || !ShouldDrawRoom(neighbor))
        {
            return;
        }

        if (string.CompareOrdinal(room.RoomId, neighbor.RoomId) > 0)
        {
            return;
        }

        Vector2 start = RoomEdge(room, direction, origin, bounds, cellSize, roomGap);
        Vector2 end = RoomEdge(neighbor, Opposite(direction), origin, bounds, cellSize, roomGap);
        Vector2 lineDirection = (end - start).normalized;
        start += lineDirection * ConnectorEndInset;
        end -= lineDirection * ConnectorEndInset;
        Vector2 delta = end - start;

        if (delta.sqrMagnitude <= 0.01f)
        {
            return;
        }

        RectTransform line = CreateImage("Line_" + room.RoomId + "_" + direction, parent, lineColor);
        line.anchoredPosition = (start + end) * 0.5f;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            line.sizeDelta = new Vector2(Mathf.Abs(delta.x) + 4f, lineThickness);
        }
        else
        {
            line.sizeDelta = new Vector2(lineThickness, Mathf.Abs(delta.y) + 2f);
        }
        generatedObjects.Add(line.gameObject);
    }

    private MinimapRoomDefinition FindNeighbor(IReadOnlyList<MinimapRoomDefinition> rooms, MinimapRoomDefinition room, MinimapConnection direction)
    {
        RectInt a = ToRect(room);

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition candidate = rooms[i];
            if (candidate == null || ReferenceEquals(candidate, room))
            {
                continue;
            }

            RectInt b = ToRect(candidate);

            if (direction == MinimapConnection.Right && a.xMax == b.xMin && RangesOverlap(a.yMin, a.yMax, b.yMin, b.yMax))
            {
                return candidate;
            }

            if (direction == MinimapConnection.Left && a.xMin == b.xMax && RangesOverlap(a.yMin, a.yMax, b.yMin, b.yMax))
            {
                return candidate;
            }

            if (direction == MinimapConnection.Up && a.yMax == b.yMin && RangesOverlap(a.xMin, a.xMax, b.xMin, b.xMax))
            {
                return candidate;
            }

            if (direction == MinimapConnection.Down && a.yMin == b.yMax && RangesOverlap(a.xMin, a.xMax, b.xMin, b.xMax))
            {
                return candidate;
            }
        }

        return null;
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

    private RectInt CalculateBounds(IReadOnlyList<MinimapRoomDefinition> rooms)
    {
        bool hasRoom = false;
        int minX = 0;
        int minY = 0;
        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoomDefinition room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Vector2Int position = room.MapPosition;
            Vector2Int size = room.MapSize;

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
            return new RectInt(0, 0, 1, 1);
        }

        return new RectInt(minX, minY, Mathf.Max(1, maxX - minX), Mathf.Max(1, maxY - minY));
    }

    private Vector2 CalculateOrigin(RectInt bounds, float cellSize, bool centerOnCurrentRoom)
    {
        if (centerOnCurrentRoom &&
            !string.IsNullOrWhiteSpace(manager.CurrentRoomId) &&
            manager.TryGetRoom(manager.CurrentRoomId, out MinimapRoomDefinition currentRoom))
        {
            return -GridToAnchored(currentRoom, Vector2.zero, bounds, cellSize);
        }

        return CalculateCenteredOrigin(bounds, cellSize);
    }

    private Vector2 CalculateCenteredOrigin(RectInt bounds, float cellSize)
    {
        Vector2 mapSize = new Vector2(bounds.width * cellSize, bounds.height * cellSize);
        return new Vector2(-mapSize.x * 0.5f, -mapSize.y * 0.5f);
    }

    private Vector2 GridToAnchored(MinimapRoomDefinition room, Vector2 origin, RectInt bounds, float cellSize)
    {
        Vector2Int size = room.MapSize;
        float x = origin.x + (room.MapPosition.x - bounds.xMin) * cellSize + size.x * cellSize * 0.5f;
        float y = origin.y + (bounds.yMax - room.MapPosition.y - size.y) * cellSize + size.y * cellSize * 0.5f;
        return new Vector2(x, y);
    }

    private Vector2 RoomEdge(MinimapRoomDefinition room, MinimapConnection direction, Vector2 origin, RectInt bounds, float cellSize, float roomGap)
    {
        Vector2 center = GridToAnchored(room, origin, bounds, cellSize);
        Vector2 half = RoomVisualSize(room, cellSize, roomGap) * 0.5f;

        if (direction == MinimapConnection.Right)
        {
            return center + new Vector2(half.x, 0f);
        }

        if (direction == MinimapConnection.Left)
        {
            return center + new Vector2(-half.x, 0f);
        }

        if (direction == MinimapConnection.Up)
        {
            return center + new Vector2(0f, half.y);
        }

        return center + new Vector2(0f, -half.y);
    }

    private Vector2 RoomVisualSize(MinimapRoomDefinition room, float cellSize, float roomGap)
    {
        if (room.MapSize == Vector2Int.one)
        {
            return new Vector2(
                Mathf.Max(12f, cellSize * RoomVisualWidthRatio),
                Mathf.Max(8f, cellSize * RoomVisualHeightRatio));
        }

        return new Vector2(
            Mathf.Max(8f, room.MapSize.x * cellSize - roomGap),
            Mathf.Max(8f, room.MapSize.y * cellSize - roomGap));
    }

    private RectInt ToRect(MinimapRoomDefinition room)
    {
        return new RectInt(room.MapPosition, room.MapSize);
    }

    private bool RangesOverlap(int aMin, int aMax, int bMin, int bMax)
    {
        return aMin < bMax && bMin < aMax;
    }

    private MinimapConnection Opposite(MinimapConnection direction)
    {
        if (direction == MinimapConnection.Right)
        {
            return MinimapConnection.Left;
        }

        if (direction == MinimapConnection.Left)
        {
            return MinimapConnection.Right;
        }

        if (direction == MinimapConnection.Up)
        {
            return MinimapConnection.Down;
        }

        return MinimapConnection.Up;
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
        return CreatePanel(objectName, parent, anchorMin, anchorMax, size, anchoredPosition, whiteSprite);
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPosition, Sprite sprite)
    {
        RectTransform rect = CreateImage(objectName, parent, panelColor);
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
