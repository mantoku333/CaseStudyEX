using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleSceneController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";

    public void OnClickStartButton()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    [Header("確認ウィンドウ")]
    [SerializeField] private GameObject quitConfirmPanel;

    [Header("ボタン")]
    [SerializeField] private Button noButton;
    [SerializeField] private Button yesButton;

    private void Start()
    {
        quitConfirmPanel.SetActive(false);
    }

    public void OnClickQuitButton()
    {
        quitConfirmPanel.SetActive(true);

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }

    public void OnClickNoButton()
    {
        quitConfirmPanel.SetActive(false);

        EventSystem.current.SetSelectedGameObject(null);
    }

    public void OnClickYesButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
