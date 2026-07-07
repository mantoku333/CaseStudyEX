using UnityEngine;
using UnityEngine.UI;

public static class OptionsCanvasButtonUtility
{
    public static Button EnsureStateButton(Transform searchRoot, string normalizedName)
    {
        Transform target = FindByNormalizedName(searchRoot, normalizedName);
        if (target == null)
        {
            return null;
        }

        return EnsureStateButton(target);
    }

    public static Button EnsureStateButton(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Transform selectRoot = target.Find("Select");
        Transform notSelectRoot = target.Find("Not Select");
        Graphic raycastGraphic = ConfigureRaycastGraphics(selectRoot, notSelectRoot, useAlphaHitTest: true);

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
        ConfigureFeedback(button, null, useDimHover: false, selectRoot, notSelectRoot);
        return button;
    }

    public static Button ConfigureExistingGraphicButton(GameObject target)
    {
        return ConfigureExistingGraphicButton(target, useDimHover: true);
    }

    public static Button ConfigureExistingGraphicButton(GameObject target, bool useDimHover)
    {
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            return null;
        }

        button.transition = Selectable.Transition.None;
        Graphic targetGraphic = FindLargestGraphic(target.transform);
        if (targetGraphic != null)
        {
            button.targetGraphic = targetGraphic;
            targetGraphic.raycastTarget = true;
        }

        ConfigureFeedback(button, targetGraphic, useDimHover, target.transform);
        return button;
    }

    public static Button ConfigureSingleIllustrationButton(Button button)
    {
        return ConfigureSingleIllustrationButton(button, useDimHover: true);
    }

    public static Button ConfigureFigmaButton(Button button)
    {
        if (button == null)
        {
            return null;
        }

        Transform selectRoot = button.transform.Find("Select");
        Transform notSelectRoot = button.transform.Find("Not Select");
        if (selectRoot != null || notSelectRoot != null)
        {
            return EnsureStateButton(button.transform);
        }

        return ConfigureSingleIllustrationButton(button);
    }

    public static Button ConfigureSingleIllustrationButton(Button button, bool useDimHover)
    {
        if (button == null)
        {
            return null;
        }

        button.transition = Selectable.Transition.None;
        Graphic targetGraphic = FindHitAreaGraphic(button.transform);
        Graphic feedbackGraphic = null;
        if (targetGraphic == null)
        {
            targetGraphic = FindLargestVisibleGraphic(button.transform);
            feedbackGraphic = targetGraphic;
        }
        else
        {
            DisableOtherRaycastGraphics(button.transform, targetGraphic);
            feedbackGraphic = FindLargestVisibleGraphic(button.transform);
        }
        if (targetGraphic == null)
        {
            targetGraphic = button.targetGraphic != null
                ? button.targetGraphic
                : FindLargestGraphic(button.transform);
            feedbackGraphic = targetGraphic;
        }

        if (feedbackGraphic == null || feedbackGraphic == targetGraphic)
        {
            feedbackGraphic = FindLargestVisibleGraphic(button.transform);
        }

        if (targetGraphic != null)
        {
            button.targetGraphic = targetGraphic;
            targetGraphic.raycastTarget = true;
        }

        ConfigureFeedback(button, feedbackGraphic, useDimHover, button.transform);
        return button;
    }

    private static Graphic FindHitAreaGraphic(Transform root)
    {
        Transform hitArea = root != null ? root.Find("HitArea") : null;
        if (hitArea == null)
        {
            return null;
        }

        Graphic graphic = hitArea.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }

        return graphic;
    }

    private static void DisableOtherRaycastGraphics(Transform root, Graphic allowedGraphic)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
            {
                continue;
            }

            graphics[i].raycastTarget = graphics[i] == allowedGraphic;
        }
    }

    public static void ConfigurePressOnlyFeedback(Button button)
    {
        ConfigureFeedback(button, null, useDimHover: false, button != null ? button.transform : null);
    }

    public static void ConfigureIllustrationOnly(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = false;
            button.enabled = false;
        }

        OptionsCanvasButtonFeedback feedback = target.GetComponent<OptionsCanvasButtonFeedback>();
        if (feedback != null)
        {
            feedback.enabled = false;
        }

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    public static Transform FindByNormalizedName(Transform root, string normalizedName)
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
            Transform match = FindByNormalizedName(root.GetChild(i), normalizedName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public static string Normalize(string value)
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

    private static void ConfigureFeedback(Button button, Graphic targetGraphic, bool useDimHover, params Transform[] scaleTargets)
    {
        if (button == null)
        {
            return;
        }

        OptionsCanvasButtonFeedback feedback = button.GetComponent<OptionsCanvasButtonFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<OptionsCanvasButtonFeedback>();
        }

        feedback.Configure(button, targetGraphic, useDimHover, scaleTargets);
    }

    private static Graphic ConfigureRaycastGraphics(Transform selectRoot, Transform notSelectRoot, bool useAlphaHitTest)
    {
        Graphic selectGraphic = ConfigureStateRaycastGraphic(selectRoot, useAlphaHitTest);
        Graphic notSelectGraphic = ConfigureStateRaycastGraphic(notSelectRoot, useAlphaHitTest);
        return notSelectGraphic != null ? notSelectGraphic : selectGraphic;
    }

    private static Graphic ConfigureStateRaycastGraphic(Transform stateRoot, bool useAlphaHitTest)
    {
        if (stateRoot == null)
        {
            return null;
        }

        Graphic bestGraphic = FindLargestGraphic(stateRoot);
        if (bestGraphic != null)
        {
            bestGraphic.raycastTarget = true;
            if (useAlphaHitTest && bestGraphic is Image image)
            {
                image.alphaHitTestMinimumThreshold = 0.1f;
            }
        }

        return bestGraphic;
    }

    private static Graphic FindLargestGraphic(Transform root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        if (graphics.Length == 0)
        {
            return null;
        }

        Graphic bestGraphic = null;
        float bestArea = float.MinValue;
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
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

        return bestGraphic;
    }

    private static Graphic FindLargestVisibleGraphic(Transform root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        if (graphics.Length == 0)
        {
            return null;
        }

        Graphic bestGraphic = null;
        float bestArea = float.MinValue;
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || !graphic.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (graphic.color.a <= 0.01f)
            {
                continue;
            }

            if (graphic is Image image && image.sprite == null)
            {
                continue;
            }

            RectTransform rectTransform = graphic.rectTransform;
            float area = Mathf.Abs(rectTransform.rect.width * rectTransform.rect.height);
            if (area > bestArea)
            {
                bestArea = area;
                bestGraphic = graphic;
            }
        }

        return bestGraphic;
    }
}
