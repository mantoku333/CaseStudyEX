using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MinimapEditorWindow : EditorWindow
{
    private const float InspectorWidth = 340f;
    private const float ToolbarHeight = 36f;
    private const float BaseCellSize = 44f;
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 2.5f;
    private const float ResizeHandleSize = 14f;
    private const float HitPadding = 4f;

    private MinimapRoom[] rooms = new MinimapRoom[0];
    private MinimapSceneAuthoringData authoringData;
    private MinimapRoom selectedRoom;
    private int selectedLinkIndex = -1;
    private Vector2 inspectorScroll;
    private Vector2 roomsListScroll;
    private Vector2 linksListScroll;
    private Vector2 canvasPan = Vector2.zero;
    private float zoom = 1f;
    private bool createLinkMode;
    private MinimapRoom pendingLinkFromRoom;
    private MinimapLinkType newLinkType = MinimapLinkType.Line;
    private bool isDraggingRoom;
    private MinimapRoom dragRoom;
    private Vector2 dragStartMousePosition;
    private Vector2Int dragStartRoomPosition;
    private bool isResizingRoom;
    private MinimapRoom resizeRoom;
    private Vector2 resizeStartMousePosition;
    private Vector2Int resizeStartRoomPosition;
    private Vector2Int resizeStartRoomSize;
    private readonly Dictionary<MinimapRoom, Rect> roomRects = new Dictionary<MinimapRoom, Rect>();

    [MenuItem("Tools/Minimap/Editor")]
    public static void Open()
    {
        MinimapEditorWindow window = GetWindow<MinimapEditorWindow>("Minimap Editor");
        window.minSize = new Vector2(980f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshSceneData();
    }

    private void OnHierarchyChange()
    {
        RefreshSceneData();
        Repaint();
    }

    private void OnFocus()
    {
        RefreshSceneData();
    }

    private void OnGUI()
    {
        DrawToolbar();

        Rect inspectorRect = new Rect(position.width - InspectorWidth, ToolbarHeight, InspectorWidth, position.height - ToolbarHeight);
        Rect canvasRect = new Rect(0f, ToolbarHeight, position.width - InspectorWidth, position.height - ToolbarHeight);

        DrawCanvas(canvasRect);
        DrawInspector(inspectorRect);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(ToolbarHeight)))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
            {
                RefreshSceneData();
            }

            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Add MinimapRoom To Selection", EditorStyles.toolbarButton, GUILayout.Width(178f)))
                {
                    AddMinimapRoomToSelection();
                }
            }

            if (GUILayout.Button("Create Link Data", EditorStyles.toolbarButton, GUILayout.Width(108f)))
            {
                EnsureAuthoringData();
            }

            createLinkMode = GUILayout.Toggle(createLinkMode, "Link Create Mode", EditorStyles.toolbarButton, GUILayout.Width(118f));
            if (!createLinkMode)
            {
                pendingLinkFromRoom = null;
            }

            newLinkType = (MinimapLinkType)EditorGUILayout.EnumPopup(newLinkType, EditorStyles.toolbarPopup, GUILayout.Width(110f));

            GUILayout.FlexibleSpace();

            GUILayout.Label("Zoom", GUILayout.Width(36f));
            float nextZoom = GUILayout.HorizontalSlider(zoom, MinZoom, MaxZoom, GUILayout.Width(110f));
            if (!Mathf.Approximately(nextZoom, zoom))
            {
                zoom = nextZoom;
                Repaint();
            }

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(64f)))
            {
                zoom = 1f;
                canvasPan = Vector2.zero;
                Repaint();
            }
        }
    }

    private void DrawCanvas(Rect canvasRect)
    {
        EditorGUI.DrawRect(canvasRect, new Color(0.12f, 0.13f, 0.15f, 1f));
        roomRects.Clear();

        float cellSize = BaseCellSize * zoom;
        Event current = Event.current;

        if (current.type == EventType.ScrollWheel && canvasRect.Contains(current.mousePosition))
        {
            float previousZoom = zoom;
            zoom = Mathf.Clamp(zoom - current.delta.y * 0.03f, MinZoom, MaxZoom);
            if (!Mathf.Approximately(previousZoom, zoom))
            {
                Repaint();
            }

            current.Use();
        }

        if (current.type == EventType.MouseDrag &&
            current.button == 2 &&
            canvasRect.Contains(current.mousePosition))
        {
            canvasPan += current.delta;
            Repaint();
            current.Use();
        }

        DrawGrid(canvasRect, cellSize);
        DrawCanvasLinks(canvasRect, cellSize);
        DrawCanvasRooms(canvasRect, cellSize);
        HandleCanvasInput(canvasRect, cellSize);

        GUI.Label(
            new Rect(canvasRect.x + 10f, canvasRect.y + 10f, canvasRect.width - 20f, 20f),
            "Middle-drag to pan / Mouse wheel to zoom",
            EditorStyles.miniBoldLabel);
    }

    private void DrawGrid(Rect canvasRect, float cellSize)
    {
        RectInt bounds = CalculateGridBounds();
        Vector2 center = CalculateCanvasCenter(canvasRect);
        int padding = 6;
        Color minor = new Color(1f, 1f, 1f, 0.05f);
        Color major = new Color(1f, 1f, 1f, 0.1f);

        Handles.BeginGUI();

        for (int x = bounds.xMin - padding; x <= bounds.xMax + padding; x++)
        {
            float xPos = center.x + x * cellSize;
            Handles.color = x == 0 ? new Color(0.45f, 0.78f, 1f, 0.34f) : ((x % 5 == 0) ? major : minor);
            Handles.DrawLine(new Vector3(xPos, canvasRect.yMin), new Vector3(xPos, canvasRect.yMax));
        }

        for (int y = bounds.yMin - padding; y <= bounds.yMax + padding; y++)
        {
            float yPos = center.y - y * cellSize;
            Handles.color = y == 0 ? new Color(0.45f, 0.78f, 1f, 0.34f) : ((y % 5 == 0) ? major : minor);
            Handles.DrawLine(new Vector3(canvasRect.xMin, yPos), new Vector3(canvasRect.xMax, yPos));
        }

        Handles.EndGUI();
    }

    private void DrawCanvasLinks(Rect canvasRect, float cellSize)
    {
        if (authoringData == null || authoringData.ManualLinks == null || authoringData.ManualLinks.Count == 0)
        {
            return;
        }

        Handles.BeginGUI();

        for (int i = 0; i < authoringData.ManualLinks.Count; i++)
        {
            MinimapLinkDefinition link = authoringData.ManualLinks[i];
            if (link == null || !link.IsValid)
            {
                continue;
            }

            MinimapRoom fromRoom = FindRoomById(link.FromRoomId);
            MinimapRoom toRoom = FindRoomById(link.ToRoomId);
            if (fromRoom == null || toRoom == null)
            {
                continue;
            }

            List<Vector2> points = BuildEditorPolyline(link, fromRoom, toRoom, canvasRect, cellSize);
            if (points.Count < 2)
            {
                continue;
            }

            Color color = GetEditorLinkColor(link, i == selectedLinkIndex);
            float thickness = link.LinkType == MinimapLinkType.Corridor ? 6f : 3f;
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, ConvertToHandlePoints(points));
        }

        Handles.EndGUI();
    }

    private void DrawCanvasRooms(Rect canvasRect, float cellSize)
    {
        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Rect rect = RoomToCanvasRect(room, canvasRect, cellSize);
            roomRects[room] = rect;

            bool isSelected = room == selectedRoom;
            Color fill = isSelected
                ? new Color(0.17f, 0.58f, 0.48f, 0.55f)
                : new Color(0.95f, 0.95f, 0.98f, 0.24f);
            Color outline = isSelected
                ? new Color(0.32f, 1f, 0.76f, 1f)
                : new Color(1f, 1f, 1f, 0.55f);

            EditorGUI.DrawRect(rect, fill);
            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, outline);
            Handles.EndGUI();

            Rect labelRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            GUI.Label(labelRect, room.DisplayName, EditorStyles.boldLabel);

            if (isSelected)
            {
                Rect handleRect = ResizeHandleRect(rect);
                EditorGUI.DrawRect(handleRect, new Color(1f, 0.85f, 0.35f, 0.95f));
            }
        }
    }

    private void HandleCanvasInput(Rect canvasRect, float cellSize)
    {
        Event current = Event.current;
        if (!canvasRect.Contains(current.mousePosition))
        {
            return;
        }

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            if (selectedRoom != null &&
                roomRects.TryGetValue(selectedRoom, out Rect selectedRect) &&
                ResizeHandleRect(selectedRect).Contains(current.mousePosition))
            {
                isResizingRoom = true;
                resizeRoom = selectedRoom;
                resizeStartMousePosition = current.mousePosition;
                resizeStartRoomPosition = resizeRoom.MapPosition;
                resizeStartRoomSize = resizeRoom.MapSize;
                current.Use();
                return;
            }

            MinimapRoom clickedRoom = PickRoom(current.mousePosition);
            if (clickedRoom != null)
            {
                SelectRoom(clickedRoom);

                if (createLinkMode)
                {
                    HandleLinkCreationClick(clickedRoom);
                    current.Use();
                    return;
                }

                isDraggingRoom = true;
                dragRoom = clickedRoom;
                dragStartMousePosition = current.mousePosition;
                dragStartRoomPosition = clickedRoom.MapPosition;
                current.Use();
                return;
            }

            selectedRoom = null;
            selectedLinkIndex = -1;
            Repaint();
        }

        if (current.type == EventType.MouseDrag && current.button == 0)
        {
            if (isDraggingRoom && dragRoom != null)
            {
                Vector2 delta = current.mousePosition - dragStartMousePosition;
                Vector2Int deltaCells = new Vector2Int(
                    Mathf.RoundToInt(delta.x / cellSize),
                    -Mathf.RoundToInt(delta.y / cellSize));

                ApplyRoomLayout(
                    dragRoom,
                    dragRoom.RoomId,
                    dragRoom.DisplayName,
                    dragStartRoomPosition + deltaCells,
                    dragRoom.MapSize);

                current.Use();
                return;
            }

            if (isResizingRoom && resizeRoom != null)
            {
                Vector2 delta = current.mousePosition - resizeStartMousePosition;
                int width = Mathf.Max(1, resizeStartRoomSize.x + Mathf.RoundToInt(delta.x / cellSize));
                int height = Mathf.Max(1, resizeStartRoomSize.y + Mathf.RoundToInt(delta.y / cellSize));
                int top = resizeStartRoomPosition.y + resizeStartRoomSize.y;
                Vector2Int nextSize = new Vector2Int(width, height);
                Vector2Int nextPosition = new Vector2Int(resizeStartRoomPosition.x, top - nextSize.y);

                ApplyRoomLayout(
                    resizeRoom,
                    resizeRoom.RoomId,
                    resizeRoom.DisplayName,
                    nextPosition,
                    nextSize);

                current.Use();
                return;
            }
        }

        if (current.type == EventType.MouseUp && current.button == 0)
        {
            isDraggingRoom = false;
            dragRoom = null;
            isResizingRoom = false;
            resizeRoom = null;
        }
    }

    private void DrawInspector(Rect inspectorRect)
    {
        EditorGUI.DrawRect(inspectorRect, new Color(0.16f, 0.17f, 0.19f, 1f));
        GUILayout.BeginArea(inspectorRect);
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

        DrawSceneInfoSection();
        EditorGUILayout.Space(10f);
        DrawRoomsSection();
        EditorGUILayout.Space(10f);
        DrawSelectedRoomSection();
        EditorGUILayout.Space(10f);
        DrawLinksSection();
        EditorGUILayout.Space(10f);
        DrawSelectedLinkSection();

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawSceneInfoSection()
    {
        EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Scene", SceneManager.GetActiveScene().name);
        EditorGUILayout.LabelField("Rooms", rooms.Length.ToString());
        EditorGUILayout.LabelField("Links", authoringData != null ? authoringData.ManualLinks.Count.ToString() : "0");

        if (createLinkMode)
        {
            string pending = pendingLinkFromRoom != null ? pendingLinkFromRoom.DisplayName : "None";
            EditorGUILayout.HelpBox("Link create mode: select the first area (" + pending + "), then click the destination area.", MessageType.Info);
        }
    }

    private void DrawRoomsSection()
    {
        EditorGUILayout.LabelField("Areas", EditorStyles.boldLabel);
        roomsListScroll = EditorGUILayout.BeginScrollView(roomsListScroll, GUILayout.Height(150f));

        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            GUIStyle style = room == selectedRoom ? EditorStyles.whiteLabel : EditorStyles.label;
            if (GUILayout.Button(room.DisplayName + "  [" + room.MapPosition.x + "," + room.MapPosition.y + "]", style))
            {
                SelectRoom(room);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSelectedRoomSection()
    {
        EditorGUILayout.LabelField("Selected Area", EditorStyles.boldLabel);
        if (selectedRoom == null)
        {
            EditorGUILayout.HelpBox("Select an area to edit its details here.", MessageType.None);
            return;
        }

        string originalId = selectedRoom.RoomId;
        string nextId = EditorGUILayout.TextField("Room ID", selectedRoom.RoomId);
        string nextDisplayName = EditorGUILayout.TextField("Display Name", selectedRoom.DisplayName);
        Vector2Int nextPosition = EditorGUILayout.Vector2IntField("Map Position", selectedRoom.MapPosition);
        Vector2Int nextSize = EditorGUILayout.Vector2IntField("Map Size", selectedRoom.MapSize);
        nextSize = new Vector2Int(Mathf.Max(1, nextSize.x), Mathf.Max(1, nextSize.y));

        if (GUILayout.Button("Apply Area Changes"))
        {
            ApplyRoomLayout(selectedRoom, nextId, nextDisplayName, nextPosition, nextSize);

            if (!string.Equals(originalId, nextId, StringComparison.Ordinal))
            {
                ReplaceRoomIdInLinks(originalId, nextId);
            }

            RefreshSceneData();
        }

        if (GUILayout.Button("Ping Selected Object"))
        {
            Selection.activeObject = selectedRoom.gameObject;
            EditorGUIUtility.PingObject(selectedRoom.gameObject);
        }
    }

    private void DrawLinksSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Links", EditorStyles.boldLabel);

        if (GUILayout.Button("Add", GUILayout.Width(64f)))
        {
            AddManualLinkFromSelection();
        }

        EditorGUILayout.EndHorizontal();

        if (authoringData == null)
        {
            EditorGUILayout.HelpBox("No link data exists yet. Use 'Create Link Data' in the toolbar.", MessageType.Warning);
            return;
        }

        linksListScroll = EditorGUILayout.BeginScrollView(linksListScroll, GUILayout.Height(160f));

        for (int i = 0; i < authoringData.ManualLinks.Count; i++)
        {
            MinimapLinkDefinition link = authoringData.ManualLinks[i];
            if (link == null)
            {
                continue;
            }

            string label = link.FromRoomId + " -> " + link.ToRoomId + "  (" + link.LinkType + ")";
            GUIStyle style = i == selectedLinkIndex ? EditorStyles.whiteLabel : EditorStyles.label;
            if (GUILayout.Button(label, style))
            {
                selectedLinkIndex = i;
                selectedRoom = null;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSelectedLinkSection()
    {
        EditorGUILayout.LabelField("Selected Link", EditorStyles.boldLabel);
        if (authoringData == null || selectedLinkIndex < 0 || selectedLinkIndex >= authoringData.ManualLinks.Count)
        {
            EditorGUILayout.HelpBox("Select a link to edit it here.", MessageType.None);
            return;
        }

        MinimapLinkDefinition currentLink = authoringData.ManualLinks[selectedLinkIndex];
        if (currentLink == null)
        {
            return;
        }

        string[] roomIds = BuildRoomIdOptions();
        if (roomIds.Length == 0)
        {
            EditorGUILayout.HelpBox("No MinimapRoom is available for link editing.", MessageType.Warning);
            return;
        }

        int fromIndex = Mathf.Max(0, IndexOf(roomIds, currentLink.FromRoomId));
        int toIndex = Mathf.Max(0, IndexOf(roomIds, currentLink.ToRoomId));
        string nextLinkId = EditorGUILayout.TextField("Link ID", currentLink.LinkId);
        fromIndex = EditorGUILayout.Popup("From", fromIndex, roomIds);
        toIndex = EditorGUILayout.Popup("To", toIndex, roomIds);
        MinimapLinkType nextType = (MinimapLinkType)EditorGUILayout.EnumPopup("Link Type", currentLink.LinkType);

        List<Vector2Int> nextPathPoints = ClonePathPoints(currentLink.PathPoints);
        EditorGUILayout.LabelField("Path Points");
        for (int i = 0; i < nextPathPoints.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            nextPathPoints[i] = EditorGUILayout.Vector2IntField("Point " + (i + 1), nextPathPoints[i]);
            if (GUILayout.Button("-", GUILayout.Width(24f)))
            {
                nextPathPoints.RemoveAt(i);
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Add Path Point"))
        {
            nextPathPoints.Add(Vector2Int.zero);
        }

        if (GUILayout.Button("Apply Link Changes"))
        {
            ReplaceLinkAt(
                selectedLinkIndex,
                new MinimapLinkDefinition(
                    nextLinkId,
                    roomIds[fromIndex],
                    roomIds[toIndex],
                    nextType,
                    nextPathPoints));
        }

        if (GUILayout.Button("Delete Link"))
        {
            RemoveLinkAt(selectedLinkIndex);
            selectedLinkIndex = -1;
        }
    }

    private void AddMinimapRoomToSelection()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            return;
        }

        MinimapRoom room = selectedObject.GetComponent<MinimapRoom>();
        if (room == null)
        {
            room = Undo.AddComponent<MinimapRoom>(selectedObject);
        }

        Undo.RecordObject(room, "Configure Minimap Room");
        room.ConfigureAuthoringFields(selectedObject.name, selectedObject.name, room.MapPosition, room.MapSize);
        EditorUtility.SetDirty(room);
        EditorSceneManager.MarkSceneDirty(selectedObject.scene);
        RefreshSceneData();
        SelectRoom(room);
    }

    private void SelectRoom(MinimapRoom room)
    {
        selectedRoom = room;
        selectedLinkIndex = -1;
        Selection.activeObject = room != null ? room.gameObject : null;
        Repaint();
    }

    private void HandleLinkCreationClick(MinimapRoom clickedRoom)
    {
        if (clickedRoom == null)
        {
            return;
        }

        if (pendingLinkFromRoom == null)
        {
            pendingLinkFromRoom = clickedRoom;
            Repaint();
            return;
        }

        if (pendingLinkFromRoom == clickedRoom)
        {
            pendingLinkFromRoom = null;
            Repaint();
            return;
        }

        EnsureAuthoringData();
        AddLink(
            new MinimapLinkDefinition(
                GenerateUniqueLinkId(pendingLinkFromRoom.RoomId, clickedRoom.RoomId),
                pendingLinkFromRoom.RoomId,
                clickedRoom.RoomId,
                newLinkType,
                null));

        pendingLinkFromRoom = clickedRoom;
        selectedLinkIndex = authoringData != null ? authoringData.ManualLinks.Count - 1 : -1;
        selectedRoom = null;
    }

    private void AddManualLinkFromSelection()
    {
        if (selectedRoom == null)
        {
            EditorUtility.DisplayDialog("Minimap Editor", "Select an area before adding a link.", "OK");
            return;
        }

        string fallbackTargetId = null;
        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null || room == selectedRoom || string.IsNullOrWhiteSpace(room.RoomId))
            {
                continue;
            }

            fallbackTargetId = room.RoomId;
            break;
        }

        if (string.IsNullOrWhiteSpace(fallbackTargetId))
        {
            EditorUtility.DisplayDialog("Minimap Editor", "At least two areas are required to create a link.", "OK");
            return;
        }

        EnsureAuthoringData();
        AddLink(
            new MinimapLinkDefinition(
                GenerateUniqueLinkId(selectedRoom.RoomId, fallbackTargetId),
                selectedRoom.RoomId,
                fallbackTargetId,
                newLinkType,
                null));

        selectedLinkIndex = authoringData.ManualLinks.Count - 1;
    }

    private void AddLink(MinimapLinkDefinition link)
    {
        if (authoringData == null || link == null)
        {
            return;
        }

        Undo.RecordObject(authoringData, "Add Minimap Link");
        var links = CloneLinks(authoringData.ManualLinks);
        links.Add(link);
        authoringData.SetManualLinks(links);
        EditorUtility.SetDirty(authoringData);
        EditorSceneManager.MarkSceneDirty(authoringData.gameObject.scene);
        RefreshSceneData();
        Repaint();
    }

    private void ReplaceLinkAt(int index, MinimapLinkDefinition replacement)
    {
        if (authoringData == null || replacement == null)
        {
            return;
        }

        var links = CloneLinks(authoringData.ManualLinks);
        if (index < 0 || index >= links.Count)
        {
            return;
        }

        links[index] = replacement;
        Undo.RecordObject(authoringData, "Update Minimap Link");
        authoringData.SetManualLinks(links);
        EditorUtility.SetDirty(authoringData);
        EditorSceneManager.MarkSceneDirty(authoringData.gameObject.scene);
        RefreshSceneData();
        Repaint();
    }

    private void RemoveLinkAt(int index)
    {
        if (authoringData == null)
        {
            return;
        }

        var links = CloneLinks(authoringData.ManualLinks);
        if (index < 0 || index >= links.Count)
        {
            return;
        }

        links.RemoveAt(index);
        Undo.RecordObject(authoringData, "Remove Minimap Link");
        authoringData.SetManualLinks(links);
        EditorUtility.SetDirty(authoringData);
        EditorSceneManager.MarkSceneDirty(authoringData.gameObject.scene);
        RefreshSceneData();
        Repaint();
    }

    private void ReplaceRoomIdInLinks(string previousRoomId, string nextRoomId)
    {
        if (authoringData == null ||
            string.IsNullOrWhiteSpace(previousRoomId) ||
            string.IsNullOrWhiteSpace(nextRoomId) ||
            string.Equals(previousRoomId, nextRoomId, StringComparison.Ordinal))
        {
            return;
        }

        var updatedLinks = new List<MinimapLinkDefinition>();
        IReadOnlyList<MinimapLinkDefinition> links = authoringData.ManualLinks;

        for (int i = 0; i < links.Count; i++)
        {
            MinimapLinkDefinition link = links[i];
            if (link == null)
            {
                continue;
            }

            string fromId = string.Equals(link.FromRoomId, previousRoomId, StringComparison.Ordinal)
                ? nextRoomId
                : link.FromRoomId;
            string toId = string.Equals(link.ToRoomId, previousRoomId, StringComparison.Ordinal)
                ? nextRoomId
                : link.ToRoomId;
            string fallbackPreviousLinkId = BuildFallbackLinkId(link.FromRoomId, link.ToRoomId);
            string fallbackNextLinkId = BuildFallbackLinkId(fromId, toId);

            updatedLinks.Add(
                new MinimapLinkDefinition(
                    string.Equals(link.LinkId, fallbackPreviousLinkId, StringComparison.Ordinal)
                        ? fallbackNextLinkId
                        : link.LinkId,
                    fromId,
                    toId,
                    link.LinkType,
                    link.PathPoints));
        }

        Undo.RecordObject(authoringData, "Update Minimap Link Room IDs");
        authoringData.SetManualLinks(updatedLinks);
        EditorUtility.SetDirty(authoringData);
        EditorSceneManager.MarkSceneDirty(authoringData.gameObject.scene);
    }

    private void ApplyRoomLayout(
        MinimapRoom room,
        string nextRoomId,
        string nextDisplayName,
        Vector2Int nextPosition,
        Vector2Int nextSize)
    {
        if (room == null)
        {
            return;
        }

        Undo.RecordObject(room, "Edit Minimap Room");
        room.ConfigureAuthoringFields(nextRoomId, nextDisplayName, nextPosition, nextSize);
        EditorUtility.SetDirty(room);
        EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
        RefreshSceneData();
        Repaint();
    }

    private void EnsureAuthoringData()
    {
        if (authoringData != null)
        {
            return;
        }

        GameObject authoringObject = new GameObject("MinimapAuthoringData");
        Undo.RegisterCreatedObjectUndo(authoringObject, "Create Minimap Authoring Data");
        SceneManager.MoveGameObjectToScene(authoringObject, SceneManager.GetActiveScene());
        authoringData = authoringObject.AddComponent<MinimapSceneAuthoringData>();
        EditorUtility.SetDirty(authoringData);
        EditorSceneManager.MarkSceneDirty(authoringObject.scene);
        RefreshSceneData();
    }

    private void RefreshSceneData()
    {
        rooms = FindObjectsByType<MinimapRoom>(FindObjectsSortMode.None);
        Array.Sort(rooms, CompareRooms);
        authoringData = FindFirstObjectByType<MinimapSceneAuthoringData>();

        if (selectedRoom != null && Array.IndexOf(rooms, selectedRoom) < 0)
        {
            selectedRoom = null;
        }

        if (authoringData == null)
        {
            selectedLinkIndex = -1;
        }
        else if (selectedLinkIndex >= authoringData.ManualLinks.Count)
        {
            selectedLinkIndex = -1;
        }
    }

    private RectInt CalculateGridBounds()
    {
        bool hasData = false;
        int minX = 0;
        int minY = 0;
        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Vector2Int position = room.MapPosition;
            Vector2Int size = room.MapSize;
            Include(position.x, position.y, position.x + size.x, position.y + size.y, ref hasData, ref minX, ref minY, ref maxX, ref maxY);
        }

        if (authoringData != null)
        {
            IReadOnlyList<MinimapLinkDefinition> links = authoringData.ManualLinks;
            for (int i = 0; i < links.Count; i++)
            {
                MinimapLinkDefinition link = links[i];
                if (link == null)
                {
                    continue;
                }

                IReadOnlyList<Vector2Int> pathPoints = link.PathPoints;
                for (int j = 0; j < pathPoints.Count; j++)
                {
                    Vector2Int point = pathPoints[j];
                    Include(point.x, point.y, point.x + 1, point.y + 1, ref hasData, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
        }

        if (!hasData)
        {
            return new RectInt(-4, -4, 8, 8);
        }

        return new RectInt(minX, minY, Mathf.Max(1, maxX - minX), Mathf.Max(1, maxY - minY));
    }

    private Vector2 CalculateCanvasCenter(Rect canvasRect)
    {
        return canvasRect.center + canvasPan;
    }

    private Rect RoomToCanvasRect(MinimapRoom room, Rect canvasRect, float cellSize)
    {
        Vector2 center = CalculateCanvasCenter(canvasRect);
        float xMin = center.x + room.MapPosition.x * cellSize;
        float xMax = center.x + (room.MapPosition.x + room.MapSize.x) * cellSize;
        float yTop = center.y - (room.MapPosition.y + room.MapSize.y) * cellSize;
        float yBottom = center.y - room.MapPosition.y * cellSize;
        return Rect.MinMaxRect(xMin, yTop, xMax, yBottom);
    }

    private Rect ResizeHandleRect(Rect roomRect)
    {
        return new Rect(
            roomRect.xMax - ResizeHandleSize,
            roomRect.yMax - ResizeHandleSize,
            ResizeHandleSize,
            ResizeHandleSize);
    }

    private MinimapRoom PickRoom(Vector2 mousePosition)
    {
        MinimapRoom picked = null;
        float smallestArea = float.MaxValue;

        foreach (KeyValuePair<MinimapRoom, Rect> pair in roomRects)
        {
            Rect expanded = pair.Value;
            expanded.xMin -= HitPadding;
            expanded.yMin -= HitPadding;
            expanded.xMax += HitPadding;
            expanded.yMax += HitPadding;

            if (!expanded.Contains(mousePosition))
            {
                continue;
            }

            float area = pair.Value.width * pair.Value.height;
            if (area < smallestArea)
            {
                smallestArea = area;
                picked = pair.Key;
            }
        }

        return picked;
    }

    private List<Vector2> BuildEditorPolyline(MinimapLinkDefinition link, MinimapRoom fromRoom, MinimapRoom toRoom, Rect canvasRect, float cellSize)
    {
        Rect fromRect = RoomToCanvasRect(fromRoom, canvasRect, cellSize);
        Rect toRect = RoomToCanvasRect(toRoom, canvasRect, cellSize);
        Vector2 fromCenter = fromRect.center;
        Vector2 toCenter = toRect.center;
        var points = new List<Vector2>();
        Vector2 firstTarget = link.PathPoints.Count > 0
            ? GridPointToCanvas(link.PathPoints[0], canvasRect, cellSize)
            : toCenter;
        Vector2 lastSource = link.PathPoints.Count > 0
            ? GridPointToCanvas(link.PathPoints[link.PathPoints.Count - 1], canvasRect, cellSize)
            : fromCenter;

        points.Add(RoomEdgeToward(fromRect, firstTarget));
        for (int i = 0; i < link.PathPoints.Count; i++)
        {
            points.Add(GridPointToCanvas(link.PathPoints[i], canvasRect, cellSize));
        }

        points.Add(RoomEdgeToward(toRect, lastSource));

        if (points.Count == 2 &&
            Mathf.Abs(points[0].x - points[1].x) > 0.01f &&
            Mathf.Abs(points[0].y - points[1].y) > 0.01f)
        {
            points.Insert(1, new Vector2(points[1].x, points[0].y));
        }

        return points;
    }

    private Vector2 GridPointToCanvas(Vector2Int point, Rect canvasRect, float cellSize)
    {
        Vector2 center = CalculateCanvasCenter(canvasRect);
        return new Vector2(
            center.x + (point.x + 0.5f) * cellSize,
            center.y - (point.y + 0.5f) * cellSize);
    }

    private Vector2 RoomEdgeToward(Rect rect, Vector2 target)
    {
        Vector2 center = rect.center;
        Vector2 delta = target - center;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            return center + new Vector2(Mathf.Sign(delta.x) * rect.width * 0.5f, 0f);
        }

        return center + new Vector2(0f, Mathf.Sign(delta.y) * rect.height * 0.5f);
    }

    private static Vector3[] ConvertToHandlePoints(List<Vector2> points)
    {
        var handlePoints = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            handlePoints[i] = points[i];
        }

        return handlePoints;
    }

    private Color GetEditorLinkColor(MinimapLinkDefinition link, bool isSelected)
    {
        Color baseColor;
        switch (link.LinkType)
        {
            case MinimapLinkType.Corridor:
                baseColor = new Color(0.92f, 0.86f, 0.5f, 1f);
                break;
            case MinimapLinkType.Stairs:
                baseColor = new Color(0.97f, 0.67f, 0.28f, 1f);
                break;
            case MinimapLinkType.Warp:
                baseColor = new Color(0.87f, 0.47f, 1f, 1f);
                break;
            default:
                baseColor = new Color(0.62f, 0.9f, 1f, 1f);
                break;
        }

        if (isSelected)
        {
            return Color.Lerp(baseColor, Color.white, 0.25f);
        }

        baseColor.a = 0.9f;
        return baseColor;
    }

    private string[] BuildRoomIdOptions()
    {
        var options = new List<string>();
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null || string.IsNullOrWhiteSpace(rooms[i].RoomId))
            {
                continue;
            }

            options.Add(rooms[i].RoomId);
        }

        return options.ToArray();
    }

    private MinimapRoom FindRoomById(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            if (string.Equals(room.RoomId, roomId, StringComparison.Ordinal))
            {
                return room;
            }
        }

        return null;
    }

    private static int CompareRooms(MinimapRoom left, MinimapRoom right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return string.Compare(left.RoomId, right.RoomId, StringComparison.Ordinal);
    }

    private static void Include(
        int xMin,
        int yMin,
        int xMax,
        int yMax,
        ref bool hasData,
        ref int minX,
        ref int minY,
        ref int maxX,
        ref int maxY)
    {
        if (!hasData)
        {
            hasData = true;
            minX = xMin;
            minY = yMin;
            maxX = xMax;
            maxY = yMax;
            return;
        }

        minX = Mathf.Min(minX, xMin);
        minY = Mathf.Min(minY, yMin);
        maxX = Mathf.Max(maxX, xMax);
        maxY = Mathf.Max(maxY, yMax);
    }

    private static int IndexOf(string[] values, string target)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], target, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<Vector2Int> ClonePathPoints(IReadOnlyList<Vector2Int> source)
    {
        var clone = new List<Vector2Int>();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Count; i++)
        {
            clone.Add(source[i]);
        }

        return clone;
    }

    private static List<MinimapLinkDefinition> CloneLinks(IReadOnlyList<MinimapLinkDefinition> source)
    {
        var clone = new List<MinimapLinkDefinition>();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Count; i++)
        {
            MinimapLinkDefinition link = source[i];
            if (link == null)
            {
                continue;
            }

            clone.Add(link.Clone());
        }

        return clone;
    }

    private string GenerateUniqueLinkId(string fromRoomId, string toRoomId)
    {
        string baseId = fromRoomId + "_to_" + toRoomId;
        if (authoringData == null)
        {
            return baseId;
        }

        string candidate = baseId;
        int suffix = 2;
        while (ContainsLinkId(candidate))
        {
            candidate = baseId + "_" + suffix;
            suffix++;
        }

        return candidate;
    }

    private bool ContainsLinkId(string linkId)
    {
        if (authoringData == null || string.IsNullOrWhiteSpace(linkId))
        {
            return false;
        }

        IReadOnlyList<MinimapLinkDefinition> links = authoringData.ManualLinks;
        for (int i = 0; i < links.Count; i++)
        {
            MinimapLinkDefinition link = links[i];
            if (link == null)
            {
                continue;
            }

            if (string.Equals(link.LinkId, linkId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildFallbackLinkId(string fromRoomId, string toRoomId)
    {
        return fromRoomId + "_to_" + toRoomId;
    }
}
