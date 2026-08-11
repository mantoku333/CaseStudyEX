using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metroidvania.Managers;
using Metroidvania.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;

[InitializeOnLoad]
public static class StoryDialogueHierarchyBuilder
{
    private const string ScenePath = "Assets/Scenes/Mantoku_Dialog.unity";
    private const string DialogueSystemPrefabPath = "Assets/Prefabs/UI/StoryDialogueSystem.prefab";
    private const string EventOverlayPrefabPath = "Assets/Prefabs/UI/StoryEventOverlay.prefab";
    private const string LegacyEventOverlayPrefabPath = "Assets/Prefabs/UI/StoryEventOverlayCanvas.prefab";
    private const string YarnProjectPath = "Assets/TestProject.yarnproject";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string FontPath = "Assets/Figma/Fonts/ZenOldMincho/TMP/ZenOldMincho-Regular SDF.asset";
    private const string IrisPortraitPath = "Assets/Art/Sprites/PlayerHUD/Iris_tatie01 1.png";
    private const string AutomationRequestPath = "Temp/StoryDialogueHierarchyBuilder.request";
    private const string AutomationResultPath = "Temp/StoryDialogueHierarchyBuilder.result";
    private const string CanvasCollectionName = "Canvases";
    private const string EventCanvasName = "EventCanvas";
    private const string LegacyPresentationName = "StoryPresentation";

    static StoryDialogueHierarchyBuilder()
    {
        // A request file lets an already-open editor run the same guarded migration.
        if (File.Exists(AutomationRequestPath))
        {
            EditorApplication.delayCall += RebuildIfRequested;
        }
    }

