using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OptionDetailPanelFigmaSkinAutomation
{
    private const string PrefabPath = "Assets/Prefabs/UI/OptionsCanvas.prefab";
    private const string AssetRoot = "Assets/Figma/Design2/OptionMenu/";
    private const string RequestPath = "Temp/ApplyOptionDetailPanelFigmaSkin.request";
    private const float KeyboardLabelLeftX = -510f;
    private const string OptionTextFontPath = "Assets/Figma/Fonts/ZenOldMincho/TMP/ZenOldMincho-Bold SDF.asset";
    private const string SoundBarFillPath = "Assets/Art/Texture/UI/Bar 1.png";

    [InitializeOnLoadMethod]
    private static void RunIfRequestedOnLoad()
    {
        EditorApplication.delayCall += RunIfRequested;
        EditorApplication.update += RunIfRequested;
    }

    [MenuItem("Tools/UI/Apply Option Detail Panel Figma Skin")]
    public static void Apply()
    {
        ConfigureImportedSprites();

        ApplyToPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[OptionDetailPanelFigmaSkin] Applied Design2 Option Menu skin to OptionsCanvas prefab.");
    }

    private static void ApplyToPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplyToOptionsCanvas(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyToOptionsCanvas(Transform optionsCanvas)
    {
        Transform detail = optionsCanvas.Find("MenuRoot/OptionPanel/OptionDetailPanel");
        if (detail == null)
        {
            Debug.LogWarning("[OptionDetailPanelFigmaSkin] OptionDetailPanel was not found.");
            return;
        }

        SetScreenRect(detail, 0f, 0f, 1920f, 1080f);
        DisableGraphic(detail);
        RemoveLegacyDirectChildren(detail);

        ConfigureLeftMenu(detail);
        ConfigureContentContainers(detail);
        ConfigureSoundPanel(detail);
        ConfigureKeyboardPanel(detail);
        ConfigureResetVisual(detail);
        ConfigureBackButton(detail);
    }

    private static void RemoveLegacyDirectChildren(Transform detail)
    {
        RemoveDirectChild(detail, "bar");
        RemoveDirectChild(detail, "bar (2)");
    }

    private static void RemoveDirectChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void RunIfRequested()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        Debug.Log("[OptionDetailPanelFigmaSkin] Request detected. Applying Design2 Option Menu skin.");
        try
        {
            Apply();
            File.Delete(RequestPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void ConfigureImportedSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Figma/Design2/OptionMenu" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static void ConfigureLeftMenu(Transform detail)
    {
        SetImage(detail, "FrameLeft", "Left_Frame", 95f, 156f, 466f, 739f, false);

        SetImage(detail, "SoundTabButton", "Left_Sound_Selected", 120f, 300f, 417f, 417f, false);
        SetImage(detail, "SoundTabButton (1)", "Left_Sound_Normal", 228f, 409f, 200f, 200f, false);
        SetImage(detail, "KeyboardTabButton", "Left_Keyboard_Selected", 121f, 128f, 416f, 417f, false);
        SetImage(detail, "KeyboardTabButton (1)", "Left_Keyboard_Normal", 213f, 222f, 230f, 230f, false);
        ConfigureHitArea(detail.Find("SoundTabButton"), 120f, 300f, 417f, 417f, 193f, 459f, 330f, 100f);
        ConfigureHitArea(detail.Find("SoundTabButton (1)"), 228f, 409f, 200f, 200f, 193f, 459f, 330f, 100f);
        ConfigureHitArea(detail.Find("KeyboardTabButton"), 121f, 128f, 416f, 417f, 193f, 287f, 330f, 100f);
        ConfigureHitArea(detail.Find("KeyboardTabButton (1)"), 213f, 222f, 230f, 230f, 193f, 287f, 330f, 100f);

        SetActiveIfChanged(detail.Find("SoundTabButton"), true);
        SetActiveIfChanged(detail.Find("SoundTabButton (1)"), false);
        SetActiveIfChanged(detail.Find("KeyboardTabButton"), false);
        SetActiveIfChanged(detail.Find("KeyboardTabButton (1)"), true);

        Transform tutorialNormal = EnsureChild(detail, "TutorialTabNormal");
        ConfigureImage(tutorialNormal, Sprite("Left_Tutorial_Normal"), false);
        SetScreenRect(tutorialNormal, 188f, 540f, 280f, 280f);

        Transform tutorialSelected = EnsureChild(detail, "TutorialTabSelected");
        ConfigureImage(tutorialSelected, Sprite("Left_Tutorial_Selected"), false);
        SetScreenRect(tutorialSelected, 121f, 471f, 417f, 417f);
        tutorialSelected.gameObject.SetActive(false);
    }

    private static void ConfigureContentContainers(Transform detail)
    {
        Transform contentFrame = EnsureChild(detail, "ContentFrame");
        SetScreenRect(contentFrame, 0f, 0f, 1920f, 1080f);
        DisableGraphic(contentFrame);

        Transform soundPanel = EnsureChild(contentFrame, "SoundContentPanel");
        SetScreenRect(soundPanel, 0f, 0f, 1920f, 1080f);
        DisableGraphic(soundPanel);
        soundPanel.gameObject.SetActive(true);

        Transform keyboardPanel = EnsureChild(contentFrame, "KeyboardContentPanel");
        SetScreenRect(keyboardPanel, 0f, 0f, 1920f, 1080f);
        DisableGraphic(keyboardPanel);
        keyboardPanel.gameObject.SetActive(false);
    }

    private static void ConfigureSoundPanel(Transform detail)
    {
        Transform panel = detail.Find("ContentFrame/SoundContentPanel");
        if (panel == null)
        {
            return;
        }

        SetImage(panel, "UP bar 1", "UpBar", 657f, 193f, 1214f, 5f, false);
        SetImage(panel, "down bar 1", "DownBar", 657f, 838f, 1214f, 5f, false);

        ConfigureSoundRow(panel, "SystemBar", "back (2)", "text (2)", "Sound_Master", 661f, -336f, 1193f, 234f, 1204f, 257f, 1670f, 239f);
        ConfigureSoundRow(panel, "BgmBar", "back", "text", "Sound_BGM", 661f, -228f, 1193f, 342f, 1204f, 365f, 1670f, 347f);
        ConfigureSoundRow(panel, "SeBar", "back (1)", "text (1)", "Sound_SE", 661f, -120f, 1193f, 450f, 1204f, 473f, 1670f, 455f);
        ConfigureVolumeText(panel, "SystemVolumeText", 1732f, 234f);
        ConfigureVolumeText(panel, "BgmVolumeText", 1732f, 342f);
        ConfigureVolumeText(panel, "SeVolumeText", 1732f, 450f);
    }

    private static void ConfigureSoundRow(
        Transform panel,
        string barName,
        string rowName,
        string labelName,
        string prefix,
        float rowX,
        float rowY,
        float valueX,
        float valueY,
        float barX,
        float barY,
        float handleX,
        float handleY)
    {
        SetImage(panel, rowName, prefix + "_RowBackground", rowX, rowY, 1200f, 1200f, false);
        SetImage(panel, rowName + " ValueBackground", prefix + "_ValueBackground", valueX, valueY, 524f, 60f, false);
        SetImage(panel, labelName, prefix + "_Label", LabelX(prefix), LabelY(prefix), LabelWidth(prefix), 37f, false);
        RemoveDirectChild(panel, barName + "Back");

        RectTransform bar = EnsureRect(panel, barName);
        SetScreenRect(bar, barX, barY, 494f, 15f);
        Image barImage = ConfigureImage(bar.transform, Sprite(prefix + "_Bar"), true);
        barImage.color = new Color(1f, 1f, 1f, 0.001f);
        barImage.type = Image.Type.Simple;

        ConfigureSoundBarTrack(bar.transform, prefix);
        ConfigureSoundBarFill(bar.transform);

        Transform handle = EnsureChild(bar.transform, "Handle");
        SetFigmaRect(handle, barX, barY, 494f, 15f, handleX, handleY, 50f, 50f);
        ConfigureImage(handle, Sprite(prefix + "_Handle"), false);
        OrderSoundBarChildren(bar.transform);
        bar.transform.SetAsLastSibling();
    }

    private static void ConfigureSoundBarTrack(Transform bar, string prefix)
    {
        Transform track = EnsureChild(bar, "Track");
        SetLocalRect(track, 0f, 0f, 494f, 15f);
        Image trackImage = ConfigureImage(track, Sprite(prefix + "_BarBack"), false);
        trackImage.type = Image.Type.Simple;
    }

    private static void ConfigureSoundBarFill(Transform bar)
    {
        Transform fill = EnsureChild(bar, "Fill");
        SetLeftAnchoredLocalRect(fill, -247f, 0f, 494f, 15f);

        Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoundBarFillPath);
        Image fillImage = ConfigureImage(fill, fillSprite, false);
        fillImage.type = Image.Type.Simple;
        fillImage.fillAmount = 1f;
    }

    private static void OrderSoundBarChildren(Transform bar)
    {
        Transform track = bar.Find("Track");
        Transform fill = bar.Find("Fill");
        Transform handle = bar.Find("Handle");

        if (track != null)
        {
            track.SetSiblingIndex(0);
        }

        if (fill != null)
        {
            fill.SetSiblingIndex(Mathf.Min(1, bar.childCount - 1));
        }

        if (handle != null)
        {
            handle.SetAsLastSibling();
        }
    }

    private static void ConfigureVolumeText(Transform panel, string name, float x, float y)
    {
        Transform target = EnsureChild(panel, name);
        SetScreenRect(target, x, y, 110f, 60f);

        TextMeshProUGUI tmp = target.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = target.gameObject.AddComponent<TextMeshProUGUI>();
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OptionTextFontPath);
        if (font != null)
        {
            tmp.font = font;
        }

        tmp.text = "100";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 32f;
        tmp.color = new Color(0.95f, 0.94f, 0.77f, 1f);
        tmp.raycastTarget = false;
    }

    private static float LabelX(string prefix)
    {
        if (prefix == "Sound_Master") return 705f;
        if (prefix == "Sound_BGM") return 703f;
        return 704f;
    }

    private static float LabelY(string prefix)
    {
        if (prefix == "Sound_Master") return 245f;
        if (prefix == "Sound_BGM") return 353f;
        return 461f;
    }

    private static float LabelWidth(string prefix)
    {
        if (prefix == "Sound_Master") return 231f;
        if (prefix == "Sound_BGM") return 170f;
        return 127f;
    }

    private static void ConfigureKeyboardPanel(Transform detail)
    {
        Transform panel = detail.Find("ContentFrame/KeyboardContentPanel");
        if (panel == null)
        {
            return;
        }

        SetImage(panel, "UP bar 1", "UpBar", 657f, 193f, 1214f, 5f, false);
        SetImage(panel, "down bar 1", "DownBar", 657f, 838f, 1214f, 5f, false);

        RectTransform viewport = EnsureRect(panel, "ScrollViewport");
        SetScreenRect(viewport, 657f, 193f, 1214f, 650f);
        Image viewportImage = EnsureImage(viewport.transform);
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;
        Mask mask = viewport.GetComponent<Mask>();
        if (mask != null)
        {
            Object.DestroyImmediate(mask);
        }
        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        RectTransform content = EnsureRect(viewport.transform, "Content");
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1214f, 1040f);
        content.localScale = Vector3.one;
        DisableGraphic(content.transform);

        ConfigureKeyboardRow(content.transform, "RightMoveRow", "Label_RightMove", "Keyboard_Right", 250f, 120f, 58f, 51f, 51f);
        ConfigureKeyboardRow(content.transform, "LeftMoveRow", "Label_LeftMove", "Keyboard_Left", 142f, 120f, 58f, 51f, 51f);
        ConfigureKeyboardRow(content.transform, "JumpRow", "Label_Jump", "Keyboard_Jump", 35f, 164f, 59f, 95f, 46f);
        ConfigureKeyboardRow(content.transform, "AttackRow", "Label_Attack", "Keyboard_Attack", -73f, 164f, 59f, 55f, 55f, "Mouse");
        ConfigureKeyboardRow(content.transform, "GlideRow", "Label_Umbrella", "Keyboard_Umbrella", -183f, 246f, 59f, 55f, 55f, "Mouse");
        ConfigureKeyboardRow(content.transform, "RecoilRow", "Label_Recoil", "Keyboard_Recoil", -291f, 298f, 60f, 51f, 51f);
        ConfigureKeyboardRow(content.transform, "ParryRow", "Label_Parry", "Keyboard_Parry", -399f, 153f, 60f, 55f, 55f, "Mouse");
        ConfigureKeyboardRow(content.transform, "DodgeRow", "Label_Dodge", "Keyboard_Dodge", -507f, 82f, 59f, 82f, 46f);

        RectTransform scrollbarTrack = EnsureRect(panel, "ScrollbarTrack");
        SetScreenRect(scrollbarTrack, 1854f, 356f, 18f, 318f);
        Image trackImage = EnsureImage(scrollbarTrack.transform);
        trackImage.color = new Color(1f, 1f, 1f, 0.001f);
        trackImage.raycastTarget = true;

        RectTransform scrollbarHandle = EnsureRect(scrollbarTrack.transform, "Handle");
        scrollbarHandle.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarHandle.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarHandle.pivot = new Vector2(0.5f, 0.5f);
        scrollbarHandle.sizeDelta = new Vector2(18f, 150f);
        Image handleImage = EnsureImage(scrollbarHandle.transform);
        handleImage.color = new Color(1f, 1f, 1f, 0.001f);

        RectTransform statusText = EnsureRect(panel, "KeyboardStatusText");
        SetScreenRect(statusText, 690f, 850f, 760f, 34f);
    }

    private static void ConfigureKeyboardRow(
        Transform content,
        string rowName,
        string labelSprite,
        string prefix,
        float rowY,
        float labelWidth,
        float labelHeight,
        float keyCapWidth,
        float keyCapHeight,
        string keyCapSuffix = "KeyCap")
    {
        Transform row = EnsureChild(content, rowName);
        RemoveChildrenExcept(row, "Image", "text", "ValueButton");
        RectTransform rowRect = EnsureRect(row);
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, rowY);
        rowRect.sizeDelta = new Vector2(1200f, 1200f);
        rowRect.localScale = Vector3.one;
        DisableGraphic(row);

        SetLocalImage(row, "Image", prefix + "_RowBackground", 0f, 0f, 1200f, 1200f, false);
        row.Find("Image")?.SetAsFirstSibling();
        SetLocalImage(row, "text", labelSprite, KeyboardLabelLeftX + (labelWidth * 0.5f), 0f, labelWidth, labelHeight, false);

        Transform valueButton = EnsureChild(row, "ValueButton");
        RemoveChildrenExcept(valueButton, "FigmaKeyCap", "Label");
        SetLocalRect(valueButton, 445f, 0f, 219f, 60f);
        valueButton.SetAsLastSibling();
        Image valueImage = ConfigureImage(valueButton, Sprite(prefix + "_KeyBack"), true);
        Button button = valueButton.GetComponent<Button>();
        if (button == null)
        {
            button = valueButton.gameObject.AddComponent<Button>();
        }
        button.transition = Selectable.Transition.None;
        button.targetGraphic = valueImage;

        Transform cap = EnsureChild(valueButton, "FigmaKeyCap");
        SetLocalRect(cap, 0f, 0f, keyCapWidth, keyCapHeight);
        ConfigureImage(cap, Sprite(prefix + "_" + keyCapSuffix), false);

        Transform label = EnsureChild(valueButton, "Label");
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = label.gameObject.AddComponent<TextMeshProUGUI>();
        }
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 34f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        SetLocalRect(label, 0f, 0f, 150f, 52f);
        label.SetAsLastSibling();
    }

    private static void RemoveChildrenExcept(Transform root, params string[] allowedNames)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            bool keep = false;
            for (int j = 0; j < allowedNames.Length; j++)
            {
                if (child.name == allowedNames[j])
                {
                    keep = true;
                    break;
                }
            }

            if (!keep)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void ConfigureResetVisual(Transform detail)
    {
        Transform resetRoot = EnsureChild(detail, "ResetButtonVisual");
        SetScreenRect(resetRoot, 1135f, 773f, 386f, 386f);
        DisableGraphic(resetRoot);

        SetImage(resetRoot, "Not Select", "Reset_Normal", 1135f, 773f, 386f, 386f, false);
        SetImage(resetRoot, "Select", "Reset_Pressed", 1135f, 773f, 386f, 386f, false);
        Transform select = resetRoot.Find("Select");
        if (select != null)
        {
            select.gameObject.SetActive(false);
        }
    }

    private static void ConfigureBackButton(Transform detail)
    {
        Transform backButton = detail.Find("BackButton");
        if (backButton == null)
        {
            return;
        }

        SetScreenRect(backButton, 1518f, 773f, 386f, 386f);
        Image image = backButton.GetComponent<Image>();
        if (image != null)
        {
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        Transform hitArea = EnsureChild(backButton, "HitArea");
        SetLocalRect(hitArea, 0f, 0f, 360f, 122f);
        Image hitImage = EnsureImage(hitArea);
        hitImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitImage.raycastTarget = true;

        Button button = backButton.GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = hitImage;
        }
    }

    private static void SetImage(Transform root, string name, string spriteName, float x, float y, float width, float height, bool raycast)
    {
        Transform target = EnsureChild(root, name);
        ConfigureImage(target, Sprite(spriteName), raycast);
        SetScreenRect(target, x, y, width, height);

        if (raycast)
        {
            Button button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
            }
            button.transition = Selectable.Transition.None;
            button.targetGraphic = target.GetComponent<Graphic>();
        }
    }

    private static void SetImageInParent(
        Transform root,
        string name,
        string spriteName,
        float parentX,
        float parentY,
        float parentWidth,
        float parentHeight,
        float x,
        float y,
        float width,
        float height,
        bool raycast)
    {
        Transform target = EnsureChild(root, name);
        ConfigureImage(target, Sprite(spriteName), raycast);
        SetFigmaRect(target, parentX, parentY, parentWidth, parentHeight, x, y, width, height);
    }

    private static void SetLocalImage(Transform root, string name, string spriteName, float x, float y, float width, float height, bool raycast)
    {
        Transform target = EnsureChild(root, name);
        ConfigureImage(target, Sprite(spriteName), raycast);
        SetLocalRect(target, x, y, width, height);
    }

    private static void ConfigureHitArea(
        Transform buttonRoot,
        float parentX,
        float parentY,
        float parentWidth,
        float parentHeight,
        float x,
        float y,
        float width,
        float height)
    {
        if (buttonRoot == null)
        {
            return;
        }

        Transform hitArea = EnsureChild(buttonRoot, "HitArea");
        Image hitImage = EnsureImage(hitArea);
        hitImage.sprite = null;
        hitImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitImage.raycastTarget = true;
        SetFigmaRect(hitArea, parentX, parentY, parentWidth, parentHeight, x, y, width, height);

        Graphic[] graphics = buttonRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = graphics[i] == hitImage;
            }
        }

        Button button = buttonRoot.GetComponent<Button>();
        if (button == null)
        {
            button = buttonRoot.gameObject.AddComponent<Button>();
        }
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hitImage;
    }

    private static void SetActiveIfChanged(Transform target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active)
        {
            target.gameObject.SetActive(active);
        }
    }

    private static Sprite Sprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetRoot + name + ".png");
        if (sprite == null)
        {
            Debug.LogWarning($"[OptionDetailPanelFigmaSkin] Missing sprite: {AssetRoot}{name}.png");
        }
        return sprite;
    }

    private static Image ConfigureImage(Transform target, Sprite sprite, bool raycast)
    {
        Image image = EnsureImage(target);
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = raycast;
        return image;
    }

    private static void DisableGraphic(Transform target)
    {
        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic == null)
        {
            return;
        }

        graphic.color = new Color(1f, 1f, 1f, 0f);
        graphic.raycastTarget = false;
    }

    private static Image EnsureImage(Transform target)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            image = target.gameObject.AddComponent<Image>();
        }
        return image;
    }

    private static RectTransform EnsureRect(Transform parent, string childName)
    {
        return EnsureRect(EnsureChild(parent, childName));
    }

    private static RectTransform EnsureRect(Transform target)
    {
        return target as RectTransform;
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName, typeof(RectTransform));
        childObject.layer = parent.gameObject.layer;
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static void SetScreenRect(Transform target, float x, float y, float width, float height)
    {
        RectTransform rectTransform = EnsureRect(target);
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2((x + width * 0.5f) - 960f, 540f - (y + height * 0.5f));
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.localScale = Vector3.one;
    }

    private static void SetLocalRect(Transform target, float x, float y, float width, float height)
    {
        RectTransform rectTransform = EnsureRect(target);
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(x, y);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.localScale = Vector3.one;
    }

    private static void SetLeftAnchoredLocalRect(Transform target, float x, float y, float width, float height)
    {
        RectTransform rectTransform = EnsureRect(target);
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(x, y);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.localScale = Vector3.one;
    }

    private static void SetFigmaRect(
        Transform target,
        float parentX,
        float parentY,
        float parentWidth,
        float parentHeight,
        float x,
        float y,
        float width,
        float height)
    {
        RectTransform rectTransform = EnsureRect(target);
        if (rectTransform == null)
        {
            return;
        }

        float parentCenterX = parentX + parentWidth * 0.5f;
        float parentCenterY = parentY + parentHeight * 0.5f;
        float centerX = x + width * 0.5f;
        float centerY = y + height * 0.5f;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(centerX - parentCenterX, parentCenterY - centerY);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.localScale = Vector3.one;
    }
}
