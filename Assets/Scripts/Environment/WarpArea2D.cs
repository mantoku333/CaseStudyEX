using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
// ワープ範囲に入ったプレイヤーへ案内文を出し、Interact入力で指定地点へ移動させる。
public sealed class WarpArea2D : MonoBehaviour
{
    [Header("Warp")]
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool clearVelocity = true;

    [Header("Prompt")]
    [SerializeField] private TextMeshPro promptText;
    // 日本語などを表示したい場合は、TTFではなくTMPのFont Assetを指定する。
    [SerializeField] private TMP_FontAsset promptFontAsset;
    [SerializeField] private bool autoCreatePrompt = true;
    [SerializeField] private string promptMessage = "Space: Warp";
    [SerializeField] private Color promptTextColor = Color.white;
    [SerializeField, Min(0f)] private float promptVerticalPadding = 0.45f;
    // promptVerticalPaddingで決めた基準位置から、文字だけを微調整する。
    [SerializeField] private Vector2 promptTextOffset = Vector2.zero;
    [SerializeField, Min(0.01f)] private float promptFontSize = 3f;
    [SerializeField] private int promptSortingOrder = 50;

    [Header("Prompt Animation")]
    // 表示率を0から1へ変えながら、下から上へ動かしてフェードさせる。
    [SerializeField, Min(0f)] private float promptAnimationDuration = 0.25f;
    [SerializeField, Min(0f)] private float promptLiftDistanceY = 0.05f;

    [Header("Prompt Background")]
    [SerializeField] private SpriteRenderer promptBackground;
    [SerializeField] private bool autoCreateBackground = true;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    // 背景は文字の表示サイズに、この余白を足した大きさになる。
    [SerializeField] private Vector2 backgroundPadding = new Vector2(0.6f, 0.25f);
    [SerializeField] private Vector2 backgroundOffset = new Vector2(0f, -0.12f);
    [SerializeField] private int backgroundSortingOrder = 49;

    private static Texture2D sharedBackgroundTexture;
    private static Sprite sharedBackgroundSprite;

