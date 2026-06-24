using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleSceneController : MonoBehaviour
{
    [Header("シーン設定")]
    [SerializeField] private string gameSceneName = "Fix_Alpha2_Fuyuno";

    [Header("確認ウィンドウ")]
    [SerializeField] private GameObject quitConfirmPanel;

    [Header("ボタン")]
    [SerializeField] private Button noButton;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button continueButton;

    [Header("Save Data List")]
    [SerializeField] private GameObject saveListPanel;
    [SerializeField] private GameObject loadConfirmPanel;
    [SerializeField] private TitleSaveSlotView[] saveSlotViews;
    [SerializeField] private Sprite defaultStageThumbnail;
    [SerializeField] private Sprite emptySlotThumbnail;
    [SerializeField] private LocationDatabase locationDatabase;
    [SerializeField] private StageDisplayInfo[] stageDisplayInfos;

    private int selectedSaveSlotIndex = SaveManager.DefaultSlotIndex;

    private void OnEnable()
    {
        RefreshContinueButtonState();
    }

    private void Start()
    {
        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        if (saveListPanel != null)
        {
            saveListPanel.SetActive(false);
        }

        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(false);
        }

        ResolveButtonReferences();
        BindContinueButton();
        RefreshContinueButtonState();
    }

    public void OnClickStartButton()
    {
        SaveManager.DeleteSave();
        SaveManager.ClearAllFlags();
        SaveManager.ClearAllItems();
        CurrentLocationService.ClearCurrentLocation();

        SceneManager.LoadScene(gameSceneName);
    }

    public void OnClickContinueButton()
    {
        if (!SaveManager.HasAnySave())
        {
            return;
        }

        ShowSaveListPanel();
    }

    public void OnClickQuitButton()
    {
        if (quitConfirmPanel == null)
        {
            return;
        }

        quitConfirmPanel.SetActive(true);

        if (EventSystem.current != null && noButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
        }
    }

    public void OnClickNoButton()
    {
        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnClickYesButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ResolveButtonReferences()
    {
        if (continueButton == null)
        {
            var obj = GameObject.Find("Btn_Countinue");
            if (obj != null)
            {
                continueButton = obj.GetComponent<Button>();
            }
        }

        if (noButton == null && quitConfirmPanel != null)
        {
            noButton = FindButtonInChildren(quitConfirmPanel.transform, "Btn_NO");
        }

        if (yesButton == null && quitConfirmPanel != null)
        {
            yesButton = FindButtonInChildren(quitConfirmPanel.transform, "Btn_YES");
        }
    }

    private void BindContinueButton()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.onClick.RemoveListener(OnClickContinueButton);
        continueButton.onClick.AddListener(OnClickContinueButton);
    }

    private void RefreshContinueButtonState()
    {
        ResolveButtonReferences();
        BindContinueButton();

        if (continueButton != null)
        {
            continueButton.interactable = SaveManager.HasAnySave();
        }
    }

    private static Button FindButtonInChildren(Transform root, string name)
    {
        Transform found = FindTransformInChildren(root, name);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static Transform FindTransformInChildren(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindTransformInChildren(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void ShowSaveListPanel()
    {
        if (saveListPanel == null)
        {
            Debug.LogWarning("[TitleSceneController] Save list panel is not assigned.");
            return;
        }

        RefreshSaveSlotViews();
        saveListPanel.SetActive(true);
        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(false);
        }
    }

    public void OnClickSaveListBackButton()
    {
        if (saveListPanel != null)
        {
            saveListPanel.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnClickSaveSlot(int slotIndex)
    {
        SelectSaveSlot(slotIndex);
    }

    public string GetStageDisplayName(string sceneName)
    {
        return GetStageDisplayName(sceneName, string.Empty);
    }

    public string GetStageDisplayName(string sceneName, string locationId)
    {
        if (locationDatabase != null &&
            locationDatabase.TryGetDisplayInfo(locationId, out LocationDisplayInfo locationInfo) &&
            !string.IsNullOrWhiteSpace(locationInfo.displayName))
        {
            return locationInfo.displayName;
        }

        StageDisplayInfo displayInfo = FindStageDisplayInfo(sceneName);
        if (!string.IsNullOrWhiteSpace(displayInfo.displayName))
        {
            return displayInfo.displayName;
        }

        return string.IsNullOrWhiteSpace(sceneName) ? "セーブデータなし" : sceneName;
    }

    public Sprite GetStageThumbnail(string sceneName)
    {
        return GetStageThumbnail(sceneName, string.Empty);
    }

    public Sprite GetStageThumbnail(string sceneName, string locationId)
    {
        if (locationDatabase != null &&
            locationDatabase.TryGetDisplayInfo(locationId, out LocationDisplayInfo locationInfo) &&
            locationInfo.thumbnail != null)
        {
            return locationInfo.thumbnail;
        }

        StageDisplayInfo displayInfo = FindStageDisplayInfo(sceneName);
        if (displayInfo.thumbnail != null)
        {
            return displayInfo.thumbnail;
        }

        return defaultStageThumbnail;
    }

    public Sprite GetEmptySlotThumbnail()
    {
        return emptySlotThumbnail != null ? emptySlotThumbnail : defaultStageThumbnail;
    }

    public void OnClickLoadConfirmYesButton()
    {
        if (!SaveManager.TryLoadGame(selectedSaveSlotIndex, gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    public void OnClickLoadConfirmNoButton()
    {
        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(false);
        }
    }

    private void RefreshSaveSlotViews()
    {
        if ((saveSlotViews == null || saveSlotViews.Length == 0) && saveListPanel != null)
        {
            saveSlotViews = saveListPanel.GetComponentsInChildren<TitleSaveSlotView>(true);
        }

        if (saveSlotViews == null)
        {
            return;
        }

        for (int i = 0; i < saveSlotViews.Length; i++)
        {
            if (saveSlotViews[i] != null)
            {
                saveSlotViews[i].Refresh(this);
            }
        }
    }

    private StageDisplayInfo FindStageDisplayInfo(string sceneName)
    {
        if (stageDisplayInfos == null || string.IsNullOrWhiteSpace(sceneName))
        {
            return default;
        }

        for (int i = 0; i < stageDisplayInfos.Length; i++)
        {
            if (stageDisplayInfos[i].Matches(sceneName))
            {
                return stageDisplayInfos[i];
            }
        }

        return default;
    }

    private void SelectSaveSlot(int slotIndex)
    {
        if (!SaveManager.HasSave(slotIndex))
        {
            return;
        }

        selectedSaveSlotIndex = slotIndex;
        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(true);
        }
    }
}

[System.Serializable]
public struct StageDisplayInfo
{
    public string sceneName;
    public string displayName;
    public Sprite thumbnail;

    public bool Matches(string targetSceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
               string.Equals(sceneName, targetSceneName, System.StringComparison.Ordinal);
    }
}
