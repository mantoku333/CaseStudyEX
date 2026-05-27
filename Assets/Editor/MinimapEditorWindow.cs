using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class MinimapEditorWindow : EditorWindow
{
    private struct AutoCorridorPreview
    {
        public MinimapRoom FromRoom;
        public MinimapRoom ToRoom;

        public AutoCorridorPreview(MinimapRoom fromRoom, MinimapRoom toRoom)
        {
            FromRoom = fromRoom;
            ToRoom = toRoom;
        }
    }

    private enum InteractionMode
    {
        None,
        Panning,
        MovingRoom,
        ResizingRoom,
        DraggingLinkPoint,
        DraggingLinkSegment
    }

    private enum ResizeHandle
    {
        None,
        Right,
        Bottom,
        BottomRight
    }

    private const float SidebarWidth = 360f;
    private const float BaseBoardScale = 80f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 3f;
    private const float PointHandleRadius = 8f;
    private const float ResizeHandleSize = 12f;
    private const float MinRoomSize = 0.2f;
    private const float SegmentHitDistance = 8f;
    private const float FramePadding = 0.8f;
    private const float ScenePlacementSpacingFactor = 1.36f;
    private static readonly Vector2 StandardRoomSize = new Vector2(1.5f, 1f);
    private static readonly Regex GeneratedRoomIdPattern = new Regex(@"^Col_(\d+)-(\d+)$", RegexOptions.Compiled);
    private static readonly Regex HierarchyAreaNamePattern = new Regex(@"^(\d+)-(\d+)$", RegexOptions.Compiled);

    [SerializeField] private float zoom = 1f;
    [SerializeField] private Vector2 panOffset = Vector2.zero;
    [SerializeField] private Vector2 sidebarScroll;
    [SerializeField] private MinimapRoom selectedRoom;
    [SerializeField] private MinimapLink selectedLink;
    [SerializeField] private int selectedLinkPointIndex = -1;
    [SerializeField] private MinimapRoom pendingLinkStartRoom;

    private readonly List<MinimapRoom> rooms = new List<MinimapRoom>();
    private readonly List<MinimapLink> links = new List<MinimapLink>();
    private readonly List<AutoCorridorPreview> autoCorridorPreviews = new List<AutoCorridorPreview>();
    private bool sceneDataDirty = true;
    private bool frameAllRequested = true;
    private InteractionMode interactionMode = InteractionMode.None;
    private ResizeHandle activeResizeHandle = ResizeHandle.None;
    private Vector2 dragStartMousePosition;
    private Vector2 dragStartPanOffset;
    private Vector2 dragStartRoomPosition;
    private Vector2 dragStartRoomSize;
    private readonly List<Vector2> dragStartLinkPoints = new List<Vector2>();
    private readonly List<int> activeLinkDraggedPointIndices = new List<int>();
    private Rect lastCanvasRect;

    [MenuItem("Tools/Minimap/Editor")]
    public static void Open()
    {
        MinimapEditorWindow window = GetWindow<MinimapEditorWindow>("ミニマップエディター");
        window.minSize = new Vector2(1040f, 620f);
        window.Show();
    }

    private float BoardScale => BaseBoardScale * zoom;

    private void OnEnable()
    {
        EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        Selection.selectionChanged += HandleSelectionChanged;
        sceneDataDirty = true;
        frameAllRequested = true;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        Selection.selectionChanged -= HandleSelectionChanged;
    }

    private void OnGUI()
    {
        RefreshSceneDataIfNeeded();
        DrawToolbarLocalized();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSidebar();
            DrawCanvasArea();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                sceneDataDirty = true;
                RefreshSceneDataIfNeeded();
            }

            if (GUILayout.Button("全体表示", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                frameAllRequested = true;
                Repaint();
            }

            if (GUILayout.Button("選択対象に MinimapRoom を追加", EditorStyles.toolbarButton, GUILayout.Width(200f)))
            {
                AttachRoomsToSelection();
            }

            if (GUILayout.Button("旧部屋を変換", EditorStyles.toolbarButton, GUILayout.Width(140f)))
            {
                ConvertLegacyRooms();
            }

            if (GUILayout.Button("全部屋を 1.5 x 1 に統一", EditorStyles.toolbarButton, GUILayout.Width(165f)))
            {
                NormalizeAllRoomSizes();
            }

            if (GUILayout.Button("内容をリセット", EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                ResetAllMinimapContent();
            }

            GUILayout.Space(14f);
            GUILayout.Label("拡大", GUILayout.Width(36f));

            float nextZoom = GUILayout.HorizontalSlider(zoom, MinZoom, MaxZoom, GUILayout.Width(130f));
            if (!Mathf.Approximately(nextZoom, zoom))
            {
                if (lastCanvasRect.width > 0f && lastCanvasRect.height > 0f)
                {
                    ZoomAroundCanvasPoint(lastCanvasRect, lastCanvasRect.center, nextZoom);
                }
                else
                {
                    zoom = nextZoom;
                }

                Repaint();
            }

            GUILayout.Label(string.Format("{0:0.00}x", zoom), GUILayout.Width(50f));
            GUILayout.FlexibleSpace();

            if (pendingLinkStartRoom != null)
            {
                GUILayout.Label("接続先の部屋をクリックしてください", EditorStyles.miniLabel);
                if (GUILayout.Button("接続作成を中止", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    pendingLinkStartRoom = null;
                }
            }
            else
            {
                GUILayout.Label(string.Format("部屋: {0}  線: {1}", rooms.Count, links.Count), EditorStyles.miniLabel);
            }
        }
    }

    private void DrawSidebar()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
        {
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(sidebarScroll))
            {
                sidebarScroll = scrollScope.scrollPosition;

                DrawRoomsList();
                EditorGUILayout.Space(10f);
                DrawLinksList();
                EditorGUILayout.Space(10f);
                DrawSelectedRoomInspector();
                EditorGUILayout.Space(10f);
                DrawSelectedLinkInspector();
                EditorGUILayout.Space(10f);
                DrawWarnings();
            }
        }
    }

    private void DrawRoomsList()
    {
        EditorGUILayout.LabelField("部屋", EditorStyles.boldLabel);

        if (rooms.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "このシーンに MinimapRoom がありません。\n部屋オブジェクトを選択して「選択対象に MinimapRoom を追加」を押してください。",
                MessageType.Info);
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            string label = BuildRoomListLabel(room);

            GUIStyle style = room == selectedRoom ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button(label, style))
            {
                SelectRoom(room);
            }
        }
    }

    private void DrawLinksList()
    {
        EditorGUILayout.LabelField("廊下 / 接続線", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = selectedRoom != null;
            if (GUILayout.Button(pendingLinkStartRoom == null ? "選択中の部屋から線を引く" : "接続先を選択中"))
            {
                pendingLinkStartRoom = selectedRoom;
            }

            if (GUILayout.Button("自動線を編集用に変換"))
            {
                BakeAutoCorridors();
            }

            GUI.enabled = selectedLink != null;
            if (GUILayout.Button("選択中の線を削除"))
            {
                DeleteSelectedLink();
            }

            GUI.enabled = true;
        }

        if (links.Count == 0)
        {
            if (autoCorridorPreviews.Count == 0)
            {
                EditorGUILayout.HelpBox("このシーンには接続線がありません。", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                string.Format(
                    "まだ手動の接続線はありません。ボード上には自動接続のプレビューが {0} 本表示されています。「自動線を編集用に変換」で編集できる線にできます。",
                    autoCorridorPreviews.Count),
                MessageType.Info);
        }

        for (int i = 0; i < links.Count; i++)
        {
            MinimapLink link = links[i];
            if (link == null)
            {
                continue;
            }

            string fromLabel = link.FromRoom != null ? GetEditorRoomLabel(link.FromRoom) : "?";
            string toLabel = link.ToRoom != null ? GetEditorRoomLabel(link.ToRoom) : "?";
            string label = string.Format("{0}  {1} -> {2}", link.LinkId, fromLabel, toLabel);
            GUIStyle style = link == selectedLink ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button(label, style))
            {
                SelectLink(link, -1);
            }
        }
    }

    private void DrawSelectedRoomInspector()
    {
        EditorGUILayout.LabelField("選択中の部屋", EditorStyles.boldLabel);

        if (selectedRoom == null)
        {
            EditorGUILayout.HelpBox("部屋の矩形をクリックするか、一覧から選んでください。", MessageType.None);
            return;
        }

        EditorGUILayout.ObjectField("オブジェクト", selectedRoom.gameObject, typeof(GameObject), true);

        EditorGUI.BeginChangeCheck();
        string nextRoomId = EditorGUILayout.TextField("部屋ID", selectedRoom.RoomId);
        string nextDisplayName = EditorGUILayout.TextField("表示名", selectedRoom.DisplayName);
        Vector2 nextAreaPosition = EditorGUILayout.Vector2Field("ボード位置", selectedRoom.AreaPosition);
        Vector2 nextAreaSize = EditorGUILayout.Vector2Field("サイズ", selectedRoom.AreaSize);

        if (EditorGUI.EndChangeCheck())
        {
            ApplyRoomFields(selectedRoom, nextRoomId, nextDisplayName, nextAreaPosition, nextAreaSize, "Edit Minimap Room");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("場所を表示"))
            {
                EditorGUIUtility.PingObject(selectedRoom.gameObject);
            }

            if (GUILayout.Button("GameObject 名を使う"))
            {
                string objectName = selectedRoom.gameObject.name;
                ApplyRoomFields(
                    selectedRoom,
                    objectName,
                    objectName,
                    selectedRoom.AreaPosition,
                    selectedRoom.AreaSize,
                    "Rename Minimap Room");
            }
        }

        EditorGUILayout.LabelField("キャンバス操作", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(
            "矩形をドラッグすると移動できます。右・下・右下のハンドルをドラッグするとサイズ変更できます。",
            EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawSelectedLinkInspector()
    {
        EditorGUILayout.LabelField("選択中の線", EditorStyles.boldLabel);

        if (selectedLink == null)
        {
            EditorGUILayout.HelpBox("キャンバス上の線か、一覧の線を選んでください。", MessageType.None);
            return;
        }

        EditorGUILayout.ObjectField("オブジェクト", selectedLink.gameObject, typeof(GameObject), true);

        EditorGUI.BeginChangeCheck();
        MinimapRoom nextFromRoom = (MinimapRoom)EditorGUILayout.ObjectField("開始部屋", selectedLink.FromRoom, typeof(MinimapRoom), true);
        MinimapRoom nextToRoom = (MinimapRoom)EditorGUILayout.ObjectField("接続先部屋", selectedLink.ToRoom, typeof(MinimapRoom), true);

        List<Vector2> pathPoints = CopyPathPoints(selectedLink);
        int removeIndex = -1;
        for (int i = 0; i < pathPoints.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                pathPoints[i] = EditorGUILayout.Vector2Field("点 " + (i + 1), pathPoints[i]);
                if (GUILayout.Button("選択", GUILayout.Width(52f)))
                {
                    SelectLink(selectedLink, i);
                }

                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    removeIndex = i;
                }
            }
        }

        if (removeIndex >= 0)
        {
            pathPoints.RemoveAt(removeIndex);
            selectedLinkPointIndex = Mathf.Clamp(selectedLinkPointIndex, -1, pathPoints.Count - 1);
        }

        if (EditorGUI.EndChangeCheck())
        {
            ApplyLinkFields(selectedLink, nextFromRoom, nextToRoom, pathPoints, "Edit Minimap Link");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("点を追加"))
            {
                AddPointToSelectedLink();
            }

            GUI.enabled = selectedLinkPointIndex >= 0;
            if (GUILayout.Button("選択中の点を削除"))
            {
                RemoveSelectedLinkPoint();
            }

            GUI.enabled = true;
        }

        EditorGUILayout.LabelField("線の操作", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(
            "線分をドラッグするとそのまま形を変えられます。Shift クリックかダブルクリックで点を追加できます。",
            EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawWarnings()
    {
        List<string> duplicateIds = FindDuplicateRoomIds();
        List<string> overlaps = FindOverlaps();
        List<string> invalidLinks = FindInvalidLinks();

        EditorGUILayout.LabelField("チェック", EditorStyles.boldLabel);

        if (duplicateIds.Count == 0 && overlaps.Count == 0 && invalidLinks.Count == 0)
        {
            EditorGUILayout.HelpBox("重複ID、部屋の重なり、不正な線はありません。", MessageType.Info);
            return;
        }

        if (duplicateIds.Count > 0)
        {
            EditorGUILayout.HelpBox("重複している部屋ID: " + string.Join(", ", duplicateIds.ToArray()), MessageType.Error);
        }

        if (overlaps.Count > 0)
        {
            EditorGUILayout.HelpBox("重なっている部屋:\n" + string.Join("\n", overlaps.ToArray()), MessageType.Warning);
        }

        if (invalidLinks.Count > 0)
        {
            EditorGUILayout.HelpBox("不正な線:\n" + string.Join("\n", invalidLinks.ToArray()), MessageType.Warning);
        }
    }

    private void DrawCanvasArea()
    {
        Rect canvasRect = GUILayoutUtility.GetRect(100f, 10000f, 100f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        lastCanvasRect = canvasRect;

        EditorGUI.DrawRect(canvasRect, new Color(0.11f, 0.12f, 0.15f, 1f));

        if (frameAllRequested)
        {
            FrameAll(canvasRect);
            frameAllRequested = false;
        }

        HandleCanvasInput(canvasRect);

        Rect localCanvasRect = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
        GUI.BeginClip(canvasRect);
        DrawGrid(localCanvasRect);
        DrawLinks(localCanvasRect);
        DrawRooms(localCanvasRect);
        DrawCanvasOverlay(localCanvasRect);
        GUI.EndClip();
    }

    private void DrawGrid(Rect canvasRect)
    {
        Handles.BeginGUI();
        Vector2 origin = GetCanvasOrigin(canvasRect);
        float scale = BoardScale;

        int minGridX = Mathf.FloorToInt((canvasRect.xMin - origin.x) / scale) - 1;
        int maxGridX = Mathf.CeilToInt((canvasRect.xMax - origin.x) / scale) + 1;
        int minGridY = Mathf.FloorToInt((canvasRect.yMin - origin.y) / scale) - 1;
        int maxGridY = Mathf.CeilToInt((canvasRect.yMax - origin.y) / scale) + 1;

        Color minorColor = new Color(1f, 1f, 1f, 0.06f);
        Color axisColor = new Color(0.28f, 0.78f, 0.95f, 0.42f);

        for (int x = minGridX; x <= maxGridX; x++)
        {
            float drawX = origin.x + (x * scale);
            Handles.color = x == 0 ? axisColor : minorColor;
            Handles.DrawLine(new Vector3(drawX, canvasRect.yMin), new Vector3(drawX, canvasRect.yMax));
        }

        for (int y = minGridY; y <= maxGridY; y++)
        {
            float drawY = origin.y + (y * scale);
            Handles.color = y == 0 ? axisColor : minorColor;
            Handles.DrawLine(new Vector3(canvasRect.xMin, drawY), new Vector3(canvasRect.xMax, drawY));
        }

        Handles.EndGUI();
    }

    private void DrawRooms(Rect canvasRect)
    {
        HashSet<MinimapRoom> overlappingRooms = GetOverlappingRooms();
        HashSet<string> duplicateIds = new HashSet<string>(FindDuplicateRoomIds(), StringComparer.Ordinal);

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null || room == selectedRoom)
            {
                continue;
            }

            DrawRoom(canvasRect, room, overlappingRooms.Contains(room), duplicateIds.Contains(room.RoomId), false);
        }

        if (selectedRoom != null)
        {
            DrawRoom(canvasRect, selectedRoom, overlappingRooms.Contains(selectedRoom), duplicateIds.Contains(selectedRoom.RoomId), true);
        }
    }

    private void DrawRoom(Rect canvasRect, MinimapRoom room, bool isOverlap, bool isDuplicate, bool isSelected)
    {
        Rect rect = RoomToCanvasRect(canvasRect, room);
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        Color fillColor = new Color(0.2f, 0.39f, 0.62f, 0.78f);
        Color borderColor = new Color(0.82f, 0.9f, 1f, 1f);

        if (isDuplicate)
        {
            fillColor = new Color(0.74f, 0.42f, 0.16f, 0.82f);
            borderColor = new Color(1f, 0.84f, 0.62f, 1f);
        }

        if (isOverlap)
        {
            fillColor = new Color(0.72f, 0.22f, 0.24f, 0.84f);
            borderColor = new Color(1f, 0.72f, 0.72f, 1f);
        }

        if (isSelected)
        {
            fillColor = new Color(0.1f, 0.58f, 0.56f, 0.9f);
            borderColor = new Color(0.84f, 1f, 0.96f, 1f);
        }

        EditorGUI.DrawRect(rect, fillColor);
        DrawRectOutline(rect, borderColor, isSelected ? 3f : 2f);

        Rect labelRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
        GUI.Label(labelRect, GetEditorRoomLabel(room), EditorStyles.whiteMiniLabel);

        if (isSelected)
        {
            Rect rightHandle = GetRightResizeHandleRect(rect);
            Rect bottomHandle = GetBottomResizeHandleRect(rect);
            Rect cornerHandle = GetBottomRightResizeHandleRect(rect);

            EditorGUI.DrawRect(rightHandle, borderColor);
            EditorGUI.DrawRect(bottomHandle, borderColor);
            EditorGUI.DrawRect(cornerHandle, borderColor);

            EditorGUIUtility.AddCursorRect(rightHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(bottomHandle, MouseCursor.ResizeVertical);
            EditorGUIUtility.AddCursorRect(cornerHandle, MouseCursor.ResizeUpLeft);
        }

        EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);
    }

    private void DrawLinks(Rect canvasRect)
    {
        Handles.BeginGUI();

        if (autoCorridorPreviews.Count > 0)
        {
            Handles.color = new Color(0.72f, 0.78f, 0.84f, 0.28f);
            for (int i = 0; i < autoCorridorPreviews.Count; i++)
            {
                AutoCorridorPreview preview = autoCorridorPreviews[i];
                if (preview.FromRoom == null || preview.ToRoom == null)
                {
                    continue;
                }

                List<Vector2> boardPoints = BuildDisplayedPolyline(preview.FromRoom, preview.ToRoom, null);
                for (int pointIndex = 0; pointIndex < boardPoints.Count - 1; pointIndex++)
                {
                    Vector2 start = BoardToCanvas(canvasRect, boardPoints[pointIndex]);
                    Vector2 end = BoardToCanvas(canvasRect, boardPoints[pointIndex + 1]);
                    Handles.DrawAAPolyLine(2f, start, end);
                }
            }
        }

        for (int i = 0; i < links.Count; i++)
        {
            MinimapLink link = links[i];
            if (link == null || !link.IsValid)
            {
                continue;
            }

            List<Vector2> boardPoints = BuildDisplayedPolyline(link);
            Color color = link == selectedLink
                ? new Color(0.93f, 0.96f, 0.98f, 1f)
                : new Color(0.88f, 0.9f, 0.93f, 0.68f);

            Handles.color = color;
            for (int pointIndex = 0; pointIndex < boardPoints.Count - 1; pointIndex++)
            {
                Vector2 start = BoardToCanvas(canvasRect, boardPoints[pointIndex]);
                Vector2 end = BoardToCanvas(canvasRect, boardPoints[pointIndex + 1]);
                Handles.DrawAAPolyLine(link == selectedLink ? 4f : 3f, start, end);
            }

            if (link == selectedLink)
            {
                IReadOnlyList<Vector2> pathPoints = link.PathPoints;
                for (int pointIndex = 0; pointIndex < pathPoints.Count; pointIndex++)
                {
                    Vector2 point = BoardToCanvas(canvasRect, pathPoints[pointIndex]);
                    float radius = selectedLinkPointIndex == pointIndex ? PointHandleRadius + 1f : PointHandleRadius;
                    DrawPointHandle(point, radius, selectedLinkPointIndex == pointIndex);
                }
            }
        }

        Handles.EndGUI();
    }

    private void DrawPointHandle(Vector2 point, float radius, bool isSelected)
    {
        Rect handleRect = new Rect(point.x - radius, point.y - radius, radius * 2f, radius * 2f);
        EditorGUI.DrawRect(handleRect, isSelected ? new Color(0.15f, 0.85f, 0.78f, 1f) : new Color(0.95f, 0.95f, 0.95f, 0.92f));
    }

    private void DrawCanvasOverlay(Rect canvasRect)
    {
        Rect infoRect = new Rect(canvasRect.x + 12f, canvasRect.y + 10f, 560f, 40f);
        string text = pendingLinkStartRoom != null
            ? "接続モード: 接続先の部屋をクリック"
            : "左ドラッグ: 部屋・点・線を移動  右/中ドラッグ: 視点移動  Shift/ダブルクリック: 点追加";
        GUI.Label(infoRect, text, EditorStyles.whiteMiniLabel);
    }

    private void HandleCanvasInput(Rect canvasRect)
    {
        Event evt = Event.current;

        if (!canvasRect.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.MouseUp)
            {
                EndInteraction();
            }

            return;
        }

        if (evt.type == EventType.ScrollWheel)
        {
            float nextZoom = Mathf.Clamp(zoom - (evt.delta.y * 0.03f), MinZoom, MaxZoom);
            ZoomAroundCanvasPoint(canvasRect, evt.mousePosition, nextZoom);
            evt.Use();
            Repaint();
            return;
        }

        if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
        {
            pendingLinkStartRoom = null;
            evt.Use();
            Repaint();
            return;
        }

        if (evt.type == EventType.MouseDown)
        {
            if (evt.button == 1 || evt.button == 2)
            {
                interactionMode = InteractionMode.Panning;
                dragStartMousePosition = evt.mousePosition;
                dragStartPanOffset = panOffset;
                evt.Use();
                return;
            }

            if (evt.button != 0)
            {
                return;
            }

            if (pendingLinkStartRoom != null)
            {
                MinimapRoom linkTargetRoom;
                if (TryHitRoom(canvasRect, evt.mousePosition, out linkTargetRoom))
                {
                    if (linkTargetRoom != null && linkTargetRoom != pendingLinkStartRoom)
                    {
                        CreateLink(pendingLinkStartRoom, linkTargetRoom);
                    }

                    pendingLinkStartRoom = null;
                    evt.Use();
                    return;
                }
            }

            int pointIndex;
            MinimapLink pointLink;
            if (TryHitLinkPoint(canvasRect, evt.mousePosition, out pointLink, out pointIndex))
            {
                SelectLink(pointLink, pointIndex);
                interactionMode = InteractionMode.DraggingLinkPoint;
                Undo.RecordObject(pointLink, "Move Minimap Link Point");
                evt.Use();
                return;
            }

            MinimapRoom hitRoom;
            Rect hitRoomRect;
            if (TryHitRoom(canvasRect, evt.mousePosition, out hitRoom, out hitRoomRect))
            {
                SelectRoom(hitRoom);

                ResizeHandle handle = GetHitResizeHandle(hitRoomRect, evt.mousePosition);
                if (handle != ResizeHandle.None)
                {
                    interactionMode = InteractionMode.ResizingRoom;
                    activeResizeHandle = handle;
                    dragStartMousePosition = evt.mousePosition;
                    dragStartRoomPosition = hitRoom.AreaPosition;
                    dragStartRoomSize = hitRoom.AreaSize;
                    Undo.RecordObject(hitRoom, "Resize Minimap Room");
                    evt.Use();
                    return;
                }

                interactionMode = InteractionMode.MovingRoom;
                activeResizeHandle = ResizeHandle.None;
                dragStartMousePosition = evt.mousePosition;
                dragStartRoomPosition = hitRoom.AreaPosition;
                dragStartRoomSize = hitRoom.AreaSize;
                Undo.RecordObject(hitRoom, "Move Minimap Room");
                evt.Use();
                return;
            }

            MinimapLink hitLink;
            int segmentIndex;
            if (TryHitLinkSegment(canvasRect, evt.mousePosition, out hitLink, out segmentIndex))
            {
                SelectLink(hitLink, -1);
                if (evt.shift || evt.clickCount > 1)
                {
                    int insertedIndex = InsertPointOnLink(hitLink, segmentIndex, CanvasToBoard(canvasRect, evt.mousePosition));
                    if (insertedIndex >= 0)
                    {
                        SelectLink(hitLink, insertedIndex);
                        interactionMode = InteractionMode.DraggingLinkPoint;
                        Undo.RecordObject(hitLink, "Move Minimap Link Point");
                    }
                }
                else
                {
                    BeginLinkSegmentDrag(hitLink, segmentIndex, evt.mousePosition);
                }

                evt.Use();
                return;
            }

            selectedRoom = null;
            selectedLink = null;
            selectedLinkPointIndex = -1;
            Selection.activeGameObject = null;
            Repaint();
        }

        if (evt.type == EventType.MouseDrag)
        {
            Vector2 delta = evt.mousePosition - dragStartMousePosition;

            if (interactionMode == InteractionMode.Panning)
            {
                panOffset = dragStartPanOffset + delta;
                evt.Use();
                Repaint();
                return;
            }

            if (interactionMode == InteractionMode.MovingRoom && selectedRoom != null)
            {
                Vector2 boardDelta = delta / BoardScale;
                ApplyRoomFields(
                    selectedRoom,
                    selectedRoom.RoomId,
                    selectedRoom.DisplayName,
                    dragStartRoomPosition + boardDelta,
                    dragStartRoomSize,
                    null);
                evt.Use();
                Repaint();
                return;
            }

            if (interactionMode == InteractionMode.ResizingRoom && selectedRoom != null)
            {
                Vector2 boardDelta = delta / BoardScale;
                Vector2 nextPosition = dragStartRoomPosition;
                Vector2 nextSize = dragStartRoomSize;

                if (activeResizeHandle == ResizeHandle.Right || activeResizeHandle == ResizeHandle.BottomRight)
                {
                    nextSize.x = Mathf.Max(MinRoomSize, dragStartRoomSize.x + boardDelta.x);
                }

                if (activeResizeHandle == ResizeHandle.Bottom || activeResizeHandle == ResizeHandle.BottomRight)
                {
                    nextSize.y = Mathf.Max(MinRoomSize, dragStartRoomSize.y + boardDelta.y);
                }

                ApplyRoomFields(
                    selectedRoom,
                    selectedRoom.RoomId,
                    selectedRoom.DisplayName,
                    nextPosition,
                    nextSize,
                    null);
                evt.Use();
                Repaint();
                return;
            }

            if (interactionMode == InteractionMode.DraggingLinkPoint &&
                selectedLink != null &&
                selectedLinkPointIndex >= 0)
            {
                List<Vector2> nextPathPoints = CopyPathPoints(selectedLink);
                if (selectedLinkPointIndex < nextPathPoints.Count)
                {
                    nextPathPoints[selectedLinkPointIndex] = CanvasToBoard(canvasRect, evt.mousePosition);
                    SetLinkPoints(selectedLink, nextPathPoints);
                }

                evt.Use();
                Repaint();
                return;
            }

            if (interactionMode == InteractionMode.DraggingLinkSegment &&
                selectedLink != null &&
                activeLinkDraggedPointIndices.Count > 0)
            {
                Vector2 boardDelta = delta / BoardScale;
                List<Vector2> nextPathPoints = new List<Vector2>(dragStartLinkPoints);
                for (int index = 0; index < activeLinkDraggedPointIndices.Count; index++)
                {
                    int pointIndex = activeLinkDraggedPointIndices[index];
                    if (pointIndex < 0 || pointIndex >= nextPathPoints.Count)
                    {
                        continue;
                    }

                    nextPathPoints[pointIndex] = dragStartLinkPoints[pointIndex] + boardDelta;
                }

                SetLinkPoints(selectedLink, nextPathPoints);
                evt.Use();
                Repaint();
                return;
            }
        }

        if (evt.type == EventType.MouseUp)
        {
            EndInteraction();
        }
    }

    private void EndInteraction()
    {
        interactionMode = InteractionMode.None;
        activeResizeHandle = ResizeHandle.None;
        dragStartLinkPoints.Clear();
        activeLinkDraggedPointIndices.Clear();
    }

    private void RefreshSceneDataIfNeeded()
    {
        if (!sceneDataDirty)
        {
            return;
        }

        sceneDataDirty = false;
        rooms.Clear();
        links.Clear();
        autoCorridorPreviews.Clear();

        MinimapRoom[] foundRooms = FindObjectsByType<MinimapRoom>(FindObjectsSortMode.None);
        if (foundRooms != null)
        {
            for (int i = 0; i < foundRooms.Length; i++)
            {
                if (foundRooms[i] != null)
                {
                    rooms.Add(foundRooms[i]);
                }
            }
        }

        MinimapLink[] foundLinks = FindObjectsByType<MinimapLink>(FindObjectsSortMode.None);
        if (foundLinks != null)
        {
            for (int i = 0; i < foundLinks.Length; i++)
            {
                if (foundLinks[i] != null)
                {
                    links.Add(foundLinks[i]);
                }
            }
        }

        rooms.Sort(CompareHierarchyOrder);
        ApplyHierarchyRoomIds();
        EnsureUniqueRoomIds();
        rooms.Sort(CompareRooms);
        links.Sort(CompareLinks);
        RebuildAutoCorridorPreviews();

        if (selectedRoom == null || !rooms.Contains(selectedRoom))
        {
            selectedRoom = rooms.Count > 0 ? rooms[0] : null;
        }

        if (selectedLink == null || !links.Contains(selectedLink))
        {
            selectedLink = null;
            selectedLinkPointIndex = -1;
        }
    }

    private static int CompareRooms(MinimapRoom a, MinimapRoom b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int idCompare = CompareNatural(a.RoomId, b.RoomId);
        if (idCompare != 0)
        {
            return idCompare;
        }

        return CompareNatural(a.gameObject.name, b.gameObject.name);
    }

    private static int CompareLinks(MinimapLink a, MinimapLink b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        return string.Compare(a.LinkId, b.LinkId, StringComparison.Ordinal);
    }

    private static int CompareHierarchyOrder(MinimapRoom a, MinimapRoom b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int leftSceneHandle = a.gameObject.scene.handle;
        int rightSceneHandle = b.gameObject.scene.handle;
        if (leftSceneHandle != rightSceneHandle)
        {
            return leftSceneHandle < rightSceneHandle ? -1 : 1;
        }

        return string.Compare(
            GetHierarchyPath(a.transform),
            GetHierarchyPath(b.transform),
            StringComparison.Ordinal);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private void EnsureUniqueRoomIds()
    {
        HashSet<string> reservedIds = CollectUsedRoomIds();
        var keptIds = new HashSet<string>(StringComparer.Ordinal);
        bool changed = false;

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            string roomId = string.IsNullOrWhiteSpace(room.RoomId) ? string.Empty : room.RoomId.Trim();
            if (!string.IsNullOrEmpty(roomId) && keptIds.Add(roomId))
            {
                continue;
            }

            string nextRoomId = GenerateNextRoomId(reservedIds);
            Undo.RecordObject(room, "Assign Unique Minimap Room Id");
            room.ConfigureFreeformAuthoringFields(
                nextRoomId,
                nextRoomId,
                room.AreaPosition,
                room.AreaSize);
            EditorUtility.SetDirty(room);
            reservedIds.Add(nextRoomId);
            keptIds.Add(nextRoomId);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private void ApplyHierarchyRoomIds()
    {
        bool changed = false;

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            int row;
            int column;
            if (!TryGetHierarchyAreaAddress(room, out row, out column))
            {
                continue;
            }

            string hierarchyRoomId = BuildRoomId(row, column);
            if (string.Equals(room.RoomId, hierarchyRoomId, StringComparison.Ordinal) &&
                string.Equals(room.DisplayName, hierarchyRoomId, StringComparison.Ordinal))
            {
                continue;
            }

            Undo.RecordObject(room, "Sync Minimap Room Id With Hierarchy");
            room.ConfigureFreeformAuthoringFields(
                hierarchyRoomId,
                hierarchyRoomId,
                room.AreaPosition,
                room.AreaSize);
            EditorUtility.SetDirty(room);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static bool TryGetHierarchyAreaAddress(MinimapRoom room, out int row, out int column)
    {
        row = 0;
        column = 0;
        if (room == null)
        {
            return false;
        }

        string objectName = room.gameObject.name;
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        objectName = objectName.Trim();
        Match areaNameMatch = HierarchyAreaNamePattern.Match(objectName);
        if (!areaNameMatch.Success)
        {
            areaNameMatch = GeneratedRoomIdPattern.Match(objectName);
        }

        if (!areaNameMatch.Success)
        {
            return false;
        }

        return int.TryParse(areaNameMatch.Groups[1].Value, out row) &&
            int.TryParse(areaNameMatch.Groups[2].Value, out column) &&
            row > 0 &&
            column > 0;
    }

    private static string BuildRoomId(int row, int column)
    {
        return "Col_" + row + "-" + column;
    }

    private HashSet<string> CollectUsedRoomIds()
    {
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                continue;
            }

            usedIds.Add(room.RoomId.Trim());
        }

        return usedIds;
    }

    private static string GenerateNextRoomId(ISet<string> usedIds)
    {
        int maxColumn = 0;
        foreach (string usedId in usedIds)
        {
            if (string.IsNullOrWhiteSpace(usedId))
            {
                continue;
            }

            Match match = GeneratedRoomIdPattern.Match(usedId.Trim());
            if (!match.Success)
            {
                continue;
            }

            int row;
            int column;
            if (!int.TryParse(match.Groups[1].Value, out row) ||
                !int.TryParse(match.Groups[2].Value, out column) ||
                row != 1)
            {
                continue;
            }

            maxColumn = Mathf.Max(maxColumn, column);
        }

        int nextColumn = Mathf.Max(1, maxColumn + 1);
        string candidate;
        do
        {
            candidate = "Col_1-" + nextColumn;
            nextColumn++;
        }
        while (usedIds.Contains(candidate));

        return candidate;
    }

    private void HandleHierarchyChanged()
    {
        sceneDataDirty = true;
        Repaint();
    }

    private void HandleSelectionChanged()
    {
        GameObject activeObject = Selection.activeGameObject;
        if (activeObject == null)
        {
            return;
        }

        MinimapRoom room = activeObject.GetComponent<MinimapRoom>();
        if (room != null)
        {
            selectedRoom = room;
            selectedLink = null;
            selectedLinkPointIndex = -1;
            Repaint();
            return;
        }

        MinimapLink link = activeObject.GetComponent<MinimapLink>();
        if (link != null)
        {
            selectedLink = link;
            selectedLinkPointIndex = -1;
            Repaint();
        }
    }

    private void SelectRoom(MinimapRoom room)
    {
        selectedRoom = room;
        selectedLink = null;
        selectedLinkPointIndex = -1;
        if (room != null)
        {
            Selection.activeGameObject = room.gameObject;
        }

        Repaint();
    }

    private void SelectLink(MinimapLink link, int pointIndex)
    {
        selectedLink = link;
        selectedLinkPointIndex = pointIndex;
        if (link != null)
        {
            Selection.activeGameObject = link.gameObject;
        }

        Repaint();
    }

    private void AttachRoomsToSelection()
    {
        RefreshSceneDataIfNeeded();

        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("ミニマップエディター", "先にシーン上のオブジェクトを1つ以上選択してください。", "OK");
            return;
        }

        int addedCount = 0;
        int skippedAssetCount = 0;
        HashSet<string> usedIds = CollectUsedRoomIds();
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject gameObject = selectedObjects[i];
            if (gameObject == null)
            {
                continue;
            }

            if (EditorUtility.IsPersistent(gameObject))
            {
                skippedAssetCount++;
                continue;
            }

            MinimapRoom room = gameObject.GetComponent<MinimapRoom>();
            if (room == null)
            {
                room = Undo.AddComponent<MinimapRoom>(gameObject);
                string nextRoomId = GenerateNextRoomId(usedIds);
                room.ConfigureFreeformAuthoringFields(
                    nextRoomId,
                    nextRoomId,
                    Vector2.zero,
                    StandardRoomSize);
                usedIds.Add(nextRoomId);
                addedCount++;
            }

            Undo.RecordObject(room, "MinimapRoom を設定");
            EditorUtility.SetDirty(room);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        sceneDataDirty = true;
        frameAllRequested = true;
        RefreshSceneDataIfNeeded();

        EditorUtility.DisplayDialog(
            "ミニマップエディター",
            string.Format(
                "{0} 個のオブジェクトを確認しました。{1} 個に MinimapRoom を追加しました。アセットは {2} 個スキップしました。",
                selectedObjects.Length,
                addedCount,
                skippedAssetCount),
            "OK");
    }

    private void ConvertLegacyRooms()
    {
        RefreshSceneDataIfNeeded();

        int convertedCount = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null || room.UsesFreeformLayout)
            {
                continue;
            }

            Undo.RecordObject(room, "旧 MinimapRoom を変換");
            room.ConfigureFreeformAuthoringFields(
                room.RoomId,
                room.DisplayName,
                room.AreaPosition,
                room.AreaSize);
            EditorUtility.SetDirty(room);
            convertedCount++;
        }

        if (convertedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            sceneDataDirty = true;
            frameAllRequested = true;
            Repaint();
        }

        EditorUtility.DisplayDialog(
            "ミニマップエディター",
            string.Format("{0} 個の旧形式の部屋を自由配置レイアウトへ変換しました。", convertedCount),
            "OK");
    }

    private void NormalizeAllRoomSizes()
    {
        RefreshSceneDataIfNeeded();

        int normalizedCount = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            if (Approximately(room.AreaSize, StandardRoomSize))
            {
                continue;
            }

            Undo.RecordObject(room, "部屋サイズを統一");
            room.ConfigureFreeformAuthoringFields(
                room.RoomId,
                room.DisplayName,
                room.AreaPosition,
                StandardRoomSize);
            EditorUtility.SetDirty(room);
            normalizedCount++;
        }

        if (normalizedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            sceneDataDirty = true;
            frameAllRequested = true;
            Repaint();
        }

        EditorUtility.DisplayDialog(
            "ミニマップエディター",
            string.Format("{0} 個の部屋を 1.5 x 1.0 に統一しました。", normalizedCount),
            "OK");
    }

    private void ResetAllMinimapContent()
    {
        RefreshSceneDataIfNeeded();

        bool shouldReset = EditorUtility.DisplayDialog(
            "ミニマップエディター",
            "現在のミニマップ配置をリセットします。\n\n" +
            "- 接続線をすべて削除\n" +
            "- 全部屋を初期位置 (0, 0)\n" +
            "- 全部屋をサイズ 1.5 x 1.0\n" +
            "- 部屋ID/表示名を GameObject 名に戻す\n\n" +
            "続けますか？",
            "リセットする",
            "キャンセル");

        if (!shouldReset)
        {
            return;
        }

        int removedLinks = 0;
        for (int i = links.Count - 1; i >= 0; i--)
        {
            MinimapLink link = links[i];
            if (link == null)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(link.gameObject);
            removedLinks++;
        }

        int resetRooms = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Undo.RecordObject(room, "ミニマップ内容をリセット");
            room.ApplyEditorFriendlyDefaults();
            EditorUtility.SetDirty(room);
            resetRooms++;
        }

        pendingLinkStartRoom = null;
        selectedLink = null;
        selectedLinkPointIndex = -1;
        sceneDataDirty = true;
        frameAllRequested = true;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        RefreshSceneDataIfNeeded();
        Repaint();

        EditorUtility.DisplayDialog(
            "ミニマップエディター",
            string.Format("リセットしました。\n部屋: {0} 件\n削除した接続線: {1} 件", resetRooms, removedLinks),
            "OK");
    }

    private void CreateLink(MinimapRoom fromRoom, MinimapRoom toRoom)
    {
        if (fromRoom == null || toRoom == null)
        {
            return;
        }

        GameObject linkRoot = GetOrCreateLinkRoot();
        GameObject linkObject = new GameObject("MinimapLink_" + fromRoom.RoomId + "_" + toRoom.RoomId);
        Undo.RegisterCreatedObjectUndo(linkObject, "接続線を作成");
        linkObject.transform.SetParent(linkRoot.transform, false);

        MinimapLink link = Undo.AddComponent<MinimapLink>(linkObject);
        Undo.RecordObject(link, "接続線を作成");
        link.Configure(fromRoom, toRoom, new List<Vector2>());
        EditorUtility.SetDirty(link);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        sceneDataDirty = true;
        RefreshSceneDataIfNeeded();
        SelectLink(link, -1);
    }

    private void DeleteSelectedLink()
    {
        if (selectedLink == null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(selectedLink.gameObject);
        selectedLink = null;
        selectedLinkPointIndex = -1;
        sceneDataDirty = true;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Repaint();
    }

    private void BakeAutoCorridors()
    {
        RefreshSceneDataIfNeeded();

        int createdCount = 0;
        for (int i = 0; i < autoCorridorPreviews.Count; i++)
        {
            AutoCorridorPreview preview = autoCorridorPreviews[i];
            if (preview.FromRoom == null || preview.ToRoom == null)
            {
                continue;
            }

            if (HasManualLinkBetween(preview.FromRoom, preview.ToRoom))
            {
                continue;
            }

            CreateLink(preview.FromRoom, preview.ToRoom);
            createdCount++;
        }

        if (createdCount == 0)
        {
            EditorUtility.DisplayDialog("ミニマップエディター", "変換できる自動接続線はありませんでした。", "OK");
            return;
        }

        sceneDataDirty = true;
        RefreshSceneDataIfNeeded();
        EditorUtility.DisplayDialog(
            "ミニマップエディター",
            string.Format("{0} 本の自動接続線を、編集できる接続線に変換しました。", createdCount),
            "OK");
    }

    private GameObject GetOrCreateLinkRoot()
    {
        GameObject existingRoot = GameObject.Find("MinimapLinks");
        if (existingRoot != null)
        {
            return existingRoot;
        }

        GameObject root = new GameObject("MinimapLinks");
        Undo.RegisterCreatedObjectUndo(root, "Create Minimap Link Root");
        return root;
    }

    private void ApplyRoomFields(
        MinimapRoom room,
        string nextRoomId,
        string nextDisplayName,
        Vector2 nextAreaPosition,
        Vector2 nextAreaSize,
        string undoLabel)
    {
        if (room == null)
        {
            return;
        }

        nextAreaSize = new Vector2(
            Mathf.Max(MinRoomSize, nextAreaSize.x),
            Mathf.Max(MinRoomSize, nextAreaSize.y));

        if (room.RoomId == nextRoomId &&
            room.DisplayName == nextDisplayName &&
            room.AreaPosition == nextAreaPosition &&
            room.AreaSize == nextAreaSize)
        {
            return;
        }

        if (!string.IsNullOrEmpty(undoLabel))
        {
            Undo.RecordObject(room, undoLabel);
        }

        room.ConfigureFreeformAuthoringFields(nextRoomId, nextDisplayName, nextAreaPosition, nextAreaSize);
        EditorUtility.SetDirty(room);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        sceneDataDirty = true;
    }

    private void ApplyLinkFields(
        MinimapLink link,
        MinimapRoom nextFromRoom,
        MinimapRoom nextToRoom,
        IList<Vector2> nextPathPoints,
        string undoLabel)
    {
        if (link == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(undoLabel))
        {
            Undo.RecordObject(link, undoLabel);
        }

        link.Configure(nextFromRoom, nextToRoom, nextPathPoints);
        EditorUtility.SetDirty(link);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        sceneDataDirty = true;
    }

    private void SetLinkPoints(MinimapLink link, IList<Vector2> nextPathPoints)
    {
        if (link == null)
        {
            return;
        }

        link.SetPathPoints(nextPathPoints);
        EditorUtility.SetDirty(link);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        sceneDataDirty = true;
    }

    private void AddPointToSelectedLink()
    {
        if (selectedLink == null || !selectedLink.IsValid)
        {
            return;
        }

        List<Vector2> nextPathPoints = CopyPathPoints(selectedLink);
        Vector2 nextPoint = CalculateDefaultNewPoint(selectedLink);
        nextPathPoints.Add(nextPoint);
        ApplyLinkFields(selectedLink, selectedLink.FromRoom, selectedLink.ToRoom, nextPathPoints, "Add Minimap Link Point");
        selectedLinkPointIndex = nextPathPoints.Count - 1;
    }

    private void RemoveSelectedLinkPoint()
    {
        if (selectedLink == null || selectedLinkPointIndex < 0)
        {
            return;
        }

        List<Vector2> nextPathPoints = CopyPathPoints(selectedLink);
        if (selectedLinkPointIndex >= nextPathPoints.Count)
        {
            return;
        }

        nextPathPoints.RemoveAt(selectedLinkPointIndex);
        ApplyLinkFields(selectedLink, selectedLink.FromRoom, selectedLink.ToRoom, nextPathPoints, "Remove Minimap Link Point");
        selectedLinkPointIndex = Mathf.Clamp(selectedLinkPointIndex - 1, -1, nextPathPoints.Count - 1);
    }

    private int InsertPointOnLink(MinimapLink link, int segmentIndex, Vector2 boardPoint)
    {
        if (link == null)
        {
            return -1;
        }

        List<Vector2> nextPathPoints = CopyPathPoints(link);
        int insertIndex = Mathf.Clamp(segmentIndex, 0, nextPathPoints.Count);
        nextPathPoints.Insert(insertIndex, boardPoint);
        ApplyLinkFields(link, link.FromRoom, link.ToRoom, nextPathPoints, "Insert Minimap Link Point");
        return insertIndex;
    }

    private void BeginLinkSegmentDrag(MinimapLink link, int segmentIndex, Vector2 mousePosition)
    {
        if (link == null || !link.IsValid)
        {
            return;
        }

        List<Vector2> pathPoints = CopyPathPoints(link);
        activeLinkDraggedPointIndices.Clear();
        bool createdDefaultPoints = false;

        if (pathPoints.Count == 0)
        {
            List<Vector2> polyline = BuildDisplayedPolyline(link);
            if (polyline.Count >= 2)
            {
                pathPoints.Add(Vector2.Lerp(polyline[0], polyline[1], 1f / 3f));
                pathPoints.Add(Vector2.Lerp(polyline[0], polyline[1], 2f / 3f));
                ApplyLinkFields(link, link.FromRoom, link.ToRoom, pathPoints, "Create Corridor Control Points");
                createdDefaultPoints = true;
            }
        }

        pathPoints = CopyPathPoints(link);
        if (pathPoints.Count == 0)
        {
            return;
        }

        if (createdDefaultPoints)
        {
            activeLinkDraggedPointIndices.Add(0);
            activeLinkDraggedPointIndices.Add(1);
        }
        else
        {
        int lastSegmentIndex = pathPoints.Count;
        if (segmentIndex <= 0)
        {
            activeLinkDraggedPointIndices.Add(0);
        }
        else if (segmentIndex >= lastSegmentIndex)
        {
            activeLinkDraggedPointIndices.Add(pathPoints.Count - 1);
        }
        else
        {
            activeLinkDraggedPointIndices.Add(segmentIndex - 1);
            activeLinkDraggedPointIndices.Add(segmentIndex);
        }
        }

        dragStartLinkPoints.Clear();
        dragStartLinkPoints.AddRange(pathPoints);
        dragStartMousePosition = mousePosition;
        interactionMode = InteractionMode.DraggingLinkSegment;
        selectedLinkPointIndex = -1;
    }

    private Vector2 CalculateDefaultNewPoint(MinimapLink link)
    {
        List<Vector2> polyline = BuildDisplayedPolyline(link);
        float longestDistance = -1f;
        Vector2 bestPoint = (link.FromRoom.AreaPosition + link.ToRoom.AreaPosition) * 0.5f;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            float distance = Vector2.Distance(polyline[i], polyline[i + 1]);
            if (distance <= longestDistance)
            {
                continue;
            }

            longestDistance = distance;
            bestPoint = (polyline[i] + polyline[i + 1]) * 0.5f;
        }

        return bestPoint;
    }

    private Rect RoomToCanvasRect(Rect canvasRect, MinimapRoom room)
    {
        Vector2 topLeft = BoardToCanvas(canvasRect, room.AreaPosition);
        Vector2 size = room.AreaSize * BoardScale;
        return new Rect(topLeft.x, topLeft.y, size.x, size.y);
    }

    private Vector2 BoardToCanvas(Rect canvasRect, Vector2 boardPoint)
    {
        Vector2 origin = GetCanvasOrigin(canvasRect);
        return origin + (boardPoint * BoardScale);
    }

    private Vector2 CanvasToBoard(Rect canvasRect, Vector2 canvasPoint)
    {
        Vector2 origin = GetCanvasOrigin(canvasRect);
        return (canvasPoint - origin) / BoardScale;
    }

    private Vector2 GetCanvasOrigin(Rect canvasRect)
    {
        return canvasRect.center + panOffset;
    }

    private void ZoomAroundCanvasPoint(Rect canvasRect, Vector2 canvasPoint, float nextZoom)
    {
        nextZoom = Mathf.Clamp(nextZoom, MinZoom, MaxZoom);
        if (Mathf.Approximately(nextZoom, zoom))
        {
            return;
        }

        float previousScale = BaseBoardScale * zoom;
        float nextScale = BaseBoardScale * nextZoom;
        if (previousScale <= 0.0001f || nextScale <= 0.0001f)
        {
            zoom = nextZoom;
            return;
        }

        Vector2 previousOrigin = GetCanvasOrigin(canvasRect);
        Vector2 boardPoint = (canvasPoint - previousOrigin) / previousScale;

        zoom = nextZoom;
        panOffset = canvasPoint - canvasRect.center - (boardPoint * nextScale);
    }

    private Rect GetRightResizeHandleRect(Rect roomRect)
    {
        return new Rect(
            roomRect.xMax - ResizeHandleSize,
            roomRect.center.y - (ResizeHandleSize * 0.5f),
            ResizeHandleSize,
            ResizeHandleSize);
    }

    private Rect GetBottomResizeHandleRect(Rect roomRect)
    {
        return new Rect(
            roomRect.center.x - (ResizeHandleSize * 0.5f),
            roomRect.yMax - ResizeHandleSize,
            ResizeHandleSize,
            ResizeHandleSize);
    }

    private Rect GetBottomRightResizeHandleRect(Rect roomRect)
    {
        return new Rect(
            roomRect.xMax - ResizeHandleSize,
            roomRect.yMax - ResizeHandleSize,
            ResizeHandleSize,
            ResizeHandleSize);
    }

    private ResizeHandle GetHitResizeHandle(Rect roomRect, Vector2 mousePosition)
    {
        if (GetBottomRightResizeHandleRect(roomRect).Contains(mousePosition))
        {
            return ResizeHandle.BottomRight;
        }

        if (GetRightResizeHandleRect(roomRect).Contains(mousePosition))
        {
            return ResizeHandle.Right;
        }

        if (GetBottomResizeHandleRect(roomRect).Contains(mousePosition))
        {
            return ResizeHandle.Bottom;
        }

        return ResizeHandle.None;
    }

    private void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }

    private bool TryHitRoom(Rect canvasRect, Vector2 mousePosition, out MinimapRoom room)
    {
        Rect roomRect;
        return TryHitRoom(canvasRect, mousePosition, out room, out roomRect);
    }

    private bool TryHitRoom(Rect canvasRect, Vector2 mousePosition, out MinimapRoom room, out Rect roomRect)
    {
        if (selectedRoom != null)
        {
            Rect selectedRect = RoomToCanvasRect(canvasRect, selectedRoom);
            if (selectedRect.Contains(mousePosition))
            {
                room = selectedRoom;
                roomRect = selectedRect;
                return true;
            }
        }

        for (int i = rooms.Count - 1; i >= 0; i--)
        {
            MinimapRoom candidate = rooms[i];
            if (candidate == null || candidate == selectedRoom)
            {
                continue;
            }

            Rect candidateRect = RoomToCanvasRect(canvasRect, candidate);
            if (!candidateRect.Contains(mousePosition))
            {
                continue;
            }

            room = candidate;
            roomRect = candidateRect;
            return true;
        }

        room = null;
        roomRect = default(Rect);
        return false;
    }

    private bool TryHitLinkPoint(Rect canvasRect, Vector2 mousePosition, out MinimapLink link, out int pointIndex)
    {
        for (int i = 0; i < links.Count; i++)
        {
            MinimapLink candidate = links[i];
            if (candidate == null || !candidate.IsValid)
            {
                continue;
            }

            IReadOnlyList<Vector2> pathPoints = candidate.PathPoints;
            for (int currentIndex = 0; currentIndex < pathPoints.Count; currentIndex++)
            {
                Vector2 screenPoint = BoardToCanvas(canvasRect, pathPoints[currentIndex]);
                if (Vector2.Distance(screenPoint, mousePosition) > PointHandleRadius + 3f)
                {
                    continue;
                }

                link = candidate;
                pointIndex = currentIndex;
                return true;
            }
        }

        link = null;
        pointIndex = -1;
        return false;
    }

    private bool TryHitLinkSegment(Rect canvasRect, Vector2 mousePosition, out MinimapLink link, out int segmentIndex)
    {
        for (int i = links.Count - 1; i >= 0; i--)
        {
            MinimapLink candidate = links[i];
            if (candidate == null || !candidate.IsValid)
            {
                continue;
            }

            List<Vector2> polyline = BuildDisplayedPolyline(candidate);
            for (int currentSegmentIndex = 0; currentSegmentIndex < polyline.Count - 1; currentSegmentIndex++)
            {
                Vector2 start = BoardToCanvas(canvasRect, polyline[currentSegmentIndex]);
                Vector2 end = BoardToCanvas(canvasRect, polyline[currentSegmentIndex + 1]);
                if (DistanceToSegment(mousePosition, start, end) > SegmentHitDistance)
                {
                    continue;
                }

                link = candidate;
                segmentIndex = currentSegmentIndex;
                return true;
            }
        }

        link = null;
        segmentIndex = -1;
        return false;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        Vector2 projection = start + (segment * t);
        return Vector2.Distance(point, projection);
    }

    private List<Vector2> CopyPathPoints(MinimapLink link)
    {
        var copy = new List<Vector2>();
        if (link == null || link.PathPoints == null)
        {
            return copy;
        }

        for (int i = 0; i < link.PathPoints.Count; i++)
        {
            copy.Add(link.PathPoints[i]);
        }

        return copy;
    }

    private List<Vector2> BuildDisplayedPolyline(MinimapLink link)
    {
        if (link == null || !link.IsValid)
        {
            return new List<Vector2>();
        }

        return BuildDisplayedPolyline(link.FromRoom, link.ToRoom, link.PathPoints);
    }

    private List<Vector2> BuildDisplayedPolyline(MinimapRoom fromRoom, MinimapRoom toRoom, IReadOnlyList<Vector2> middle)
    {
        var polyline = new List<Vector2>();
        if (fromRoom == null || toRoom == null)
        {
            return polyline;
        }

        Vector2 startTarget = middle != null && middle.Count > 0 ? middle[0] : toRoom.AreaPosition + (toRoom.AreaSize * 0.5f);
        Vector2 endTarget = middle != null && middle.Count > 0 ? middle[middle.Count - 1] : fromRoom.AreaPosition + (fromRoom.AreaSize * 0.5f);

        polyline.Add(ClosestPointOnRoom(fromRoom, startTarget));
        if (middle != null)
        {
            for (int i = 0; i < middle.Count; i++)
            {
                polyline.Add(middle[i]);
            }
        }

        polyline.Add(ClosestPointOnRoom(toRoom, endTarget));
        return polyline;
    }

    private Vector2 ClosestPointOnRoom(MinimapRoom room, Vector2 target)
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

    private void FrameAll(Rect canvasRect)
    {
        if (rooms.Count == 0)
        {
            panOffset = Vector2.zero;
            zoom = 1f;
            return;
        }

        bool hasBounds = false;
        float minX = 0f;
        float minY = 0f;
        float maxX = 0f;
        float maxY = 0f;

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Vector2 roomMin = room.AreaPosition;
            Vector2 roomMax = room.AreaPosition + room.AreaSize;

            if (!hasBounds)
            {
                minX = roomMin.x;
                minY = roomMin.y;
                maxX = roomMax.x;
                maxY = roomMax.y;
                hasBounds = true;
                continue;
            }

            minX = Mathf.Min(minX, roomMin.x);
            minY = Mathf.Min(minY, roomMin.y);
            maxX = Mathf.Max(maxX, roomMax.x);
            maxY = Mathf.Max(maxY, roomMax.y);
        }

        for (int i = 0; i < links.Count; i++)
        {
            MinimapLink link = links[i];
            if (link == null || link.PathPoints == null)
            {
                continue;
            }

            for (int pointIndex = 0; pointIndex < link.PathPoints.Count; pointIndex++)
            {
                Vector2 point = link.PathPoints[pointIndex];
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
        }

        minX -= FramePadding;
        minY -= FramePadding;
        maxX += FramePadding;
        maxY += FramePadding;

        float width = Mathf.Max(1f, maxX - minX);
        float height = Mathf.Max(1f, maxY - minY);
        float zoomX = (canvasRect.width - 48f) / (width * BaseBoardScale);
        float zoomY = (canvasRect.height - 48f) / (height * BaseBoardScale);
        zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY), MinZoom, MaxZoom);

        Vector2 boardCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        panOffset = -(boardCenter * BoardScale);
    }

    private List<string> FindDuplicateRoomIds()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                continue;
            }

            int currentCount;
            counts.TryGetValue(room.RoomId, out currentCount);
            counts[room.RoomId] = currentCount + 1;
        }

        var duplicates = new List<string>();
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > 1)
            {
                duplicates.Add(pair.Key);
            }
        }

        duplicates.Sort(StringComparer.Ordinal);
        return duplicates;
    }

    private List<string> FindOverlaps()
    {
        var overlaps = new List<string>();
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom a = rooms[i];
            if (a == null)
            {
                continue;
            }

            for (int j = i + 1; j < rooms.Count; j++)
            {
                MinimapRoom b = rooms[j];
                if (b == null || !RoomsOverlap(a, b))
                {
                    continue;
                }

                overlaps.Add(string.Format("{0} <-> {1}", a.RoomId, b.RoomId));
            }
        }

        return overlaps;
    }

    private List<string> FindInvalidLinks()
    {
        var invalid = new List<string>();
        for (int i = 0; i < links.Count; i++)
        {
            MinimapLink link = links[i];
            if (link == null)
            {
                continue;
            }

            if (!link.IsValid)
            {
                invalid.Add(link.name);
            }
        }

        return invalid;
    }

    private HashSet<MinimapRoom> GetOverlappingRooms()
    {
        var set = new HashSet<MinimapRoom>();
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom a = rooms[i];
            if (a == null)
            {
                continue;
            }

            for (int j = i + 1; j < rooms.Count; j++)
            {
                MinimapRoom b = rooms[j];
                if (b == null || !RoomsOverlap(a, b))
                {
                    continue;
                }

                set.Add(a);
                set.Add(b);
            }
        }

        return set;
    }

    private bool RoomsOverlap(MinimapRoom a, MinimapRoom b)
    {
        Rect rectA = new Rect(a.AreaPosition, a.AreaSize);
        Rect rectB = new Rect(b.AreaPosition, b.AreaSize);
        return rectA.Overlaps(rectB, true);
    }

    private void RebuildAutoCorridorPreviews()
    {
        autoCorridorPreviews.Clear();

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom a = rooms[i];
            if (a == null)
            {
                continue;
            }

            for (int j = i + 1; j < rooms.Count; j++)
            {
                MinimapRoom b = rooms[j];
                if (b == null)
                {
                    continue;
                }

                if (!RoomsTouchForAutoCorridor(a, b) || HasManualLinkBetween(a, b))
                {
                    continue;
                }

                autoCorridorPreviews.Add(new AutoCorridorPreview(a, b));
            }
        }
    }

    private void DrawToolbarLocalized()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                sceneDataDirty = true;
                RefreshSceneDataIfNeeded();
            }

            if (GUILayout.Button("全体表示", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                frameAllRequested = true;
                Repaint();
            }

            if (GUILayout.Button("選択対象に MinimapRoom を追加", EditorStyles.toolbarButton, GUILayout.Width(200f)))
            {
                AttachRoomsToSelection();
            }

            if (GUILayout.Button("旧部屋を変換", EditorStyles.toolbarButton, GUILayout.Width(140f)))
            {
                ConvertLegacyRooms();
            }

            if (GUILayout.Button("シーン配置から配置", EditorStyles.toolbarButton, GUILayout.Width(160f)))
            {
                LayoutRoomsFromScenePlacement();
            }

            if (GUILayout.Button("Hierarchy Layout", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            {
                LayoutRoomsFromHierarchyNames();
            }

            if (GUILayout.Button("全部屋を 1.5 x 1 に統一", EditorStyles.toolbarButton, GUILayout.Width(165f)))
            {
                NormalizeAllRoomSizes();
            }

            if (GUILayout.Button("内容をリセット", EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                ResetAllMinimapContent();
            }

            GUILayout.Space(14f);
            GUILayout.Label("ズーム", GUILayout.Width(40f));

            float nextZoom = GUILayout.HorizontalSlider(zoom, MinZoom, MaxZoom, GUILayout.Width(130f));
            if (!Mathf.Approximately(nextZoom, zoom))
            {
                if (lastCanvasRect.width > 0f && lastCanvasRect.height > 0f)
                {
                    ZoomAroundCanvasPoint(lastCanvasRect, lastCanvasRect.center, nextZoom);
                }
                else
                {
                    zoom = nextZoom;
                }

                Repaint();
            }

            GUILayout.Label(string.Format("{0:0.00}x", zoom), GUILayout.Width(50f));
            GUILayout.FlexibleSpace();

            if (pendingLinkStartRoom != null)
            {
                GUILayout.Label("接続先の部屋をクリックしてください", EditorStyles.miniLabel);
                if (GUILayout.Button("接続をキャンセル", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                {
                    pendingLinkStartRoom = null;
                }
            }
            else
            {
                GUILayout.Label(string.Format("部屋: {0}  線: {1}", rooms.Count, links.Count), EditorStyles.miniLabel);
            }
        }
    }

    private void LayoutRoomsFromHierarchyNames()
    {
        sceneDataDirty = true;
        RefreshSceneDataIfNeeded();

        const float horizontalSpacing = 2.05f;
        const float verticalSpacing = 1.45f;

        int positionedCount = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            int row;
            int column;
            if (!TryGetHierarchyAreaAddress(room, out row, out column))
            {
                continue;
            }

            string roomId = BuildRoomId(row, column);
            Vector2 boardPosition = new Vector2(
                (column - 1) * horizontalSpacing,
                (row - 1) * verticalSpacing);

            Undo.RecordObject(room, "Layout Minimap Rooms From Hierarchy");
            room.ConfigureFreeformAuthoringFields(
                roomId,
                roomId,
                boardPosition,
                StandardRoomSize);
            EditorUtility.SetDirty(room);
            positionedCount++;
        }

        if (positionedCount == 0)
        {
            EditorUtility.DisplayDialog("ミニマップエディター", "Hierarchy 名が 1-1 形式の MinimapRoom がありません。", "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        sceneDataDirty = true;
        frameAllRequested = true;
        RefreshSceneDataIfNeeded();
        Repaint();

        EditorUtility.DisplayDialog(
            "ミニマップエディター",
            string.Format("{0} 個の部屋を Hierarchy 名に合わせて配置しました。", positionedCount),
            "OK");
    }

    private void LayoutRoomsFromScenePlacement()
    {
        RefreshSceneDataIfNeeded();

        if (rooms.Count == 0)
        {
            EditorUtility.DisplayDialog("ミニマップエディター", "配置に使える MinimapRoom がありません。", "OK");
            return;
        }

        List<Vector2> boardPositions = new List<Vector2>(rooms.Count);
        List<Vector2> boardSizes = new List<Vector2>(rooms.Count);
        List<Vector2> sceneCenters = new List<Vector2>(rooms.Count);
        List<Vector2> sceneFootprints = new List<Vector2>(rooms.Count);
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;

        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                boardPositions.Add(Vector2.zero);
                boardSizes.Add(StandardRoomSize);
                sceneCenters.Add(Vector2.zero);
                sceneFootprints.Add(Vector2.one);
                continue;
            }

            Vector2 sceneCenter;
            Vector2 sceneFootprint;
            GetScenePlacement(room, out sceneCenter, out sceneFootprint);
            sceneCenters.Add(sceneCenter);
            sceneFootprints.Add(sceneFootprint);
            boardPositions.Add(Vector2.zero);
            boardSizes.Add(StandardRoomSize);
        }

        Vector2 sceneScale = ComputeScenePlacementScale(sceneFootprints);

        for (int i = 0; i < rooms.Count; i++)
        {
            Vector2 normalizedSize = StandardRoomSize;
            Vector2 scaledCenter = new Vector2(
                sceneCenters[i].x * sceneScale.x * ScenePlacementSpacingFactor,
                sceneCenters[i].y * sceneScale.y * ScenePlacementSpacingFactor);

            Vector2 boardPosition = new Vector2(
                scaledCenter.x - (normalizedSize.x * 0.5f),
                -(scaledCenter.y + (normalizedSize.y * 0.5f)));

            boardPositions[i] = boardPosition;
            boardSizes[i] = normalizedSize;
            minX = Mathf.Min(minX, boardPosition.x);
            minY = Mathf.Min(minY, boardPosition.y);
        }

        if (float.IsInfinity(minX) || float.IsInfinity(minY))
        {
            EditorUtility.DisplayDialog("ミニマップエディター", "シーン配置の読み取りに失敗しました。", "OK");
            return;
        }

        Vector2 offset = new Vector2(-minX, -minY);
        int positionedCount = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Undo.RecordObject(room, "シーン配置からミニマップ配置");
            room.ConfigureFreeformAuthoringFields(
                room.RoomId,
                room.DisplayName,
                boardPositions[i] + offset,
                boardSizes[i]);
            EditorUtility.SetDirty(room);
            positionedCount++;
        }

        if (positionedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            sceneDataDirty = true;
            frameAllRequested = true;
            Repaint();
        }

        EditorUtility.DisplayDialog(
            "ミニマップエディター",
            string.Format("{0} 個の部屋をシーン上の配置をもとに並べました。", positionedCount),
            "OK");
    }

    private static void GetScenePlacement(MinimapRoom room, out Vector2 center, out Vector2 footprint)
    {
        Bounds bounds;
        if (TryGetCombinedBounds(room.gameObject, out bounds))
        {
            center = new Vector2(bounds.center.x, bounds.center.y);
            footprint = new Vector2(
                Mathf.Max(0.01f, bounds.size.x),
                Mathf.Max(0.01f, bounds.size.y));
            return;
        }

        Vector3 worldPosition = room.transform.position;
        center = new Vector2(worldPosition.x, worldPosition.y);
        footprint = Vector2.one;
    }

    private static Vector2 ComputeScenePlacementScale(List<Vector2> sceneFootprints)
    {
        float typicalWidth = ComputeMedianPositive(sceneFootprints, useX: true);
        float typicalHeight = ComputeMedianPositive(sceneFootprints, useX: false);

        typicalWidth = Mathf.Max(0.01f, typicalWidth);
        typicalHeight = Mathf.Max(0.01f, typicalHeight);

        return new Vector2(
            StandardRoomSize.x / typicalWidth,
            StandardRoomSize.y / typicalHeight);
    }

    private static float ComputeMedianPositive(List<Vector2> values, bool useX)
    {
        List<float> positives = new List<float>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            float candidate = useX ? values[i].x : values[i].y;
            if (candidate > 0.01f)
            {
                positives.Add(candidate);
            }
        }

        if (positives.Count == 0)
        {
            return 1f;
        }

        positives.Sort();
        int middleIndex = positives.Count / 2;
        if ((positives.Count & 1) == 1)
        {
            return positives[middleIndex];
        }

        return (positives[middleIndex - 1] + positives[middleIndex]) * 0.5f;
    }

    private static bool TryGetCombinedBounds(GameObject target, out Bounds bounds)
    {
        Collider2D[] colliders2D = target.GetComponents<Collider2D>();
        bool hasBounds = false;
        bounds = default;

        for (int i = 0; i < colliders2D.Length; i++)
        {
            Collider2D collider2D = colliders2D[i];
            if (collider2D == null || !collider2D.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider2D.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider2D.bounds);
            }
        }

        Collider[] colliders3D = target.GetComponents<Collider>();
        for (int i = 0; i < colliders3D.Length; i++)
        {
            Collider collider3D = colliders3D[i];
            if (collider3D == null || !collider3D.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider3D.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider3D.bounds);
            }
        }

        return hasBounds;
    }

    private bool HasManualLinkBetween(MinimapRoom a, MinimapRoom b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        for (int i = 0; i < links.Count; i++)
        {
            MinimapLink link = links[i];
            if (link == null || !link.IsValid || link.FromRoom == null || link.ToRoom == null)
            {
                continue;
            }

            bool sameDirection = link.FromRoom == a && link.ToRoom == b;
            bool reverseDirection = link.FromRoom == b && link.ToRoom == a;
            if (sameDirection || reverseDirection)
            {
                return true;
            }
        }

        return false;
    }

    private bool RoomsTouchForAutoCorridor(MinimapRoom a, MinimapRoom b)
    {
        Rect rectA = new Rect(a.AreaPosition, a.AreaSize);
        Rect rectB = new Rect(b.AreaPosition, b.AreaSize);

        bool touchesHorizontally =
            (Mathf.Abs(rectA.xMax - rectB.xMin) < 0.001f || Mathf.Abs(rectA.xMin - rectB.xMax) < 0.001f) &&
            RangesOverlap(rectA.yMin, rectA.yMax, rectB.yMin, rectB.yMax);

        if (touchesHorizontally)
        {
            return true;
        }

        bool touchesVertically =
            (Mathf.Abs(rectA.yMax - rectB.yMin) < 0.001f || Mathf.Abs(rectA.yMin - rectB.yMax) < 0.001f) &&
            RangesOverlap(rectA.xMin, rectA.xMax, rectB.xMin, rectB.xMax);

        return touchesVertically;
    }

    private static bool RangesOverlap(float aMin, float aMax, float bMin, float bMax)
    {
        return aMin < bMax && bMin < aMax;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f &&
            Mathf.Abs(a.y - b.y) < 0.0001f;
    }

    private static string BuildRoomListLabel(MinimapRoom room)
    {
        if (room == null)
        {
            return "(Missing Room)";
        }

        return GetEditorRoomLabel(room);
    }

    private static string GetEditorRoomLabel(MinimapRoom room)
    {
        if (room == null)
        {
            return "?";
        }

        string roomId = string.IsNullOrWhiteSpace(room.RoomId) ? room.gameObject.name : room.RoomId;
        string displayName = string.IsNullOrWhiteSpace(room.DisplayName) ? roomId : room.DisplayName;

        if (string.Equals(displayName, roomId, StringComparison.Ordinal))
        {
            return BeautifyRoomLabel(roomId);
        }

        return displayName;
    }

    private static string BeautifyRoomLabel(string rawLabel)
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
        {
            return "エリア";
        }

        string label = rawLabel.Trim();
        if (label.StartsWith("Col_", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = label.Substring(4).Trim();
            return string.IsNullOrEmpty(suffix) ? "エリア" : "エリア " + suffix;
        }

        if (label.StartsWith("Col-", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = label.Substring(4).Trim();
            return string.IsNullOrEmpty(suffix) ? "エリア" : "エリア " + suffix;
        }

        return label;
    }

    private static int CompareNatural(string a, string b)
    {
        string left = a ?? string.Empty;
        string right = b ?? string.Empty;
        int leftIndex = 0;
        int rightIndex = 0;

        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            char leftChar = left[leftIndex];
            char rightChar = right[rightIndex];

            if (char.IsDigit(leftChar) && char.IsDigit(rightChar))
            {
                long leftNumber = ReadNumber(left, ref leftIndex);
                long rightNumber = ReadNumber(right, ref rightIndex);
                if (leftNumber != rightNumber)
                {
                    return leftNumber < rightNumber ? -1 : 1;
                }

                continue;
            }

            int charCompare = char.ToUpperInvariant(leftChar).CompareTo(char.ToUpperInvariant(rightChar));
            if (charCompare != 0)
            {
                return charCompare;
            }

            leftIndex++;
            rightIndex++;
        }

        return left.Length.CompareTo(right.Length);
    }

    private static long ReadNumber(string text, ref int index)
    {
        long value = 0;
        while (index < text.Length && char.IsDigit(text[index]))
        {
            value = (value * 10L) + (text[index] - '0');
            index++;
        }

        return value;
    }
}
