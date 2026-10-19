using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DecorationScrollInstaller
{
    private const string PrefabPath = "Assets/Prefabs/UI/OptionsCanvas.prefab";
    private const string SlotPrefabPath = "Assets/Prefabs/UI/DecorationItemSlot.prefab";

    private const int Columns = 3;

    // Padding / spacing measured off the approved decoration screenshot: column pitch
    // is 214 units and row pitch 308, which these values reproduce for this panel.
    private const int PadH = 34, PadV = 10, Gap = 2;

    // ビューポートを背景の下端より少しだけ下に伸ばし、次の行のカードの上部が覗くようにする。
    // これが無いと2行がちょうど収まってしまい、スクロールできることが分からない。
    private const float ViewportBottomExtension = 50f;

    // Authored size of DecorationItemSlot.prefab. The grid forces its cell size onto
    // every card, so the cell must keep this aspect — the card art (ItemCardBg) is drawn
    // at a fixed size around the cell centre, so a cell shorter than the art makes the
    // name and cost labels spill over the row below.
    //
    // The card art does NOT follow the cell size: it is sized by the slot prefab's root
    // localScale (1.10, which draws it at ~95% of the cell pitch). Changing the cell size
    // here means changing that scale by the same factor, or the cards overlap / drift apart.
    private const float CardDesignWidth = 180f;
    private const float CardDesignHeight = 260f;

    static DecorationScrollInstaller()
    {
        EditorApplication.delayCall += TryInstall;
    }

    [MenuItem("Tools/Decoration Scroll/Force Reinstall")]
    private static void ForceReinstall()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null) { Debug.LogError("[DecorationScrollInstaller] Prefab not found."); return; }
        try   { FullSetup(prefabRoot); }
        finally { PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath); PrefabUtility.UnloadPrefabContents(prefabRoot); }
        Debug.Log("[DecorationScrollInstaller] Force reinstall complete.");
    }

    private static void TryInstall()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null) return;

        bool changed = false;
        try
        {
            Transform itemBg = prefabRoot.transform.Find("MenuRoot/OptionPanel/Decoration/Item back ground");
            if (itemBg == null) return;

            // ScrollRect needs localScale == (1,1,1) on its own GameObject.
            // Non-unit scale causes a world↔local coordinate mismatch that breaks
            // scroll wheel hit detection. Bake the scale into sizeDelta instead.
            changed |= BakeScaleIntoSize(itemBg as RectTransform);

            ScrollRect existing = itemBg.GetComponent<ScrollRect>();
            bool alreadyDone = existing != null
                && existing.viewport != null
                && existing.content != null;

            if (alreadyDone)
            {
                changed |= FixViewportRT(existing.viewport);
                changed |= FixContentRT(existing.content);
                changed |= FixGridLayout(existing.content, itemBg as RectTransform);
                changed |= EnsureDecorationPageSlotContainer(prefabRoot, existing.content);
                changed |= EnsureDecorationPageSlotPrefab(prefabRoot);
                return; // finally handles save/unload
            }

            FullSetup(prefabRoot);
            changed = true;
        }
        finally
        {
            if (changed)
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void FullSetup(GameObject prefabRoot)
    {
        Transform itemBg = prefabRoot.transform.Find("MenuRoot/OptionPanel/Decoration/Item back ground");
        if (itemBg == null) return;

        // --- Remove broken ScrollViewport if present ---
        // Rescue children before destroying so Unknown items inside don't become
        // dangling references (MissingReferenceException on SetParent).
        Transform brokenViewport = itemBg.Find("ScrollViewport");
        if (brokenViewport != null)
        {
            while (brokenViewport.childCount > 0)
                brokenViewport.GetChild(0).SetParent(itemBg, false);
            Object.DestroyImmediate(brokenViewport.gameObject);
        }

        // --- Collect Unknown items from anywhere under Decoration ---
        Transform decoration = prefabRoot.transform.Find("MenuRoot/OptionPanel/Decoration");
        var unknownItems = new System.Collections.Generic.List<Transform>();
        CollectUnknownItems(decoration, unknownItems);

        // Remove and re-add ScrollRect for a clean state
        ScrollRect oldSR = itemBg.GetComponent<ScrollRect>();
        if (oldSR != null) Object.DestroyImmediate(oldSR);

        // ScrollViewport — fills Item back ground, clips overflow with RectMask2D
        GameObject viewportGO = new GameObject("ScrollViewport");
        viewportGO.layer = LayerMask.NameToLayer("UI");
        viewportGO.transform.SetParent(itemBg, false);
        viewportGO.transform.SetAsFirstSibling();
        RectTransform viewportRT = viewportGO.AddComponent<RectTransform>();
        ApplyViewportRT(viewportRT);
        viewportGO.AddComponent<RectMask2D>();

        // Content — top-anchored, grows downward, 3-column grid
        GameObject contentGO = new GameObject("Content");
        contentGO.layer = LayerMask.NameToLayer("UI");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        ApplyContentRT(contentRT);

        GridLayoutGroup grid = contentGO.AddComponent<GridLayoutGroup>();
        ApplyGridLayout(grid, itemBg as RectTransform);

        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Move Unknown items into Content
        foreach (Transform item in unknownItems)
        {
            item.SetParent(contentGO.transform, false);
            RectTransform rt = item.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        // ScrollRect — Item back ground needs raycastTarget=true to receive wheel events
        Image bgImage = itemBg.GetComponent<Image>();
        if (bgImage != null) bgImage.raycastTarget = true;

        ScrollRect scrollRect = itemBg.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 30f;

        EnsureDecorationPageSlotContainer(prefabRoot, contentRT);
        EnsureDecorationPageSlotPrefab(prefabRoot);

        Debug.Log($"[DecorationScrollInstaller] Full setup complete. {unknownItems.Count} items in Content.");
    }

    // Bakes a non-unit localScale into sizeDelta so the ScrollRect's GameObject
    // always has scale (1,1,1). Returns true if a change was made.
    private static bool BakeScaleIntoSize(RectTransform rt)
    {
        if (rt == null || rt.localScale == Vector3.one) return false;
        rt.sizeDelta = new Vector2(
            rt.sizeDelta.x * rt.localScale.x,
            rt.sizeDelta.y * rt.localScale.y);
        rt.localScale = Vector3.one;
        Debug.Log($"[DecorationScrollInstaller] Baked scale into sizeDelta on '{rt.name}'. New size: {rt.sizeDelta}");
        return true;
    }

    // anchorMin=(0,1)/anchorMax=(1,1): full width, top-pinned — required for
    // ContentSizeFitter to calculate height correctly in a vertical ScrollRect.
    private static void ApplyContentRT(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    // The viewport is both the scroll window and the RectMask2D clip rect. It matches
    // 'Item back ground' except at the bottom, where it reaches a little further down
    // (still inside the panel art) so the next row peeks in and the list reads as scrollable.
    private static void ApplyViewportRT(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, -ViewportBottomExtension);
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static bool FixViewportRT(RectTransform rt)
    {
        if (rt == null) return false;
        bool ok = rt.anchorMin == Vector2.zero
               && rt.anchorMax == Vector2.one
               && rt.offsetMin == new Vector2(0f, -ViewportBottomExtension)
               && rt.offsetMax == Vector2.zero
               && rt.localScale == Vector3.one;
        if (ok) return false;
        ApplyViewportRT(rt);
        Debug.Log("[DecorationScrollInstaller] Fixed ScrollViewport RectTransform.");
        return true;
    }

    // Cell width is derived from the container so the columns always fill it exactly.
    // Formula: (containerWidth - leftPad - rightPad - spacing*(cols-1)) / cols
    private static Vector2 CalculateCellSize(RectTransform itemBg)
    {
        float containerW = itemBg != null ? itemBg.rect.width : 0f;
        if (containerW <= 0f && itemBg != null) containerW = itemBg.sizeDelta.x;
        if (containerW <= 0f) return new Vector2(CardDesignWidth, CardDesignHeight);

        float cellW = Mathf.Floor((containerW - PadH * 2 - Gap * (Columns - 1)) / Columns);
        float cellH = Mathf.Round(cellW * (CardDesignHeight / CardDesignWidth));
        return new Vector2(cellW, cellH);
    }

    private static void ApplyGridLayout(GridLayoutGroup grid, RectTransform itemBg)
    {
        grid.padding = new RectOffset(PadH, PadH, PadV, PadV);
        grid.cellSize = CalculateCellSize(itemBg);
        grid.spacing = new Vector2(Gap, Gap);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
    }

    private static bool FixGridLayout(RectTransform content, RectTransform itemBg)
    {
        if (content == null) return false;
        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null) return false;

        Vector2 cellSize = CalculateCellSize(itemBg);
        bool ok = grid.cellSize == cellSize
               && grid.spacing == new Vector2(Gap, Gap)
               && grid.padding.left == PadH && grid.padding.right == PadH
               && grid.padding.top == PadV && grid.padding.bottom == PadV
               && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
               && grid.constraintCount == Columns;
        if (ok) return false;

        ApplyGridLayout(grid, itemBg);
        Debug.Log($"[DecorationScrollInstaller] Fixed grid layout. Cell size: {cellSize}");
        return true;
    }

    private static bool FixContentRT(RectTransform rt)
    {
        bool ok = rt.anchorMin == new Vector2(0f, 1f)
               && rt.anchorMax == new Vector2(1f, 1f)
               && rt.pivot == new Vector2(0.5f, 1f)
               && rt.localScale == Vector3.one;
        if (ok) return false;
        ApplyContentRT(rt);
        Debug.Log("[DecorationScrollInstaller] Fixed Content RectTransform.");
        return true;
    }

    // A reimport of the slot prefab while the editor holds OptionsCanvas loaded can drop
    // this reference, and DecorationPage then instantiates null and shows no cards at all.
    private static bool EnsureDecorationPageSlotPrefab(GameObject prefabRoot)
    {
        if (prefabRoot == null) return false;

        DecorationPage page = prefabRoot.GetComponentInChildren<DecorationPage>(true);
        if (page == null) return false;

        SerializedObject serializedPage = new SerializedObject(page);
        SerializedProperty slotPrefab = serializedPage.FindProperty("slotPrefab");
        if (slotPrefab == null || slotPrefab.objectReferenceValue != null) return false;

        GameObject slotAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        DecorationItemSlot slot = slotAsset != null ? slotAsset.GetComponent<DecorationItemSlot>() : null;
        if (slot == null) return false;

        slotPrefab.objectReferenceValue = slot;
        serializedPage.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[DecorationScrollInstaller] Restored DecorationPage.slotPrefab.");
        return true;
    }

    private static bool EnsureDecorationPageSlotContainer(GameObject prefabRoot, RectTransform content)
    {
        if (prefabRoot == null || content == null) return false;

        DecorationPage page = prefabRoot.GetComponentInChildren<DecorationPage>(true);
        if (page == null) return false;

        SerializedObject serializedPage = new SerializedObject(page);
        SerializedProperty slotContainer = serializedPage.FindProperty("slotContainer");
        if (slotContainer == null || slotContainer.objectReferenceValue == content) return false;

        slotContainer.objectReferenceValue = content;
        serializedPage.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[DecorationScrollInstaller] Pointed DecorationPage.slotContainer to ScrollRect Content.");
        return true;
    }

    private static void CollectUnknownItems(Transform root, System.Collections.Generic.List<Transform> result)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith("Unknown item"))
                result.Add(child);
            else
                CollectUnknownItems(child, result);
        }
    }
}