    private Collider2D triggerCollider;
    private global::PlayerController currentPlayer;
    private Rigidbody2D currentPlayerRigidbody;
    private InputAction interactAction;
    private Coroutine restoreControlRoutine;
    private Coroutine promptAnimationRoutine;
    private global::PlayerController restoreLockedPlayer;
    // 0が完全に非表示、1が完全に表示。アルファ値と上下移動の両方に使う。
    private float promptVisibility;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        ConfigurePrompt();
        ConfigureBackground();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsurePrompt();
        EnsureBackground();
        HidePromptImmediate();
    }

    private void OnDisable()
    {
        HidePromptImmediate();
        currentPlayer = null;
        currentPlayerRigidbody = null;
        interactAction = null;
        RestorePlayerControlIfNeeded();
    }

    private void Update()
    {
        if (currentPlayer == null || destinationPoint == null || interactAction == null)
        {
            return;
        }

        if (interactAction.WasPressedThisFrame())
        {
            WarpCurrentPlayer();
        }
    }

    private void LateUpdate()
    {
        if (promptText != null && promptText.gameObject.activeSelf)
        {
            // Play中にInspectorで変えた文字サイズや色を、表示中のプロンプトへすぐ反映する。
            ConfigurePrompt();
            ConfigureBackground();
        }

        PositionPrompt();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other, out var player))
        {
            return;
        }

        currentPlayer = player;
        currentPlayerRigidbody = currentPlayer.GetComponent<Rigidbody2D>();
        ResolveInteractAction(currentPlayer);
        ShowPrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (currentPlayer == null || !IsPlayerCollider(other, out var player) || player != currentPlayer)
        {
            return;
        }

        currentPlayer = null;
        currentPlayerRigidbody = null;
        interactAction = null;
        HidePrompt();
    }

    private bool IsPlayerCollider(Collider2D other, out global::PlayerController player)
    {
        player = null;
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
        {
            return false;
        }

        player = other.GetComponentInParent<global::PlayerController>();
        return player != null;
    }

    private void ResolveInteractAction(global::PlayerController player)
    {
        interactAction = null;
        if (player == null)
        {
            return;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogWarning($"[WarpArea2D] PlayerInput or actions are missing on '{player.name}'.", this);
            return;
        }

        interactAction = playerInput.actions.FindAction(interactActionName, false);
        if (interactAction == null)
        {
            Debug.LogWarning($"[WarpArea2D] Input action '{interactActionName}' was not found.", this);
            return;
        }

        if (!interactAction.enabled)
        {
            interactAction.Enable();
        }
    }

    private void WarpCurrentPlayer()
    {
        if (currentPlayer == null || destinationPoint == null)
        {
            return;
        }

        global::PlayerController playerToWarp = currentPlayer;
        Rigidbody2D rigidbodyToWarp = currentPlayerRigidbody != null
            ? currentPlayerRigidbody
            : playerToWarp.GetComponent<Rigidbody2D>();
        DodgeController dodgeController = playerToWarp.GetComponent<DodgeController>();
        if (dodgeController != null)
        {
            dodgeController.CancelCurrentDodgeMovement();
        }

        bool acquiredControlLock = !playerToWarp.IsExternalControlLocked;
        if (acquiredControlLock)
        {
            // 同じ入力がワープ後にジャンプとして処理されないよう、短時間だけ操作を止める。
            playerToWarp.SetExternalControlLocked(true);
        }

        Vector3 destination = destinationPoint.position;
        if (rigidbodyToWarp != null)
        {
            rigidbodyToWarp.position = new Vector2(destination.x, destination.y);
            playerToWarp.transform.position = destination;

            if (clearVelocity)
            {
                rigidbodyToWarp.linearVelocity = Vector2.zero;
                rigidbodyToWarp.angularVelocity = 0f;
                rigidbodyToWarp.Sleep();
            }
        }
        else
        {
            playerToWarp.transform.position = destination;
        }

        Physics2D.SyncTransforms();
        RoomCameraTrigger.TryActivateRoomAtPosition(playerToWarp.transform.position, out _);
        RoomCameraSwitchPortal.TryActivateAtPlayerPosition(playerToWarp.transform);
        currentPlayer = null;
        currentPlayerRigidbody = null;
        interactAction = null;
        HidePromptImmediate();

        if (acquiredControlLock)
        {
            StartRestoreControlRoutine(playerToWarp);
        }
    }

    private void StartRestoreControlRoutine(global::PlayerController player)
    {
        RestorePlayerControlIfNeeded();
        restoreLockedPlayer = player;
        restoreControlRoutine = StartCoroutine(RestoreControlAfterFixedUpdate());
    }

    private IEnumerator RestoreControlAfterFixedUpdate()
    {
        yield return new WaitForFixedUpdate();

        if (restoreLockedPlayer != null)
        {
            restoreLockedPlayer.SetExternalControlLocked(false);
            restoreLockedPlayer = null;
        }

        restoreControlRoutine = null;
    }

    private void RestorePlayerControlIfNeeded()
    {
        if (restoreControlRoutine != null)
        {
            StopCoroutine(restoreControlRoutine);
            restoreControlRoutine = null;
        }

        if (restoreLockedPlayer != null)
        {
            restoreLockedPlayer.SetExternalControlLocked(false);
            restoreLockedPlayer = null;
        }
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void EnsurePrompt()
    {
        if (promptText == null && autoCreatePrompt)
        {
            // 手動でTextMeshProを置かなくても、このコンポーネントだけで表示できるようにする。
            Transform existingPrompt = transform.Find("WarpPrompt");
            if (existingPrompt != null)
            {
                promptText = existingPrompt.GetComponent<TextMeshPro>();
            }

            if (promptText == null)
            {
                GameObject promptObject = new GameObject("WarpPrompt");
                promptObject.transform.SetParent(transform, false);
                promptText = promptObject.AddComponent<TextMeshPro>();
            }
        }

        ConfigurePrompt();
    }

    private void ConfigurePrompt()
    {
        if (promptText == null)
        {
            return;
        }

        if (promptFontAsset != null)
        {
            promptText.font = promptFontAsset;
        }

        promptText.text = promptMessage;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.textWrappingMode = TextWrappingModes.NoWrap;
        promptText.enableAutoSizing = false;
        promptText.overflowMode = TextOverflowModes.Overflow;
        promptText.color = GetVisibleColor(promptTextColor);
        promptText.fontSize = promptFontSize;
        promptText.sortingOrder = promptSortingOrder;
    }

    private void EnsureBackground()
    {
        if (promptBackground == null && autoCreateBackground)
        {
            Transform existingBackground = transform.Find("WarpPromptBackground");
            if (existingBackground != null)
            {
                promptBackground = existingBackground.GetComponent<SpriteRenderer>();
            }

            if (promptBackground == null)
            {
                GameObject backgroundObject = new GameObject("WarpPromptBackground");
                backgroundObject.transform.SetParent(transform, false);
                promptBackground = backgroundObject.AddComponent<SpriteRenderer>();
            }
        }

        ConfigureBackground();
    }

    private void ConfigureBackground()
    {
        if (promptBackground == null)
        {
            return;
        }

        promptBackground.sprite = GetBackgroundSprite();
        promptBackground.color = GetVisibleColor(backgroundColor);
        promptBackground.sortingOrder = backgroundSortingOrder;
    }

    private static Sprite GetBackgroundSprite()
    {
        if (sharedBackgroundSprite == null)
        {
            // 背景は1x1白スプライトを共有し、色とスケールで見た目を作る。
            sharedBackgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "WarpPromptBackgroundTexture",
                hideFlags = HideFlags.HideAndDontSave
            };
            sharedBackgroundTexture.SetPixel(0, 0, Color.white);
            sharedBackgroundTexture.Apply();

            sharedBackgroundSprite = Sprite.Create(
                sharedBackgroundTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sharedBackgroundSprite.name = "WarpPromptBackground";
            sharedBackgroundSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        return sharedBackgroundSprite;
    }

    private void ShowPrompt()
    {
        EnsurePrompt();
        EnsureBackground();
        SetPromptActive(true);
        StartPromptAnimation(1f, false);
    }

    private void HidePrompt()
    {
        StartPromptAnimation(0f, true);
    }

    private void HidePromptImmediate()
    {
        StopPromptAnimation();
        promptVisibility = 0f;
        ConfigurePrompt();
        ConfigureBackground();
        PositionPrompt();
        SetPromptActive(false);
    }

    private void StartPromptAnimation(float targetVisibility, bool deactivateWhenHidden)
    {
        // 再入場や途中退出では、今の表示率から次の状態へ滑らかにつなぐ。
        StopPromptAnimation();

        targetVisibility = Mathf.Clamp01(targetVisibility);
        if (targetVisibility > 0f || promptVisibility > 0f)
        {
            SetPromptActive(true);
        }

        ConfigurePrompt();
        ConfigureBackground();
        PositionPrompt();

        if (Mathf.Approximately(promptAnimationDuration, 0f))
        {
            promptVisibility = targetVisibility;
            ConfigurePrompt();
            ConfigureBackground();
            PositionPrompt();

            if (deactivateWhenHidden && Mathf.Approximately(targetVisibility, 0f))
            {
                SetPromptActive(false);
            }

            return;
        }

        promptAnimationRoutine = StartCoroutine(AnimatePromptVisibility(targetVisibility, deactivateWhenHidden));
    }

    private IEnumerator AnimatePromptVisibility(float targetVisibility, bool deactivateWhenHidden)
    {
        float startVisibility = promptVisibility;
        float elapsedTime = 0f;

        while (elapsedTime < promptAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / promptAnimationDuration);
            promptVisibility = Mathf.Lerp(startVisibility, targetVisibility, progress);
            ConfigurePrompt();
            ConfigureBackground();
            PositionPrompt();
            yield return null;
        }

        promptVisibility = targetVisibility;
        ConfigurePrompt();
        ConfigureBackground();
        PositionPrompt();

        if (deactivateWhenHidden && Mathf.Approximately(targetVisibility, 0f))
        {
            SetPromptActive(false);
        }

        promptAnimationRoutine = null;
    }

    private void StopPromptAnimation()
    {
        if (promptAnimationRoutine == null)
        {
            return;
        }

        StopCoroutine(promptAnimationRoutine);
        promptAnimationRoutine = null;
    }

    private void SetPromptActive(bool isActive)
    {
        if (promptText != null)
        {
            promptText.gameObject.SetActive(isActive);
        }

        if (promptBackground != null)
        {
            promptBackground.gameObject.SetActive(isActive);
        }
    }

    private Color GetVisibleColor(Color baseColor)
    {
        baseColor.a *= Mathf.Clamp01(promptVisibility);
        return baseColor;
    }

    private void PositionPrompt()
    {
        if (promptText == null)
        {
            return;
        }

        Vector3 promptPosition = transform.position + Vector3.down * promptVerticalPadding;
        if (triggerCollider != null)
        {
            Bounds bounds = triggerCollider.bounds;
            promptPosition = new Vector3(
                bounds.center.x,
                bounds.min.y - promptVerticalPadding,
                transform.position.z);
        }

        // 非表示に近いほど少し下に置き、表示に近づくほど最終位置へ戻す。
        Vector3 animationOffset = Vector3.down * (promptLiftDistanceY * (1f - Mathf.Clamp01(promptVisibility)));
        Vector3 animatedPromptPosition = promptPosition + animationOffset;
        Vector3 textPosition = animatedPromptPosition + new Vector3(promptTextOffset.x, promptTextOffset.y, 0f);
        Transform promptTransform = promptText.transform;
        promptTransform.position = textPosition;
        promptTransform.rotation = Quaternion.identity;

        Vector3 parentScale = transform.lossyScale;
        Vector3 inverseParentScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / parentScale.z);
        promptTransform.localScale = inverseParentScale;

        PositionBackground(animatedPromptPosition, inverseParentScale);
    }

    private void PositionBackground(Vector3 promptPosition, Vector3 inverseParentScale)
    {
        if (promptBackground == null || promptText == null)
        {
            return;
        }

        // 文字の実際の表示サイズに合わせて、背景の横幅と高さを毎回調整する。
        promptText.ForceMeshUpdate();
        Vector2 textSize = promptText.GetRenderedValues(false);
        if (textSize.x <= 0f || textSize.y <= 0f)
        {
            Bounds textBounds = promptText.textBounds;
            textSize = new Vector2(textBounds.size.x, textBounds.size.y);
        }

        float backgroundWidth = Mathf.Max(0.01f, textSize.x + backgroundPadding.x);
        float backgroundHeight = Mathf.Max(0.01f, textSize.y + backgroundPadding.y);

        Transform backgroundTransform = promptBackground.transform;
        backgroundTransform.position = promptPosition + new Vector3(backgroundOffset.x, backgroundOffset.y, 0f);
        backgroundTransform.rotation = Quaternion.identity;
        backgroundTransform.localScale = new Vector3(
            backgroundWidth * inverseParentScale.x,
            backgroundHeight * inverseParentScale.y,
            inverseParentScale.z);
    }
}
