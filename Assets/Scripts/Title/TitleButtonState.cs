using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleButtonState :
    MonoBehaviour,
    ISelectHandler,
    IDeselectHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private GameObject selectState;
    [SerializeField] private GameObject notSelectState;

    private static TitleButtonState hoveredInstance;
    private static bool pointerVisualMode;

    private void Awake()
    {
        if (selectState == null)
        {
            Transform t = transform.Find("Select");
            if (t != null) selectState = t.gameObject;
        }

        if (notSelectState == null)
        {
            Transform t = transform.Find("Not Select");
            if (t != null) notSelectState = t.gameObject;
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!pointerVisualMode) Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!pointerVisualMode) Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerVisualMode  = true;
        hoveredInstance    = this;
        RefreshAll();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoveredInstance == this) hoveredInstance = null;
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
        bool showSelected = pointerVisualMode
            ? hoveredInstance == this
            : EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

        if (selectState != null)
        {
            selectState.SetActive(showSelected);
        }

        if (notSelectState != null)
        {
            // selectState がない場合は notSelectState を常に表示したままにする
            bool hideNotSelect = showSelected && selectState != null;
            notSelectState.SetActive(!hideNotSelect);
        }
    }

    private static void RefreshAll()
    {
        TitleButtonState[] all = FindObjectsByType<TitleButtonState>(FindObjectsSortMode.None);
        foreach (TitleButtonState b in all)
        {
            if (b != null) b.Refresh();
        }
    }
}
