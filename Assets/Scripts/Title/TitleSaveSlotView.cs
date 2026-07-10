using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleSaveSlotView : MonoBehaviour
{
    [SerializeField] private int slotIndex = SaveManager.DefaultSlotIndex;
    [SerializeField] private Button button;
    [SerializeField] private Image stageThumbnailImage;
    [SerializeField] private RectTransform stageThumbnailRoot;
    [SerializeField] private Vector2 stageThumbnailSize = new Vector2(160f, 110f);
    [SerializeField] private TMP_Text savedAtText;
    [SerializeField] private TMP_Text stageNameText;
    [SerializeField] private string emptySavedAtText = "--/-- --:--";
    [SerializeField] private string emptyStageNameText = "セーブデータなし";
    [SerializeField] private string corruptedStageNameText = "読み込み不可";

    private TitleSceneController titleController;

    public int SlotIndex => slotIndex;
    public Button Button => button;
    public Image StageThumbnailImage => stageThumbnailImage;
    public TMP_Text SavedAtLabel => savedAtText;
    public TMP_Text StageNameLabel => stageNameText;

    public void ApplyDesign2Layout(Vector2 thumbnailSize)
    {
        stageThumbnailSize = thumbnailSize;
        ResolveReferences();
        ApplyThumbnailSize();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyThumbnailSize();
        BindButton();
    }

    private void OnEnable()
    {
        if (titleController == null)
        {
            titleController = GetComponentInParent<TitleSceneController>();
        }

        Refresh(titleController);
    }

    public void Refresh(TitleSceneController controller)
    {
        titleController = controller != null ? controller : titleController;
        ResolveReferences();
        ApplyThumbnailSize();
        BindButton();

        SaveSlotMeta slotMeta = SaveManager.GetSlotMeta(slotIndex);
        bool canLoad = slotMeta.HasSave && !slotMeta.IsCorrupted;

        if (button != null)
        {
            button.interactable = canLoad;
        }

        if (!slotMeta.HasSave)
        {
            SetText(savedAtText, emptySavedAtText);
            SetText(stageNameText, emptyStageNameText);
            SetThumbnail(titleController != null ? titleController.GetEmptySlotThumbnail() : null);
            return;
        }

        if (slotMeta.IsCorrupted)
        {
            SetText(savedAtText, emptySavedAtText);
            SetText(stageNameText, corruptedStageNameText);
            SetThumbnail(titleController != null ? titleController.GetEmptySlotThumbnail() : null);
            return;
        }

        SetText(savedAtText, FormatSavedAt(slotMeta.SavedAtUtc));
        SetText(stageNameText, titleController != null ? titleController.GetStageDisplayName(slotMeta.SceneName, slotMeta.LocationId) : slotMeta.SceneName);
        SetThumbnail(titleController != null ? titleController.GetSaveSlotThumbnail(slotIndex, slotMeta.SceneName, slotMeta.LocationId) : null);
    }

    private void ResolveReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (stageThumbnailImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject != gameObject)
                {
                    stageThumbnailImage = images[i];
                    break;
                }
            }
        }

        if (stageThumbnailRoot == null && stageThumbnailImage != null)
        {
            stageThumbnailRoot = stageThumbnailImage.rectTransform;
        }
    }

    private void ApplyThumbnailSize()
    {
        if (stageThumbnailRoot == null)
        {
            return;
        }

        stageThumbnailRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, stageThumbnailSize.x);
        stageThumbnailRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, stageThumbnailSize.y);

        LayoutElement layoutElement = stageThumbnailRoot.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = stageThumbnailRoot.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minWidth = stageThumbnailSize.x;
        layoutElement.preferredWidth = stageThumbnailSize.x;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = stageThumbnailSize.y;
        layoutElement.preferredHeight = stageThumbnailSize.y;
        layoutElement.flexibleHeight = 0f;
    }

    private void BindButton()
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
        OptionsCanvasButtonUtility.ConfigurePressOnlyFeedback(button);
        UIButtonSfxPlayer.Register(button);
        UIButtonSfxPlayer.RegisterHover(button);
    }

    private void OnClick()
    {
        if (titleController == null)
        {
            titleController = GetComponentInParent<TitleSceneController>();
        }

        if (titleController != null)
        {
            titleController.OnClickSaveSlot(slotIndex);
        }
    }

    private void SetThumbnail(Sprite sprite)
    {
        if (stageThumbnailImage == null)
        {
            return;
        }

        stageThumbnailImage.sprite = sprite;
        stageThumbnailImage.enabled = sprite != null;
        stageThumbnailImage.preserveAspect = true;
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }

    private static string FormatSavedAt(string savedAtUtc)
    {
        if (DateTime.TryParse(
                savedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime savedAt))
        {
            return savedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
        }

        return "--/-- --:--";
    }
}
