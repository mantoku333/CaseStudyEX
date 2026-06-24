using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OptionsMainMenuSkin : MonoBehaviour
{
    private static readonly List<Graphic> GraphicSearchBuffer = new List<Graphic>(16);

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

        Bind(continueButton, backend.CloseMenu);
        Bind(saveButton, backend.SaveCurrentGame);
        Bind(mapButton, backend.OpenMap);
        Bind(optionButton, backend.ShowOptionDetail);
        Bind(skillButton, backend.OpenSkillList);
        Bind(titleButton, backend.ShowFinishPrompt);
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered || backend == null)
        {
            return;
        }

        Unbind(continueButton, backend.CloseMenu);
        Unbind(saveButton, backend.SaveCurrentGame);
        Unbind(mapButton, backend.OpenMap);
        Unbind(optionButton, backend.ShowOptionDetail);
        Unbind(skillButton, backend.OpenSkillList);
        Unbind(titleButton, backend.ShowFinishPrompt);
        listenersRegistered = false;
    }

    private Button EnsureButton(string normalizedName)
    {
        Transform target = FindByNormalizedName(normalizedName);
        if (target == null)
        {
            return null;
        }

        Transform selectRoot = target.Find("Select");
        Transform notSelectRoot = target.Find("Not Select");
        Graphic raycastGraphic = ConfigureRaycastGraphics(selectRoot, notSelectRoot);

        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;
        }
        else if (raycastGraphic == null)
        {
            image = target.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            raycastGraphic = image;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        button.transition = Selectable.Transition.None;
        button.targetGraphic = raycastGraphic;

        OptionsMenuButtonState state = target.GetComponent<OptionsMenuButtonState>();
        if (state == null)
        {
            state = target.gameObject.AddComponent<OptionsMenuButtonState>();
        }

        state.Configure(selectRoot, notSelectRoot);
        return button;
    }

    private static Graphic ConfigureRaycastGraphics(Transform selectRoot, Transform notSelectRoot)
    {
        Graphic selectGraphic = ConfigureStateRaycastGraphic(selectRoot);
        Graphic notSelectGraphic = ConfigureStateRaycastGraphic(notSelectRoot);
        return notSelectGraphic != null ? notSelectGraphic : selectGraphic;
    }

    private static Graphic ConfigureStateRaycastGraphic(Transform stateRoot)
    {
        if (stateRoot == null)
        {
            return null;
        }

        GraphicSearchBuffer.Clear();
        stateRoot.GetComponentsInChildren(true, GraphicSearchBuffer);
        if (GraphicSearchBuffer.Count == 0)
        {
            return null;
        }

        Graphic bestGraphic = null;
        float bestArea = float.MinValue;
        for (int i = 0; i < GraphicSearchBuffer.Count; i++)
        {
            Graphic graphic = GraphicSearchBuffer[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.raycastTarget = false;

            RectTransform rectTransform = graphic.rectTransform;
            float area = Mathf.Abs(rectTransform.rect.width * rectTransform.rect.height);
            if (area > bestArea)
            {
                bestArea = area;
                bestGraphic = graphic;
            }
        }

        if (bestGraphic != null)
        {
            bestGraphic.raycastTarget = true;
            if (bestGraphic is Image image)
            {
                image.alphaHitTestMinimumThreshold = 0.1f;
            }
        }

        GraphicSearchBuffer.Clear();
        return bestGraphic;
    }

    private Transform FindByNormalizedName(string normalizedName)
    {
        return FindInChildren(transform, normalizedName);
    }

    private static Transform FindInChildren(Transform root, string normalizedName)
    {
        if (root == null)
        {
            return null;
        }

        if (Normalize(root.name) == normalizedName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindInChildren(root.GetChild(i), normalizedName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (!char.IsWhiteSpace(character) && character != '\u3000')
            {
                buffer[count++] = character;
            }
        }

        return new string(buffer, 0, count);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
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
