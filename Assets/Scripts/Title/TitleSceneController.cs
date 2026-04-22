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

    private void Start()
    {
        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        ResolveContinueButtonReference();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnClickContinueButton);
            continueButton.onClick.AddListener(OnClickContinueButton);
            continueButton.interactable = SaveManager.HasSave();
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
        if (!SaveManager.TryLoadGame(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
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
}
