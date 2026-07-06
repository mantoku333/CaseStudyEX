using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OptionsMainMenuSkin : MonoBehaviour
{
    private OptionsMenu backend;
    private Button continueButton;
    private Button saveButton;
    private Button mapButton;
    private Button optionButton;
    private Button titleButton;
    private Button skillButton;

    private bool listenersRegistered;

    private void Awake()
    {
        Resolve();
        RegisterListeners();
    }

    private void OnEnable()
    {
        Resolve();
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public void SelectDefaultButton()
    {
        SelectButton(continueButton);
    }

    public void SelectOptionButton()
    {
        SelectButton(optionButton);
    }

    private void Resolve()
    {
        if (backend == null)
        {
            backend = GetComponentInParent<OptionsMenu>(true);
        }

        continueButton = EnsureButton("Continue");
        saveButton = EnsureButton("Save");
        mapButton = EnsureButton("Map");
        optionButton = EnsureButton("Option");
        titleButton = EnsureButton("TitleBack");
        skillButton = EnsureButton("Skill");
    }

    private void RegisterListeners()
    {
        if (listenersRegistered || backend == null)
        {
            return;
        }

        Bind(continueButton, backend.RequestCloseMenu);
        Bind(saveButton, backend.SaveCurrentGame);
        Bind(mapButton, backend.RequestOpenMap);
        Bind(optionButton, backend.RequestShowOptionDetail);
        Bind(skillButton, backend.OpenSkillList);
        Bind(titleButton, backend.RequestShowFinishPrompt);
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered || backend == null)
        {
            return;
        }

        Unbind(continueButton, backend.RequestCloseMenu);
        Unbind(saveButton, backend.SaveCurrentGame);
        Unbind(mapButton, backend.RequestOpenMap);
        Unbind(optionButton, backend.RequestShowOptionDetail);
        Unbind(skillButton, backend.OpenSkillList);
        Unbind(titleButton, backend.RequestShowFinishPrompt);
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
        UIButtonSfxPlayer.RegisterHover(button);
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

public sealed class OptionsMenuButtonState :
    MonoBehaviour,
    ISelectHandler,
    IDeselectHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private static readonly System.Collections.Generic.List<OptionsMenuButtonState> Instances = new();
    private static OptionsMenuButtonState hoveredInstance;
    private static bool pointerVisualMode;

    [SerializeField] private GameObject selectState;
    [SerializeField] private GameObject notSelectState;

    private void OnEnable()
    {
        if (!Instances.Contains(this))
        {
            Instances.Add(this);
        }

        Refresh();
    }

    private void OnDisable()
    {
        Instances.Remove(this);
        if (hoveredInstance == this)
        {
            hoveredInstance = null;
        }
    }

    public void Configure(Transform selectRoot, Transform notSelectRoot)
    {
        selectState = selectRoot != null ? selectRoot.gameObject : null;
        notSelectState = notSelectRoot != null ? notSelectRoot.gameObject : null;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!pointerVisualMode)
        {
            RefreshAll();
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!pointerVisualMode)
        {
            RefreshAll();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerVisualMode = true;
        hoveredInstance = this;
        RefreshAll();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoveredInstance == this)
        {
            hoveredInstance = null;
        }

        RefreshAll();
    }

    public static void ResetPointerVisualMode()
    {
        pointerVisualMode = false;
        hoveredInstance = null;
        RefreshAll();
    }

    private void Refresh()
    {
        bool showSelected;
        if (pointerVisualMode)
        {
            showSelected = hoveredInstance == this;
        }
        else
        {
            showSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        }

        if (selectState != null)
        {
            selectState.SetActive(showSelected);
        }

        if (notSelectState != null)
        {
            notSelectState.SetActive(!showSelected);
        }
    }

    private static void RefreshAll()
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            if (Instances[i] != null)
            {
                Instances[i].Refresh();
            }
        }
    }
}
