using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MantokuStoryOptionsFinishPromptSkin : MonoBehaviour
{
    private const string BackgroundNodeName = "Background";
    private const string DimBackgroundName = "背景の暗さ";

    private MantokuStoryOptionsMenu backend;
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
        MantokuStoryOptionsMenuButtonState.ResetPointerVisualMode();
        SelectButton(noButton);
    }

    private void Resolve()
    {
        NormalizeLayout();

        if (backend == null)
        {
            backend = GetComponentInParent<MantokuStoryOptionsMenu>(true);
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

        Bind(yesButton, backend.ReturnToTitle);
        Bind(noButton, backend.HideFinishPrompt);
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered || backend == null)
        {
            return;
        }

        Unbind(yesButton, backend.ReturnToTitle);
        Unbind(noButton, backend.HideFinishPrompt);
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

        MantokuStoryOptionsMenuButtonState state = target.GetComponent<MantokuStoryOptionsMenuButtonState>();
        if (state == null)
        {
            state = target.gameObject.AddComponent<MantokuStoryOptionsMenuButtonState>();
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

        Graphic[] graphics = stateRoot.GetComponentsInChildren<Graphic>(true);
        if (graphics.Length == 0)
        {
            return null;
        }

        Graphic bestGraphic = null;
        float bestArea = float.MinValue;
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
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
