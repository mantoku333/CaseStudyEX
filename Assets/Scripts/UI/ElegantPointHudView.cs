using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ElegantPointHudView : MonoBehaviour
{
    private const string GaugeObjectName = "Elegant Point Gauge";
    private const float FillAnchorLeft = 0.015f;
    private const float FillAnchorRight = 0.985f;
    private const float FillAnchorBottom = 0.16f;
    private const float FillAnchorTop = 0.84f;

    [SerializeField] private Transform hudGroup;
    [SerializeField] private Image gaugeFill;
    [SerializeField] private TMP_Text balanceText;

    private void Awake()
    {
        EnsureView();
    }

    private void OnEnable()
    {
        EnsureView();
        ElegantPointWallet.BalanceChanged += HandleBalanceChanged;
        Refresh(ElegantPointWallet.Balance);
    }

    private void OnDisable()
    {
        ElegantPointWallet.BalanceChanged -= HandleBalanceChanged;
    }

    private void HandleBalanceChanged(int balance)
    {
        Refresh(balance);
    }

    private void Refresh(int balance)
    {
        int clampedBalance = Mathf.Clamp(balance, 0, ElegantPointWallet.MaxBalance);

        if (gaugeFill != null)
        {
            float normalizedBalance = ElegantPointWallet.MaxBalance > 0
                ? clampedBalance / (float)ElegantPointWallet.MaxBalance
                : 0f;
            gaugeFill.fillAmount = normalizedBalance;

            RectTransform fillRect = gaugeFill.rectTransform;
            fillRect.anchorMin = new Vector2(FillAnchorLeft, FillAnchorBottom);
            fillRect.anchorMax = new Vector2(
                Mathf.Lerp(FillAnchorLeft, FillAnchorRight, normalizedBalance),
                FillAnchorTop);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        if (balanceText != null)
        {
            balanceText.text = $"{clampedBalance} / {ElegantPointWallet.MaxBalance}";
        }
    }

    private void EnsureView()
    {
        if (gaugeFill != null && balanceText != null)
        {
            return;
        }

        if (hudGroup == null)
        {
            hudGroup = transform.Find("Player HP");
        }

        if (hudGroup == null)
        {
            hudGroup = transform;
        }

        Transform existingGauge = hudGroup.Find(GaugeObjectName);
        GameObject gaugeObject = existingGauge != null
            ? existingGauge.gameObject
            : CreateUiObject(GaugeObjectName, hudGroup);

        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(0.5f, 0.5f);
        gaugeRect.anchorMax = new Vector2(0.5f, 0.5f);
        gaugeRect.pivot = new Vector2(0f, 1f);
        gaugeRect.anchoredPosition = new Vector2(124.8f, -50f);
        gaugeRect.sizeDelta = new Vector2(439f, 22f);

        Image background = gaugeObject.GetComponent<Image>();
        if (background == null)
        {
            background = gaugeObject.AddComponent<Image>();
        }
        background.color = new Color(0.12f, 0.035f, 0.17f, 0.92f);
        background.raycastTarget = false;

        Transform existingFill = gaugeObject.transform.Find("Fill");
        GameObject fillObject = existingFill != null
            ? existingFill.gameObject
            : CreateUiObject("Fill", gaugeObject.transform);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(FillAnchorLeft, FillAnchorBottom);
        fillRect.anchorMax = new Vector2(FillAnchorRight, FillAnchorTop);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        gaugeFill = fillObject.GetComponent<Image>();
        if (gaugeFill == null)
        {
            gaugeFill = fillObject.AddComponent<Image>();
        }
        gaugeFill.color = new Color(0.86f, 0.12f, 0.91f, 1f);
        gaugeFill.type = Image.Type.Simple;
        gaugeFill.raycastTarget = false;

        Transform existingText = gaugeObject.transform.Find("Balance");
        GameObject textObject = existingText != null
            ? existingText.gameObject
            : CreateUiObject("Balance", gaugeObject.transform);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        balanceText = textObject.GetComponent<TextMeshProUGUI>();
        if (balanceText == null)
        {
            balanceText = textObject.AddComponent<TextMeshProUGUI>();
        }
        balanceText.alignment = TextAlignmentOptions.Center;
        balanceText.color = Color.white;
        balanceText.fontSize = 16f;
        balanceText.fontStyle = FontStyles.Bold;
        balanceText.raycastTarget = false;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        var gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
