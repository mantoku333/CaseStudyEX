using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleSceneController : MonoBehaviour
{
    [Header("シーン設定")]
    [SerializeField] private string gameSceneName = "Story_Mantoku";

    [Header("確認ウィンドウ")]
    [SerializeField] private GameObject quitConfirmPanel;

    [Header("ボタン")]
    [SerializeField] private Button noButton;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button continueButton;

    [Header("Save Data List")]
    [SerializeField] private GameObject saveListPanel;
    [SerializeField] private GameObject loadConfirmPanel;

    private int selectedSaveSlotIndex = SaveManager.DefaultSlotIndex;

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

        ResolveContinueButtonReference();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnClickContinueButton);
            continueButton.onClick.AddListener(OnClickContinueButton);
            continueButton.interactable = SaveManager.HasAnySave();
        }
    }

    public void OnClickStartButton()
    {
        SaveManager.DeleteSave();
        SaveManager.ClearAllFlags();
        SaveManager.ClearAllItems();

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

    private void ResolveContinueButtonReference()
    {
        if (continueButton != null)
        {
            return;
        }

        var continueObject = GameObject.Find("Btn_Countinue");
        if (continueObject != null)
        {
            continueButton = continueObject.GetComponent<Button>();
        }
    }

    private void ShowSaveListPanel()
    {
        if (saveListPanel == null)
        {
            Debug.LogWarning("[TitleSceneController] Save list panel is not assigned.");
            return;
        }

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

    public void OnClickSaveSlot1()
    {
        SelectSaveSlot(1);
    }

    public void OnClickSaveSlot2()
    {
        SelectSaveSlot(2);
    }

    public void OnClickSaveSlot3()
    {
        SelectSaveSlot(3);
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
