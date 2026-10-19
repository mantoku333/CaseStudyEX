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
    private static TitleButtonState lockedInstance;
    private static bool selectionLocked;

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
        if (selectionLocked) return;

        pointerVisualMode  = true;
        hoveredInstance    = this;
        RefreshAll();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (selectionLocked) return;

        if (hoveredInstance == this) hoveredInstance = null;
        RefreshAll();
    }

    // 初めから演出中など、選択表示をひとつのボタンに固定したいときに使う。
    // 固定している間はホバーもナビゲーションも見た目を変えない。
    public static void LockSelection(GameObject target)
    {
        selectionLocked = true;
        lockedInstance = target != null ? target.GetComponent<TitleButtonState>() : null;
        RefreshAll();
    }

    public static void UnlockSelection()
    {
        selectionLocked = false;
        lockedInstance = null;
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
        if (selectionLocked)
        {
            showSelected = lockedInstance == this;
        }
        else if (pointerVisualMode)
        {
            showSelected = hoveredInstance == this;
        }
        else
        {
            showSelected = EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject;
        }

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
