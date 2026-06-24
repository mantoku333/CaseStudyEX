using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleMenuSkin : MonoBehaviour
{
    private static readonly List<Graphic> GraphicSearchBuffer = new List<Graphic>(16);

    private TitleSceneController controller;
    private bool listenersRegistered;

    private Button newGameButton;
    private Button continueButton;
    private Button exitGameButton;
    private Button quitYesButton;
    private Button quitNoButton;
    private Button loadYesButton;
    private Button loadNoButton;
    private Button saveListBackButton;

    private void Awake()
    {
        controller = GetComponentInParent<TitleSceneController>(true);
        if (controller == null)
        {
            controller = FindObjectOfType<TitleSceneController>();
        }

        newGameButton  = EnsureButton(transform, "Btn_NewGame");
        continueButton = EnsureButton(transform, "Btn_Countinue");
        exitGameButton = EnsureButton(transform, "Btn_ExitGame");

        Transform quitPanel = FindInChildren(transform, "Panel_QuitConfirm");
        if (quitPanel != null)
        {
            quitYesButton = EnsureButton(quitPanel, "Btn_YES");
            quitNoButton  = EnsureButton(quitPanel, "Btn_NO");
        }

        Transform loadPanel = FindInChildren(transform, "LoadConfirmPanel");
        if (loadPanel != null)
        {
            loadYesButton = EnsureButton(loadPanel, "Btn_YES");
            loadNoButton  = EnsureButton(loadPanel, "Btn_NO");
        }

        Transform saveListPanel = FindInChildren(transform, "SaveListPanel");
        if (saveListPanel != null)
        {
            saveListBackButton = EnsureButton(saveListPanel, "Back");
        }

        RegisterListeners();
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    private void RegisterListeners()
    {
        if (listenersRegistered || controller == null)
        {
            return;
        }

        Bind(newGameButton,      controller.OnClickStartButton);
        Bind(continueButton,     controller.OnClickContinueButton);
        Bind(exitGameButton,     controller.OnClickQuitButton);
        Bind(quitYesButton,      controller.OnClickYesButton);
        Bind(quitNoButton,       controller.OnClickNoButton);
        Bind(loadYesButton,      controller.OnClickLoadConfirmYesButton);
        Bind(loadNoButton,       controller.OnClickLoadConfirmNoButton);
        Bind(saveListBackButton, controller.OnClickSaveListBackButton);

        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered || controller == null)
        {
            return;
        }

        Unbind(newGameButton,      controller.OnClickStartButton);
        Unbind(continueButton,     controller.OnClickContinueButton);
        Unbind(exitGameButton,     controller.OnClickQuitButton);
        Unbind(quitYesButton,      controller.OnClickYesButton);
        Unbind(quitNoButton,       controller.OnClickNoButton);
        Unbind(loadYesButton,      controller.OnClickLoadConfirmYesButton);
        Unbind(loadNoButton,       controller.OnClickLoadConfirmNoButton);
        Unbind(saveListBackButton, controller.OnClickSaveListBackButton);

        listenersRegistered = false;
    }

    private static Button EnsureButton(Transform searchRoot, string targetName)
    {
        Transform target = FindInChildren(searchRoot, targetName);
        if (target == null)
        {
            return null;
        }

        Transform selectRoot    = target.Find("Select");
        Transform notSelectRoot = target.Find("Not Select");

        DisableChildRaycasts(selectRoot);
        DisableChildRaycasts(notSelectRoot);

        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            image = target.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
        }
        image.raycastTarget = true;

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        button.transition    = Selectable.Transition.None;
        button.targetGraphic = image;

        OptionsMenuButtonState state = target.GetComponent<OptionsMenuButtonState>();
        if (state == null)
        {
            state = target.gameObject.AddComponent<OptionsMenuButtonState>();
        }

        state.Configure(selectRoot, notSelectRoot);
        return button;
    }

    private static void DisableChildRaycasts(Transform stateRoot)
    {
        if (stateRoot == null)
        {
            return;
        }

        GraphicSearchBuffer.Clear();
        stateRoot.GetComponentsInChildren(true, GraphicSearchBuffer);
        for (int i = 0; i < GraphicSearchBuffer.Count; i++)
        {
            Graphic graphic = GraphicSearchBuffer[i];
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }

        GraphicSearchBuffer.Clear();
    }

    private static Transform FindInChildren(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindInChildren(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void Unbind(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }
}