    private static void RebuildIfRequested()
    {
        if (!File.Exists(AutomationRequestPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RebuildIfRequested;
            return;
        }

        File.Delete(AutomationRequestPath);
        try
        {
            string backupPath = RebuildHierarchy();
            File.WriteAllText(AutomationResultPath, $"SUCCESS\n{backupPath}");
        }
        catch (Exception exception)
        {
            File.WriteAllText(AutomationResultPath, $"ERROR\n{exception}");
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/CaseStudy/Story/Rebuild Mantoku Dialogue Hierarchy")]
    public static void RebuildFromMenu()
    {
        try
        {
            string backupPath = RebuildHierarchy();
            EditorUtility.DisplayDialog(
                "Story Dialogue",
                $"Mantoku_Dialog の会話Hierarchyを再構築しました。\n\nBackup: {backupPath}",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Story Dialogue", exception.Message, "OK");
        }
    }

    /// <summary>
    /// Entry point for -executeMethod. The editor exits with a non-zero code
    /// when validation or migration fails.
    /// </summary>
    public static void RebuildFromCommandLine()
    {
        try
        {
            string backupPath = RebuildHierarchy();
            Debug.Log($"[StoryDialogueHierarchyBuilder] Rebuild completed. backup='{backupPath}'");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static string RebuildHierarchy()
    {
        Scene scene = OpenTargetSceneSafely();
        string backupPath = BackupSceneFile();

        GameObject canvasesRoot = FindUniqueOptional(scene, CanvasCollectionName);
        if (canvasesRoot == null)
        {
            throw new InvalidOperationException($"'{CanvasCollectionName}' が見つかりません。");
        }

        GameObject existingPresentation = FindUniqueOptional(scene, LegacyPresentationName);
        GameObject oldDialogueCanvas = FindUniqueOptional(scene, "DialogueCanvas");
        GameObject oldBubbleCanvas = FindUniqueOptional(scene, "BubbleDialogueCanvas");
        GameObject oldDialogueSystem = FindLegacyDialogueSystem(scene);
        GameObject oldEventCanvas = FindUniqueOptional(scene, EventCanvasName);
        GameObject existingOverlay = FindUniqueOptional(scene, "StoryEventOverlay");
        if (existingOverlay == null)
        {
            existingOverlay = FindUniqueOptional(scene, "StoryEventOverlayCanvas");
        }

        // On subsequent rebuilds EventCanvas is only the shared presentation canvas,
        // so preserve the extracted overlay rather than cloning the whole canvas.
        GameObject overlaySource = existingOverlay != null ? existingOverlay : oldEventCanvas;
        if (overlaySource == null)
        {
            throw new InvalidOperationException(
                "EventCanvas または StoryEventOverlayCanvas が見つかりません。非会話UIを安全に退避できないため中断しました。");
        }

        GameObject overlayPrefab = BuildEventOverlayPrefab(overlaySource);
        GameObject dialogueSystemPrefab = BuildDialogueSystemPrefab();

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild Mantoku Dialogue Hierarchy");

        DestroyIfPresent(existingPresentation);
        DestroyIfPresent(oldDialogueCanvas);
        DestroyIfPresent(oldBubbleCanvas);
        DestroyIfPresent(oldDialogueSystem);
        DestroyIfPresent(oldEventCanvas);

        var presentationRoot = new GameObject(
            EventCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(presentationRoot, $"Create {EventCanvasName}");
        SceneManager.MoveGameObjectToScene(presentationRoot, scene);
        presentationRoot.transform.SetParent(canvasesRoot.transform, false);
        Canvas presentationCanvas = presentationRoot.GetComponent<Canvas>();
        CanvasScaler presentationScaler = presentationRoot.GetComponent<CanvasScaler>();
        ConfigureScreenCanvas(presentationRoot, sortingOrder: 110);
        Stretch((RectTransform)presentationRoot.transform);
        ForceVisibleScale(presentationRoot.transform);
        RequireVisibleScale(presentationRoot.transform, $"Scene/{CanvasCollectionName}/{EventCanvasName}");
        presentationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        presentationScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        var dialogueSystemInstance = PrefabUtility.InstantiatePrefab(dialogueSystemPrefab, scene) as GameObject;
        var overlayInstance = PrefabUtility.InstantiatePrefab(overlayPrefab, scene) as GameObject;
        if (dialogueSystemInstance == null || overlayInstance == null)
        {
            throw new InvalidOperationException("新しい会話PrefabをSceneへ配置できませんでした。");
        }

        overlayInstance.name = "StoryEventOverlay";

        Undo.RegisterCreatedObjectUndo(dialogueSystemInstance, "Create StoryDialogueSystem");
        Undo.RegisterCreatedObjectUndo(overlayInstance, "Create StoryEventOverlay");
        overlayInstance.transform.SetParent(presentationRoot.transform, false);
        dialogueSystemInstance.transform.SetParent(presentationRoot.transform, false);

        Stretch((RectTransform)overlayInstance.transform);
        Stretch((RectTransform)dialogueSystemInstance.transform);
        ForceVisibleScale(dialogueSystemInstance.transform);
        ForceVisibleScale(overlayInstance.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(dialogueSystemInstance.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(overlayInstance.transform);
        RequireVisibleScale(dialogueSystemInstance.transform, "Scene/StoryDialogueSystem");
        RequireVisibleScale(overlayInstance.transform, "Scene/StoryEventOverlay");

        DialogueManager manager = dialogueSystemInstance.GetComponent<DialogueManager>();
        WireSceneOwnedStoryControllers(scene, manager);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException($"Sceneを保存できませんでした: {ScenePath}");
        }

        if (!string.Equals(EventOverlayPrefabPath, LegacyEventOverlayPrefabPath, StringComparison.Ordinal) &&
            AssetDatabase.LoadAssetAtPath<GameObject>(LegacyEventOverlayPrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(LegacyEventOverlayPrefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = dialogueSystemInstance;
        Debug.Log(
            $"[StoryDialogueHierarchyBuilder] Rebuilt '{ScenePath}'. " +
            $"Created '{DialogueSystemPrefabPath}' and '{EventOverlayPrefabPath}'.");
        return backupPath;
    }

    private static Scene OpenTargetSceneSafely()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath)
        {
            return activeScene;
        }

        if (activeScene.IsValid() && activeScene.isDirty)
        {
            throw new InvalidOperationException(
                $"現在のScene '{activeScene.path}' に未保存変更があります。保存してから再実行してください。");
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static string BackupSceneFile()
    {
        string backupDirectory = Path.Combine("Temp", "StoryDialogueBackups");
        Directory.CreateDirectory(backupDirectory);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backupPath = Path.Combine(backupDirectory, $"Mantoku_Dialog-{timestamp}.unity");
        File.Copy(ScenePath, backupPath, overwrite: false);
        return backupPath.Replace('\\', '/');
    }

    private static GameObject BuildEventOverlayPrefab(GameObject source)
    {
        GameObject clone = UnityEngine.Object.Instantiate(source);
        clone.name = "StoryEventOverlay";
        clone.transform.SetParent(null, false);

        if (PrefabUtility.IsPartOfPrefabInstance(clone))
        {
            PrefabUtility.UnpackPrefabInstance(
                PrefabUtility.GetOutermostPrefabInstanceRoot(clone),
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }

        RemoveDialoguePresentationObjects(clone);
        RemoveRootCanvasComponents(clone);
        Stretch((RectTransform)clone.transform);
        SetLayerRecursively(clone, LayerMask.NameToLayer("UI"));
        ForceVisibleScale(clone.transform);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, EventOverlayPrefabPath);
        UnityEngine.Object.DestroyImmediate(clone);
        if (prefab == null)
        {
            throw new IOException($"Prefabを保存できませんでした: {EventOverlayPrefabPath}");
        }

        prefab = RepairPrefabVisibleScale(EventOverlayPrefabPath, string.Empty, "StoryEventOverlay");
        RequireVisibleScale(prefab.transform, EventOverlayPrefabPath);

        return prefab;
    }

    private static void RemoveDialoguePresentationObjects(GameObject root)
    {
        var targets = new HashSet<GameObject>();
        foreach (DialogueView view in root.GetComponentsInChildren<DialogueView>(true))
        {
            targets.Add(view.gameObject);
        }

        foreach (BubbleDialogueView view in root.GetComponentsInChildren<BubbleDialogueView>(true))
        {
            targets.Add(view.gameObject);
        }

        foreach (GameObject target in targets)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void RemoveRootCanvasComponents(GameObject root)
    {
        GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        Canvas canvas = root.GetComponent<Canvas>();

        if (raycaster != null)
        {
            UnityEngine.Object.DestroyImmediate(raycaster);
        }

        if (scaler != null)
        {
            UnityEngine.Object.DestroyImmediate(scaler);
        }

        if (canvas != null)
        {
            UnityEngine.Object.DestroyImmediate(canvas);
        }
    }

    private static GameObject BuildDialogueSystemPrefab()
    {
        List<CharacterPortrait> portraitConfigurations = CapturePortraitConfigurations();
        List<DialogueIllustration> illustrationConfigurations = CaptureIllustrationConfigurations();
        Sprite irisPortrait = AssetDatabase.LoadAllAssetsAtPath(IrisPortraitPath).OfType<Sprite>().FirstOrDefault();
        if (portraitConfigurations.Count == 0 && irisPortrait != null)
        {
            portraitConfigurations.Add(new CharacterPortrait
            {
                characterName = "イリス",
                portraitSprite = irisPortrait,
                slot = DialoguePortraitSlot.Right,
                expressionPortraits = Array.Empty<PortraitExpression>()
            });
        }

        var root = new GameObject("StoryDialogueSystem", typeof(RectTransform));
        Stretch((RectTransform)root.transform);
        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        DialogueRunner runner = root.AddComponent<DialogueRunner>();
        DialogueManager manager = root.AddComponent<DialogueManager>();
        DialogueGamePauser pauser = root.AddComponent<DialogueGamePauser>();
        DialogueView view = root.AddComponent<DialogueView>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject presentationRoot = CreateUiObject("PresentationRoot", root.transform);
        Stretch(presentationRoot.GetComponent<RectTransform>());

        Image backgroundDimmer = CreateImage(
            "BackgroundDimmer",
            presentationRoot.transform,
            new Color(0f, 0f, 0f, 0.58f));
        Stretch(backgroundDimmer.rectTransform);
        backgroundDimmer.raycastTarget = false;

        Button advanceButton = CreateButton("AdvanceInputBlocker", presentationRoot.transform, string.Empty, font);
        Stretch(advanceButton.GetComponent<RectTransform>());
        advanceButton.image.color = new Color(0f, 0f, 0f, 0f);
        advanceButton.transition = Selectable.Transition.None;

        Image centerIllustration = CreateImage("CenterIllustration", presentationRoot.transform, Color.white);
        SetRect(centerIllustration.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(1040f, 600f), Vector2.zero);
        centerIllustration.preserveAspect = true;
        centerIllustration.raycastTarget = false;
        centerIllustration.gameObject.SetActive(false);

        GameObject portraitLayer = CreateUiObject("PortraitLayer", presentationRoot.transform);
        Stretch(portraitLayer.GetComponent<RectTransform>());

        Image leftPortrait = CreateImage("LeftPortrait", portraitLayer.transform, Color.white);
        SetRect(leftPortrait.rectTransform, new Vector2(0f, 0f), new Vector2(720f, 900f), new Vector2(360f, 450f));
        leftPortrait.preserveAspect = true;
        leftPortrait.raycastTarget = false;
        leftPortrait.gameObject.SetActive(false);

        Image rightPortrait = CreateImage("RightPortrait", portraitLayer.transform, Color.white);
        SetRect(rightPortrait.rectTransform, new Vector2(1f, 0f), new Vector2(720f, 900f), new Vector2(-360f, 450f));
        rightPortrait.preserveAspect = true;
        rightPortrait.raycastTarget = false;
        rightPortrait.sprite = portraitConfigurations
            .Where(portrait => portrait.slot == DialoguePortraitSlot.Right)
            .Select(portrait => portrait.portraitSprite)
            .FirstOrDefault(sprite => sprite != null) ?? irisPortrait;
        rightPortrait.gameObject.SetActive(false);

        Image dialogueWindow = CreateImage(
            "DialogueWindow",
            presentationRoot.transform,
            new Color(0.045f, 0.055f, 0.09f, 0.96f));
        SetRect(dialogueWindow.rectTransform, new Vector2(0.5f, 0f), new Vector2(1560f, 250f), new Vector2(0f, 150f));
        dialogueWindow.raycastTarget = false;

        TextMeshProUGUI speakerName = CreateText(
            "SpeakerName",
            dialogueWindow.transform,
            font,
            48f,
            TextAlignmentOptions.Left,
            Color.white);
        SetRect(speakerName.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 74f), new Vector2(290f, -38f));

        TextMeshProUGUI dialogueText = CreateText(
            "DialogueText",
            dialogueWindow.transform,
            font,
            34f,
            TextAlignmentOptions.TopLeft,
            Color.white);
        // Keep the original left edge while limiting a normal-size line to
        // approximately 30 full-width Japanese characters (34 px x 30).
        SetRect(dialogueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1020f, 130f), new Vector2(-170f, -30f));
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.overflowMode = TextOverflowModes.Overflow;
        dialogueText.lineSpacing = 8f;

        TextMeshProUGUI nextIndicator = CreateText(
            "NextIndicator",
            dialogueWindow.transform,
            font,
            32f,
            TextAlignmentOptions.Center,
            Color.white);
        nextIndicator.text = "▼";
        SetRect(nextIndicator.rectTransform, new Vector2(1f, 0f), new Vector2(72f, 56f), new Vector2(-54f, 38f));

        GameObject controls = CreateUiObject("Controls", presentationRoot.transform);
        SetRect(controls.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(360f, 80f), new Vector2(-210f, -60f));
        HorizontalLayoutGroup controlsLayout = controls.AddComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 20f;
        controlsLayout.childAlignment = TextAnchor.MiddleCenter;
        controlsLayout.childControlWidth = false;
        controlsLayout.childControlHeight = false;
        controlsLayout.childForceExpandWidth = false;
        controlsLayout.childForceExpandHeight = false;

        Button logButton = CreateButton("LogButton", controls.transform, "ログ", font, new Vector2(160f, 64f));
        Button skipButton = CreateButton("SkipButton", controls.transform, "スキップ", font, new Vector2(160f, 64f));

        GameObject logModal = CreateModalBackdrop("LogModal", presentationRoot.transform);
        Image logBody = CreateImage("LogBody", logModal.transform, new Color(0.055f, 0.065f, 0.1f, 0.98f));
        SetRect(logBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1420f, 820f), Vector2.zero);

        GameObject scrollViewObject = CreateUiObject("ScrollView", logBody.transform);
        SetRect(scrollViewObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(1260f, 660f), new Vector2(0f, 30f));
        ScrollRect scrollRect = scrollViewObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        Image viewport = CreateImage("Viewport", scrollViewObject.transform, new Color(0f, 0f, 0f, 0f));
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<RectMask2D>();
        scrollRect.viewport = viewport.rectTransform;

        TextMeshProUGUI logText = CreateText(
            "LogText",
            viewport.transform,
            font,
            30f,
            TextAlignmentOptions.TopLeft,
            Color.white);
        logText.rectTransform.anchorMin = new Vector2(0f, 1f);
        logText.rectTransform.anchorMax = new Vector2(1f, 1f);
        logText.rectTransform.pivot = new Vector2(0.5f, 1f);
        logText.rectTransform.anchoredPosition = Vector2.zero;
        logText.rectTransform.sizeDelta = new Vector2(-32f, 0f);
        logText.textWrappingMode = TextWrappingModes.Normal;
        ContentSizeFitter logFitter = logText.gameObject.AddComponent<ContentSizeFitter>();
        logFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = logText.rectTransform;

        Button logCloseButton = CreateButton("LogCloseButton", logBody.transform, "閉じる", font, new Vector2(240f, 64f));
        SetRect(logCloseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(240f, 64f), new Vector2(0f, 54f));
        logModal.SetActive(false);

        GameObject skipModal = CreateModalBackdrop("SkipConfirmModal", presentationRoot.transform);
        Image skipBody = CreateImage("SkipConfirmBody", skipModal.transform, new Color(0.055f, 0.065f, 0.1f, 0.99f));
        SetRect(skipBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(760f, 360f), Vector2.zero);

        TextMeshProUGUI skipMessage = CreateText(
            "SkipMessage",
            skipBody.transform,
            font,
            34f,
            TextAlignmentOptions.Center,
            Color.white);
        skipMessage.text = "ストーリーをスキップしますか？";
        SetRect(skipMessage.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(650f, 100f), Vector2.zero);

        Button skipYes = CreateButton("YesButton", skipBody.transform, "はい", font, new Vector2(220f, 70f));
        SetRect(skipYes.GetComponent<RectTransform>(), new Vector2(0.35f, 0.25f), new Vector2(220f, 70f), Vector2.zero);
        Button skipNo = CreateButton("NoButton", skipBody.transform, "いいえ", font, new Vector2(220f, 70f));
        SetRect(skipNo.GetComponent<RectTransform>(), new Vector2(0.65f, 0.25f), new Vector2(220f, 70f), Vector2.zero);
        skipModal.SetActive(false);

        UnityEventTools.AddPersistentListener(advanceButton.onClick, view.OnContinueClicked);
        UnityEventTools.AddPersistentListener(logButton.onClick, view.OnLogClicked);
        UnityEventTools.AddPersistentListener(logCloseButton.onClick, view.OnLogCloseClicked);
        UnityEventTools.AddPersistentListener(skipButton.onClick, view.OnSkipClicked);
        UnityEventTools.AddPersistentListener(skipYes.onClick, view.OnSkipConfirmYesClicked);
        UnityEventTools.AddPersistentListener(skipNo.onClick, view.OnSkipConfirmNoClicked);

        ConfigureDialogueView(
            view,
            presentationRoot,
            dialogueWindow.gameObject,
            speakerName,
            dialogueText,
            centerIllustration,
            nextIndicator.gameObject,
            leftPortrait,
            rightPortrait,
            logButton,
            logModal,
            logText,
            logCloseButton,
            skipButton,
            skipModal,
            skipYes,
            skipNo,
            portraitConfigurations,
            illustrationConfigurations);
        ConfigureDialogueRuntime(runner, manager, pauser, view);

        presentationRoot.SetActive(false);
        ForceVisibleScale(root.transform);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DialogueSystemPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
        {
            throw new IOException($"Prefabを保存できませんでした: {DialogueSystemPrefabPath}");
        }

        prefab = RepairPrefabVisibleScale(DialogueSystemPrefabPath, string.Empty, "StoryDialogueSystem");
        RequireVisibleScale(prefab.transform, DialogueSystemPrefabPath);

        return prefab;
    }

    private static void ConfigureDialogueView(
        DialogueView view,
        GameObject presentationRoot,
        GameObject dialoguePanel,
        TextMeshProUGUI speakerName,
        TextMeshProUGUI dialogueText,
        Image centerIllustration,
        GameObject nextIndicator,
        Image leftPortrait,
        Image rightPortrait,
        Button logButton,
        GameObject logPanel,
        TextMeshProUGUI logText,
        Button logCloseButton,
        Button skipButton,
        GameObject skipPanel,
        Button skipYes,
        Button skipNo,
        IReadOnlyList<CharacterPortrait> portraitConfigurations,
        IReadOnlyList<DialogueIllustration> illustrationConfigurations)
    {
        var serialized = new SerializedObject(view);
        SetReference(serialized, "presentationRoot", presentationRoot);
        SetReference(serialized, "dialoguePanel", dialoguePanel);
        SetReference(serialized, "speakerNameText", speakerName);
        SetReference(serialized, "dialogueText", dialogueText);
        SetReference(serialized, "centerIllustrationImage", centerIllustration);
        SetReference(serialized, "nextIndicator", nextIndicator);
        SetReference(serialized, "leftPortraitImage", leftPortrait);
        SetReference(serialized, "rightPortraitImage", rightPortrait);
        SetReference(serialized, "logButton", logButton);
        SetReference(serialized, "logPanel", logPanel);
        SetReference(serialized, "logText", logText);
        SetReference(serialized, "logCloseButton", logCloseButton);
        SetReference(serialized, "skipButton", skipButton);
        SetReference(serialized, "skipConfirmPanel", skipPanel);
        SetReference(serialized, "skipConfirmYesButton", skipYes);
        SetReference(serialized, "skipConfirmNoButton", skipNo);

        SerializedProperty portraits = serialized.FindProperty("characterPortraits");
        portraits.arraySize = portraitConfigurations.Count;
        for (int portraitIndex = 0; portraitIndex < portraitConfigurations.Count; portraitIndex++)
        {
            CharacterPortrait configuration = portraitConfigurations[portraitIndex];
            SerializedProperty portrait = portraits.GetArrayElementAtIndex(portraitIndex);
            portrait.FindPropertyRelative("characterName").stringValue = configuration.characterName;
            portrait.FindPropertyRelative("portraitSprite").objectReferenceValue = configuration.portraitSprite;
            portrait.FindPropertyRelative("slot").enumValueIndex = (int)configuration.slot;

            SerializedProperty expressionPortraits = portrait.FindPropertyRelative("expressionPortraits");
            PortraitExpression[] expressions = configuration.expressionPortraits ?? Array.Empty<PortraitExpression>();
            expressionPortraits.arraySize = expressions.Length;
            for (int expressionIndex = 0; expressionIndex < expressions.Length; expressionIndex++)
            {
                PortraitExpression expression = expressions[expressionIndex];
                SerializedProperty expressionProperty = expressionPortraits.GetArrayElementAtIndex(expressionIndex);
                expressionProperty.FindPropertyRelative("expressionName").stringValue = expression.expressionName;
                expressionProperty.FindPropertyRelative("portraitSprite").objectReferenceValue = expression.portraitSprite;
            }
        }

        SerializedProperty illustrations = serialized.FindProperty("centerIllustrations");
        illustrations.arraySize = illustrationConfigurations.Count;
        for (int illustrationIndex = 0; illustrationIndex < illustrationConfigurations.Count; illustrationIndex++)
        {
            DialogueIllustration configuration = illustrationConfigurations[illustrationIndex];
            SerializedProperty illustration = illustrations.GetArrayElementAtIndex(illustrationIndex);
            illustration.FindPropertyRelative("illustrationName").stringValue = configuration.illustrationName;
            illustration.FindPropertyRelative("illustrationSprite").objectReferenceValue = configuration.illustrationSprite;
            illustration.FindPropertyRelative("displaySfx").objectReferenceValue = configuration.displaySfx;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<CharacterPortrait> CapturePortraitConfigurations()
    {
        var result = new List<CharacterPortrait>();
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogueSystemPrefabPath);
        DialogueView existingView = existingPrefab != null ? existingPrefab.GetComponent<DialogueView>() : null;
        if (existingView == null)
        {
            return result;
        }

        var serialized = new SerializedObject(existingView);
        serialized.Update();
        SerializedProperty portraits = serialized.FindProperty("characterPortraits");
        if (portraits == null)
        {
            return result;
        }

        for (int portraitIndex = 0; portraitIndex < portraits.arraySize; portraitIndex++)
        {
            SerializedProperty portrait = portraits.GetArrayElementAtIndex(portraitIndex);
            SerializedProperty expressionsProperty = portrait.FindPropertyRelative("expressionPortraits");
            int expressionCount = expressionsProperty != null ? expressionsProperty.arraySize : 0;
            var expressions = new PortraitExpression[expressionCount];
            for (int expressionIndex = 0; expressionIndex < expressionCount; expressionIndex++)
            {
                SerializedProperty expression = expressionsProperty.GetArrayElementAtIndex(expressionIndex);
                expressions[expressionIndex] = new PortraitExpression
                {
                    expressionName = expression.FindPropertyRelative("expressionName").stringValue,
                    portraitSprite = expression.FindPropertyRelative("portraitSprite").objectReferenceValue as Sprite
                };
            }

            result.Add(new CharacterPortrait
            {
                characterName = portrait.FindPropertyRelative("characterName").stringValue,
                portraitSprite = portrait.FindPropertyRelative("portraitSprite").objectReferenceValue as Sprite,
                slot = (DialoguePortraitSlot)portrait.FindPropertyRelative("slot").enumValueIndex,
                expressionPortraits = expressions
            });
        }

        return result;
    }

    private static List<DialogueIllustration> CaptureIllustrationConfigurations()
    {
        var result = new List<DialogueIllustration>();
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogueSystemPrefabPath);
        DialogueView existingView = existingPrefab != null ? existingPrefab.GetComponent<DialogueView>() : null;
        if (existingView == null)
        {
            return result;
        }

        var serialized = new SerializedObject(existingView);
        serialized.Update();
        SerializedProperty illustrations = serialized.FindProperty("centerIllustrations");
        if (illustrations == null)
        {
            return result;
        }

        for (int illustrationIndex = 0; illustrationIndex < illustrations.arraySize; illustrationIndex++)
        {
            SerializedProperty illustration = illustrations.GetArrayElementAtIndex(illustrationIndex);
            result.Add(new DialogueIllustration
            {
                illustrationName = illustration.FindPropertyRelative("illustrationName").stringValue,
                illustrationSprite = illustration.FindPropertyRelative("illustrationSprite").objectReferenceValue as Sprite,
                displaySfx = illustration.FindPropertyRelative("displaySfx").objectReferenceValue as AudioClip
            });
        }

        return result;
    }

    private static void ConfigureDialogueRuntime(
        DialogueRunner runner,
        DialogueManager manager,
        DialogueGamePauser pauser,
        DialogueView view)
    {
        YarnProject yarnProject = AssetDatabase.LoadAssetAtPath<YarnProject>(YarnProjectPath);
        var runnerSerialized = new SerializedObject(runner);
        SetReference(runnerSerialized, "yarnProject", yarnProject);
        SerializedProperty presenters = runnerSerialized.FindProperty("dialoguePresenters");
        presenters.arraySize = 1;
        presenters.GetArrayElementAtIndex(0).objectReferenceValue = view;
        runnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        var managerSerialized = new SerializedObject(manager);
        SetReference(managerSerialized, "dialogueRunner", runner);
        SetReference(managerSerialized, "advView", view);
        SetReference(managerSerialized, "bubbleView", null);
        SetReference(managerSerialized, "nextAction", FindDialogueNextReference());
        managerSerialized.ApplyModifiedPropertiesWithoutUndo();

        var pauserSerialized = new SerializedObject(pauser);
        SetReference(pauserSerialized, "dialogueRunner", runner);
        pauserSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static InputActionReference FindDialogueNextReference()
    {
        return AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
            .OfType<InputActionReference>()
            .FirstOrDefault(reference =>
                reference != null &&
                reference.action != null &&
                string.Equals(reference.action.name, "DialogueNext", StringComparison.OrdinalIgnoreCase));
    }

    private static void ConfigureScreenCanvas(GameObject root, int sortingOrder)
    {
        root.transform.localScale = Vector3.one;
        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
        }

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
    }

    private static GameObject CreateModalBackdrop(string name, Transform parent)
    {
        Image image = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.78f));
        Stretch(image.rectTransform);
        image.raycastTarget = true;
        return image.gameObject;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        TMP_FontAsset font,
        Vector2? size = null)
    {
        Image image = CreateImage(name, parent, new Color(0.16f, 0.18f, 0.28f, 0.98f));
        if (size.HasValue)
        {
            image.rectTransform.sizeDelta = size.Value;
        }

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = CreateText(
                "Label",
                image.transform,
                font,
                28f,
                TextAlignmentOptions.Center,
                Color.white);
            text.text = label;
            Stretch(text.rectTransform);
        }

        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localScale = Vector3.one;
        gameObject.layer = LayerMask.NameToLayer("UI");
        return gameObject;
    }

    private static void ForceVisibleScale(Transform transform)
    {
        var serialized = new SerializedObject(transform);
        SerializedProperty localScale = serialized.FindProperty("m_LocalScale");
        if (localScale != null)
        {
            localScale.vector3Value = Vector3.one;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        transform.localScale = Vector3.one;
        EditorUtility.SetDirty(transform);
    }

    private static GameObject RepairPrefabVisibleScale(
        string prefabPath,
        string relativeTransformPath,
        string rootName)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            contents.name = rootName;
            Transform target = string.IsNullOrEmpty(relativeTransformPath)
                ? contents.transform
                : contents.transform.Find(relativeTransformPath);
            RequireVisibleScaleTarget(target, $"{prefabPath}/{relativeTransformPath}");
            ForceVisibleScale(target);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static void RequireVisibleScaleTarget(Transform transform, string objectPath)
    {
        if (transform == null)
        {
            throw new InvalidOperationException($"生成したUIが見つかりません: {objectPath}");
        }
    }

    private static void RequireVisibleScale(Transform transform, string objectPath)
    {
        RequireVisibleScaleTarget(transform, objectPath);

        if (transform.localScale.sqrMagnitude <= 0.000001f)
        {
            throw new InvalidOperationException($"生成したUIのScaleが0です: {objectPath}");
        }
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
        rectTransform.localScale = Vector3.one;
    }

    private static void SetReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        }

        property.objectReferenceValue = value;
    }

    private static void WireSceneOwnedStoryControllers(Scene scene, DialogueManager manager)
    {
        foreach (StoryEventController controller in UnityEngine.Object.FindObjectsByType<StoryEventController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (controller == null || controller.gameObject.scene != scene || PrefabUtility.IsPartOfPrefabInstance(controller))
            {
                continue;
            }

            var serialized = new SerializedObject(controller);
            SetReference(serialized, "dialogueManager", manager);
            SerializedProperty style = serialized.FindProperty("defaultDialogueStyle");
            if (style != null)
            {
                style.enumValueIndex = (int)DialogueStyle.ADV;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameObject FindLegacyDialogueSystem(Scene scene)
    {
        GameObject byName = FindUniqueOptional(scene, "Dialogue System");
        if (byName != null)
        {
            return byName;
        }

        var matches = new List<GameObject>();
        foreach (DialogueManager manager in UnityEngine.Object.FindObjectsByType<DialogueManager>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (manager != null && manager.gameObject.scene == scene)
            {
                GameObject owner = manager.gameObject;
                for (Transform current = manager.transform; current != null; current = current.parent)
                {
                    if (string.Equals(current.name, CanvasCollectionName, StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (string.Equals(current.name, "Dialogue System", StringComparison.Ordinal) ||
                        string.Equals(current.name, "StoryDialogueSystem", StringComparison.Ordinal) ||
                        string.Equals(current.name, LegacyPresentationName, StringComparison.Ordinal) ||
                        string.Equals(current.name, EventCanvasName, StringComparison.Ordinal))
                    {
                        owner = current.gameObject;
                        break;
                    }
                }

                matches.Add(owner);
            }
        }

        matches = matches.Distinct().ToList();
        if (matches.Count > 1)
        {
            throw new InvalidOperationException("旧DialogueManagerが複数見つかったため、自動削除を中断しました。");
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private static GameObject FindUniqueOptional(Scene scene, string objectName)
    {
        List<GameObject> matches = EnumerateSceneObjects(scene)
            .Where(gameObject => string.Equals(gameObject.name, objectName, StringComparison.Ordinal))
            .ToList();

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"'{objectName}' が{matches.Count}個見つかったため、自動変更を中断しました。");
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private static IEnumerable<GameObject> EnumerateSceneObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                yield return transform.gameObject;
            }
        }
    }

    private static void DestroyIfPresent(GameObject gameObject)
    {
        if (gameObject != null)
        {
            Undo.DestroyObjectImmediate(gameObject);
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (layer < 0)
        {
            return;
        }

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.layer = layer;
        }
    }
}
