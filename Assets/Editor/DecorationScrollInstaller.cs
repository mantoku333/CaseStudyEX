using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DecorationScrollInstaller
{
    private const string PrefabPath = "Assets/Prefabs/UI/OptionsCanvas.prefab";

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
                changed |= FixContentRT(existing.content);
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
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewportRT.localScale = Vector3.one;
        viewportGO.AddComponent<RectMask2D>();

        // Content — top-anchored, grows downward, 3-column grid
        GameObject contentGO = new GameObject("Content");
        contentGO.layer = LayerMask.NameToLayer("UI");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        ApplyContentRT(contentRT);

        // Cell width is derived from the container so 3 columns always fit exactly.
        // Formula: (containerWidth - leftPad - rightPad - spacing*(cols-1)) / cols
        const int cols = 3;
        const int padH = 10, padV = 10, gap = 10;
        float containerW = ((RectTransform)itemBg).sizeDelta.x;
        float cellW = Mathf.Floor((containerW - padH * 2 - gap * (cols - 1)) / cols);
        float cellH = Mathf.Round(314f * (containerW / 763f)); // keep height proportional

        GridLayoutGroup grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(padH, padH, padV, padV);
        grid.cellSize = new Vector2(cellW, cellH);
        grid.spacing = new Vector2(gap, gap);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

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
