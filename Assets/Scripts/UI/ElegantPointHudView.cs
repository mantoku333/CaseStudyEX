using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ElegantPointHudView : MonoBehaviour
{
    private const string GaugeObjectName = "Elegant Point Gauge";
    private const float DefaultFillWidth = 271f;
    private static readonly Vector2 GaugePosition = new Vector2(117f, -106f);
    private static readonly Vector2 GaugeSize = new Vector2(441f, 38f);
    private static readonly Vector2 FillPosition = new Vector2(81f, -12f);
    private static readonly Vector2 FillSize = new Vector2(DefaultFillWidth, 13f);
    private static readonly Vector2 BalancePosition = new Vector2(370f, 0f);
    private static readonly Vector2 BalanceSize = new Vector2(55f, 35f);

    [SerializeField] private Transform hudGroup;
    [SerializeField] private Image gaugeFill;
    [SerializeField] private TMP_Text balanceText;

    public RectTransform AbsorbTargetRect
    {
        get
        {
            if (gaugeFill != null)
            {
                return gaugeFill.rectTransform;
            }

            return transform as RectTransform;
        }
    }

    private void Awake()
    {
        EnsureView();
        EnsureGainVfxController();
    }

    private void OnEnable()
    {
        EnsureView();
        EnsureGainVfxController();
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

            RectTransform fillRect = gaugeFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 1f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 1f);
            fillRect.localScale = Vector3.one;
            fillRect.anchoredPosition = FillPosition;
            Vector2 size = fillRect.sizeDelta;
            size.y = FillSize.y;
            size.x = DefaultFillWidth * normalizedBalance;
            fillRect.sizeDelta = size;
        }

        if (balanceText != null)
        {
            balanceText.text = clampedBalance.ToString();
        }
    }

    private void EnsureView()
    {
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
        gaugeRect.anchorMin = new Vector2(0f, 1f);
        gaugeRect.anchorMax = new Vector2(0f, 1f);
        gaugeRect.pivot = new Vector2(0f, 1f);
        gaugeRect.localScale = Vector3.one;
        gaugeRect.anchoredPosition = GaugePosition;
        gaugeRect.sizeDelta = GaugeSize;

        Image background = gaugeObject.GetComponent<Image>();
        if (background == null)
        {
            background = gaugeObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.035f, 0.17f, 0.92f);
        }
        background.raycastTarget = false;

        Transform existingFill = gaugeObject.transform.Find("Fill");
        GameObject fillObject = existingFill != null
            ? existingFill.gameObject
            : CreateUiObject("Fill", gaugeObject.transform);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 1f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 1f);
        fillRect.localScale = Vector3.one;
        fillRect.anchoredPosition = FillPosition;
        fillRect.sizeDelta = FillSize;

        gaugeFill = fillObject.GetComponent<Image>();
        if (gaugeFill == null)
        {
            gaugeFill = fillObject.AddComponent<Image>();
            gaugeFill.color = new Color(0.86f, 0.12f, 0.91f, 1f);
        }
        gaugeFill.type = Image.Type.Simple;
        gaugeFill.raycastTarget = false;

        Transform existingText = gaugeObject.transform.Find("Balance");
        GameObject textObject = existingText != null
            ? existingText.gameObject
            : CreateUiObject("Balance", gaugeObject.transform);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.localScale = Vector3.one;
        textRect.anchoredPosition = BalancePosition;
        textRect.sizeDelta = BalanceSize;

        balanceText = textObject.GetComponent<TextMeshProUGUI>();
        if (balanceText == null)
        {
            balanceText = textObject.AddComponent<TextMeshProUGUI>();
            balanceText.color = new Color(0.8196079f, 0.5137255f, 0.9490197f, 1f);
        }
        balanceText.alignment = TextAlignmentOptions.Left;
        balanceText.fontSize = 24f;
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

    private void EnsureGainVfxController()
    {
        if (GetComponent<ElegantPointGainVfxController>() == null)
        {
            gameObject.AddComponent<ElegantPointGainVfxController>();
        }
    }
}
