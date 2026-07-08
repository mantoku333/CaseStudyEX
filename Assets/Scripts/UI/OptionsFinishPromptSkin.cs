using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OptionsFinishPromptSkin : MonoBehaviour
{
    private const string BackgroundNodeName = "Background";
    private const string DimBackgroundName = "背景の暗さ";

    private OptionsMenu backend;
    private Button yesButton;
    private Button noButton;
    private bool listenersRegistered;

    private void Awake()
    {
        Resolve();
        RegisterListeners();
    }

    private void OnEnable()
    {
        NormalizeLayout();
        Resolve();
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public void SelectDefaultButton()
    {
        OptionsMenuButtonState.ResetPointerVisualMode();
        SelectButton(noButton);
    }

    private void Resolve()
    {
        NormalizeLayout();

        if (backend == null)
        {
            backend = GetComponentInParent<OptionsMenu>(true);
        }

        yesButton = EnsureButton("Yes");
        noButton = EnsureButton("No");
    }

    private void NormalizeLayout()
    {
        if (transform is not RectTransform rootRect)
        {
            return;
        }

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.localScale = Vector3.one;

        RectTransform referenceRect = FindChildRectTransform(BackgroundNodeName);
        if (referenceRect == null)
        {
            referenceRect = FindChildRectTransform(DimBackgroundName);
        }

        if (referenceRect != null)
        {
            rootRect.anchoredPosition = new Vector2(-referenceRect.anchoredPosition.x, -referenceRect.anchoredPosition.y);
        }
    }

    private RectTransform FindChildRectTransform(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child as RectTransform : null;
    }

    private void RegisterListeners()
    {
        if (listenersRegistered || backend == null)
        {
            return;
        }

        Bind(yesButton, backend.RequestReturnToTitle);
        Bind(noButton, backend.RequestHideFinishPrompt);
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered || backend == null)
        {
            return;
        }

        Unbind(yesButton, backend.RequestReturnToTitle);
        Unbind(noButton, backend.RequestHideFinishPrompt);
        listenersRegistered = false;
    }

    private Button EnsureButton(string normalizedName)
    {
        return OptionsCanvasButtonUtility.EnsureStateButton(transform, normalizedName);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
        UIButtonSfxPlayer.Register(button);
    }

    private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    private static void SelectButton(Button button)
    {
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
