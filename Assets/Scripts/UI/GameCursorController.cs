using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class GameCursorController : MonoBehaviour
{
    private const string ResourcePrefabPath = "UI/GameCursorCanvas";
    private const string SettingsResourcePath = "UI/GameCursorBootstrapSettings";
    private const string EditorPrefabPath = "Assets/Prefabs/UI/GameCursorCanvas.prefab";
    private const string CursorImageName = "CursorImage";
    private const string ReticleImageName = "ReticleImage";
    private const string ReloadImageName = "ReloadImage";
    private const int ReloadCircleTextureSize = 128;

    private static GameCursorController instance;
    private static readonly HashSet<object> menuCursorModeOwners = new HashSet<object>();

    [SerializeField] private Canvas canvas;
    [SerializeField] private Image cursorImage;
    [SerializeField] private Image reticleImage;
    [SerializeField] private Image reloadImage;

    // 通常カーソルの見た目と位置調整。Prefab 側でサイズやホットスポットを調整できる。
    [SerializeField] private Vector2 cursorHotspotOffset = Vector2.zero;
    [SerializeField] private Vector2 cursorSize = new Vector2(20.0f, 36.0f);

    // 通常カーソル専用のまばたき設定。Reticle や Reload 表示中は使わない。
    [SerializeField] private Sprite cursorOpenSprite;
    [SerializeField] private Sprite cursorClosedSprite;
    [SerializeField] private string cursorBlinkPattern = "010000000010100000";
    [SerializeField, Min(0.0f)] private float cursorBlinkMinIntervalSeconds = 30.0f;
    [SerializeField, Min(0.0f)] private float cursorBlinkMaxIntervalSeconds = 180.0f;
    [SerializeField, Min(0.01f)] private float cursorBlinkStepSeconds = 0.08f;

    // 銃アビリティ取得後、傘を開いている時だけ使う Reticle / Reload 表示設定。
    [SerializeField] private Vector2 reticleSize = new Vector2(64.0f, 64.0f);
    [SerializeField] private Vector2 reloadSize = new Vector2(64.0f, 64.0f);
    [SerializeField] private bool clampReticleToPlayerRadius = true;
    [SerializeField, Min(0.0f)] private float reticleWorldRadius = 1.5f;
    [SerializeField] private Color reloadStartColor = Color.red;
    [SerializeField] private Color reloadMiddleColor = new Color(1.0f, 0.45f, 0.0f, 1.0f);
    [SerializeField] private Color reloadCompleteColor = Color.white;
    [SerializeField, Range(0.01f, 0.99f)] private float orangeAtCompletionRatio = 0.25f;

    private PlayerController activePlayer;
    private UmbrellaController activeUmbrella;
    private GunController activeGun;
    private Player.PlayerAbilityController activeAbility;
    private Sprite reloadCircleSprite;
    private Texture2D reloadCircleTexture;
    private float cursorBlinkTimer;
    private float cursorBlinkDelaySeconds;
    private int cursorBlinkPatternIndex;
    private bool cursorBlinkPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static bool TryGetClampedAimWorldPosition(
        Transform player,
        Camera camera,
        out Vector3 worldPosition)
    {
        GameCursorController controller = EnsureInstance();
        if (controller == null ||
            player == null ||
            camera == null ||
            !controller.clampReticleToPlayerRadius ||
            !controller.TryGetPointerScreenPosition(out Vector2 screenPosition))
        {
            worldPosition = Vector3.zero;
            return false;
        }

        return controller.TryGetClampedAimWorldPosition(
            player,
            camera,
            screenPosition,
            out worldPosition);
    }

    public static void SetMenuCursorModeActive(object owner, bool active)
    {
        if (owner == null)
        {
            return;
        }

        if (active)
        {
            menuCursorModeOwners.Add(owner);
            return;
        }

        menuCursorModeOwners.Remove(owner);
    }

    private static GameCursorController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameCursorController existing =
            FindFirstObjectByType<GameCursorController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject prefab = LoadCursorPrefab();
        if (prefab != null)
        {
            GameObject cursorObject = Instantiate(prefab);
            cursorObject.name = nameof(GameCursorController);
            instance = cursorObject.GetComponent<GameCursorController>();
            return instance;
        }

        GameObject fallbackObject = new GameObject(nameof(GameCursorController));
        instance = fallbackObject.AddComponent<GameCursorController>();
        return instance;
    }

    private static GameObject LoadCursorPrefab()
    {
        GameCursorBootstrapSettings settings =
            Resources.Load<GameCursorBootstrapSettings>(SettingsResourcePath);
        if (settings != null && settings.CursorPrefab != null)
        {
            return settings.CursorPrefab;
        }

#if UNITY_EDITOR
        GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefabPath);
        if (editorPrefab != null)
        {
            return editorPrefab;
        }
#endif

        return Resources.Load<GameObject>(ResourcePrefabPath);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveReferences();
        ConfigureCanvas();
        ConfigureImages();
        ResetCursorBlink();
        ApplySystemCursorVisibility();
        HideAllImages();
    }

    private void OnEnable()
    {
        ResetCursorBlink();
        ApplySystemCursorVisibility();
    }

    private void Update()
    {
        SetSystemCursorVisible(false);

        if (HideCursorForPvMode())
        {
            return;
        }

        ResolvePlayerReferences();

        if (!TryGetPointerScreenPosition(out Vector2 rawScreenPosition))
        {
            HideAllImages();
            return;
        }

        bool umbrellaOpen =
            activeUmbrella != null &&
            activeUmbrella.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open;
        bool hasGunAbility = HasGunAbility();
        bool reloading =
            hasGunAbility &&
            umbrellaOpen &&
            activeGun != null &&
            activeGun.IsReloading;
        Vector2 displayPosition = rawScreenPosition;

        if (menuCursorModeOwners.Count > 0)
        {
            ShowCursor(rawScreenPosition);
            return;
        }

        // Reticle の制限は「銃アビリティあり + 傘オープン」の時だけ。通常カーソルは制限しない。
        if (hasGunAbility &&
            umbrellaOpen &&
            clampReticleToPlayerRadius &&
            activePlayer != null &&
            Camera.main != null &&
            TryGetClampedAimWorldPosition(
                activePlayer.transform,
                Camera.main,
                rawScreenPosition,
                out Vector3 clampedWorldPosition))
        {
            displayPosition = Camera.main.WorldToScreenPoint(clampedWorldPosition);
        }

        // Reload 中は Reticle を隠し、同じ制限済み位置に Reload 円を出す。
        if (reloading)
        {
            ShowReload(displayPosition);
            return;
        }

        if (hasGunAbility && umbrellaOpen && activePlayer != null)
        {
            ShowReticle(displayPosition);
            return;
        }

        ShowCursor(rawScreenPosition);
    }

    private bool HideCursorForPvMode()
    {
        if (!PvModeState.IsActive)
        {
            return false;
        }

        HideAllImages();
        return true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplySystemCursorVisibility();
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;
        SetSystemCursorVisible(true);

        if (reloadCircleSprite != null)
        {
            Destroy(reloadCircleSprite);
        }

        if (reloadCircleTexture != null)
        {
            Destroy(reloadCircleTexture);
        }
    }

    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }

        cursorImage = ResolveImage(cursorImage, CursorImageName);
        reticleImage = ResolveImage(reticleImage, ReticleImageName);
        reloadImage = ResolveImage(reloadImage, ReloadImageName);
    }

    private Image ResolveImage(Image current, string imageName)
    {
        if (current != null)
        {
            return current;
        }

        Transform existing = transform.Find(imageName);
        if (existing != null && existing.TryGetComponent(out Image existingImage))
        {
            return existingImage;
        }

        GameObject imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        return imageObject.GetComponent<Image>();
    }

    private void ConfigureCanvas()
    {
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
    }

    private void ConfigureImages()
    {
        // 開き目スプライトが未設定なら、現在の CursorImage をフォールバックとして使う。
        if (cursorOpenSprite == null && cursorImage != null)
        {
            cursorOpenSprite = cursorImage.sprite;
        }

        ConfigureImage(cursorImage, Vector2.zero, ResolveCursorSize(), new Vector2(0.0f, 1.0f));
        ConfigureImage(reticleImage, Vector2.zero, reticleSize, new Vector2(0.5f, 0.5f));
        ConfigureImage(reloadImage, Vector2.zero, reloadSize, new Vector2(0.5f, 0.5f));
        SetCursorOpenSprite();

        if (reloadImage != null)
        {
            reloadImage.sprite = GetReloadCircleSprite();
            reloadImage.type = Image.Type.Filled;
            reloadImage.fillMethod = Image.FillMethod.Radial360;
            reloadImage.fillOrigin = (int)Image.Origin360.Top;
            reloadImage.fillClockwise = false;
            reloadImage.fillAmount = 1.0f;
        }
    }

    private void ConfigureImage(Image image, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = false;
        image.preserveAspect = true;

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private Vector2 ResolveCursorSize()
    {
        if (cursorSize.x > 0.0f && cursorSize.y > 0.0f)
        {
            return cursorSize;
        }

        if (cursorImage != null && cursorImage.sprite != null)
        {
            Rect spriteRect = cursorImage.sprite.rect;
            return new Vector2(spriteRect.width, spriteRect.height);
        }

        return new Vector2(32.0f, 32.0f);
    }

    private void ResolvePlayerReferences()
    {
        if (activePlayer == null || !activePlayer.isActiveAndEnabled)
        {
            activePlayer = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
            activeGun = null;
            activeUmbrella = null;
            activeAbility = null;
        }

        if (activePlayer == null)
        {
            activeGun = null;
            activeUmbrella = null;
            activeAbility = null;
            return;
        }

        if (activeGun == null)
        {
            activeGun = activePlayer.GetComponentInChildren<GunController>(true);
        }

        if (activeUmbrella == null)
        {
            activeUmbrella = activePlayer.GetComponentInChildren<UmbrellaController>(true);
        }

        if (activeAbility == null)
        {
            activeAbility = activePlayer.GetComponent<Player.PlayerAbilityController>();
        }
    }

    private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
    {
        if (Pointer.current != null)
        {
            screenPosition = Pointer.current.position.ReadValue();
            return true;
        }

        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private bool TryGetClampedAimWorldPosition(
        Transform player,
        Camera camera,
        Vector2 screenPosition,
        out Vector3 worldPosition)
    {
        if (player == null || camera == null)
        {
            worldPosition = Vector3.zero;
            return false;
        }

        if (!clampReticleToPlayerRadius)
        {
            worldPosition = Vector3.zero;
            return false;
        }

        if (!PlayerHasGunAbility(player))
        {
            worldPosition = Vector3.zero;
            return false;
        }

        // ポインターをプレイヤー平面のワールド座標に変換し、プレイヤー中心の円内へ丸める。
        Vector3 pointerWorldPosition = ScreenToPlayerPlaneWorld(
            camera,
            screenPosition,
            player.position.z);
        Vector2 offset = pointerWorldPosition - player.position;
        float maxRadius = Mathf.Max(0.0f, reticleWorldRadius);

        if (maxRadius > 0.0f && offset.sqrMagnitude > maxRadius * maxRadius)
        {
            offset = offset.normalized * maxRadius;
        }

        worldPosition = new Vector3(
            player.position.x + offset.x,
            player.position.y + offset.y,
            player.position.z);
        return true;
    }

    private static Vector3 ScreenToPlayerPlaneWorld(
        Camera camera,
        Vector2 screenPosition,
        float playerZ)
    {
        float depth = camera.orthographic
            ? Mathf.Abs(camera.transform.position.z - playerZ)
            : Vector3.Dot(
                new Vector3(0.0f, 0.0f, playerZ) - camera.transform.position,
                camera.transform.forward);

        if (depth <= 0.0f)
        {
            depth = Mathf.Abs(camera.transform.position.z - playerZ);
        }

        Vector3 worldPosition = camera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition.z = playerZ;
        return worldPosition;
    }

    private bool HasGunAbility()
    {
        return activeAbility != null && activeAbility.GetCanGunRecoil();
    }

    private static bool PlayerHasGunAbility(Transform player)
    {
        if (player == null)
        {
            return false;
        }

        Player.PlayerAbilityController ability =
            player.GetComponent<Player.PlayerAbilityController>();
        if (ability == null)
        {
            ability = player.GetComponentInParent<Player.PlayerAbilityController>();
        }

        return ability != null && ability.GetCanGunRecoil();
    }

    private void ShowCursor(Vector2 screenPosition)
    {
        SetImageVisible(cursorImage, true);
        SetImageVisible(reticleImage, false);
        SetImageVisible(reloadImage, false);
        UpdateCursorBlink();
        MoveImage(cursorImage, screenPosition, cursorHotspotOffset);
    }

    private void ShowReticle(Vector2 screenPosition)
    {
        // Reticle 表示中は通常カーソルのまばたきを止め、戻った時は開き目から始める。
        ResetCursorBlink();
        SetImageVisible(cursorImage, false);
        SetImageVisible(reticleImage, true);
        SetImageVisible(reloadImage, false);
        MoveImage(reticleImage, screenPosition, Vector2.zero);
    }

    private void ShowReload(Vector2 screenPosition)
    {
        // Reload 表示中も通常カーソルのまばたきは止めておく。
        ResetCursorBlink();
        SetImageVisible(cursorImage, false);
        SetImageVisible(reticleImage, false);
        SetImageVisible(reloadImage, true);
        MoveImage(reloadImage, screenPosition, Vector2.zero);

        if (reloadImage == null || activeGun == null)
        {
            return;
        }

        float remainingRatio = Mathf.Clamp01(activeGun.ReloadRemainingRatio);
        reloadImage.fillAmount = remainingRatio;
        reloadImage.color = EvaluateReloadColor(1.0f - remainingRatio);
    }

    private void MoveImage(Image image, Vector2 screenPosition, Vector2 localOffset)
    {
        if (image == null)
        {
            return;
        }

        image.rectTransform.anchoredPosition =
            ScreenToCanvasPosition(screenPosition) + localOffset;
    }

    private Vector2 ScreenToCanvasPosition(Vector2 screenPosition)
    {
        float scaleFactor =
            canvas != null
                ? Mathf.Max(0.0001f, canvas.scaleFactor)
                : 1.0f;
        return screenPosition / scaleFactor;
    }

    private void HideAllImages()
    {
        ResetCursorBlink();
        SetImageVisible(cursorImage, false);
        SetImageVisible(reticleImage, false);
        SetImageVisible(reloadImage, false);
    }

    private void UpdateCursorBlink()
    {
        if (cursorImage == null)
        {
            return;
        }

        if (cursorOpenSprite == null)
        {
            cursorOpenSprite = cursorImage.sprite;
        }

        // 閉じ目スプライトがない場合は、古い通常カーソルとして静止表示にする。
        if (cursorClosedSprite == null || string.IsNullOrEmpty(cursorBlinkPattern))
        {
            SetCursorOpenSprite();
            return;
        }

        float stepSeconds = Mathf.Max(0.01f, cursorBlinkStepSeconds);

        if (!cursorBlinkPlaying)
        {
            cursorBlinkTimer += Time.unscaledDeltaTime;
            SetCursorOpenSprite();

            if (cursorBlinkTimer < cursorBlinkDelaySeconds)
            {
                return;
            }

            cursorBlinkPlaying = true;
            cursorBlinkTimer = 0.0f;
            cursorBlinkPatternIndex = 0;
        }

        // パターン文字の 0 は開き目、1 は閉じ目として 1 ステップずつ再生する。
        ApplyCursorBlinkPatternFrame();
        cursorBlinkTimer += Time.unscaledDeltaTime;

        while (cursorBlinkTimer >= stepSeconds && cursorBlinkPlaying)
        {
            cursorBlinkTimer -= stepSeconds;
            cursorBlinkPatternIndex++;

            if (cursorBlinkPatternIndex >= cursorBlinkPattern.Length)
            {
                ResetCursorBlink();
                return;
            }

            ApplyCursorBlinkPatternFrame();
        }
    }

    private void ApplyCursorBlinkPatternFrame()
    {
        if (cursorImage == null || cursorBlinkPatternIndex >= cursorBlinkPattern.Length)
        {
            return;
        }

        cursorImage.sprite =
            cursorBlinkPattern[cursorBlinkPatternIndex] == '1'
                ? cursorClosedSprite
                : cursorOpenSprite;
    }

    private void ResetCursorBlink()
    {
        cursorBlinkTimer = 0.0f;
        // まばたきが終わるたびに次の待ち時間をランダムに取り直す。
        cursorBlinkDelaySeconds = GetRandomCursorBlinkDelaySeconds();
        cursorBlinkPatternIndex = 0;
        cursorBlinkPlaying = false;
        SetCursorOpenSprite();
    }

    private float GetRandomCursorBlinkDelaySeconds()
    {
        float minSeconds = Mathf.Max(0.0f, cursorBlinkMinIntervalSeconds);
        float maxSeconds = Mathf.Max(0.0f, cursorBlinkMaxIntervalSeconds);

        if (maxSeconds < minSeconds)
        {
            float tempSeconds = minSeconds;
            minSeconds = maxSeconds;
            maxSeconds = tempSeconds;
        }

        if (Mathf.Approximately(minSeconds, maxSeconds))
        {
            return minSeconds;
        }

        return Random.Range(minSeconds, maxSeconds);
    }

    private void SetCursorOpenSprite()
    {
        if (cursorImage != null && cursorOpenSprite != null && cursorImage.sprite != cursorOpenSprite)
        {
            cursorImage.sprite = cursorOpenSprite;
        }
    }

    private static void SetImageVisible(Image image, bool visible)
    {
        if (image != null && image.enabled != visible)
        {
            image.enabled = visible;
        }
    }

    private Color EvaluateReloadColor(float completionRatio)
    {
        completionRatio = Mathf.Clamp01(completionRatio);

        if (completionRatio <= orangeAtCompletionRatio)
        {
            float t = completionRatio / orangeAtCompletionRatio;
            return Color.Lerp(reloadStartColor, reloadMiddleColor, t);
        }

        float whiteT =
            (completionRatio - orangeAtCompletionRatio) /
            (1.0f - orangeAtCompletionRatio);
        return Color.Lerp(reloadMiddleColor, reloadCompleteColor, whiteT);
    }

    private Sprite GetReloadCircleSprite()
    {
        if (reloadCircleSprite != null)
        {
            return reloadCircleSprite;
        }

        reloadCircleTexture = new Texture2D(
            ReloadCircleTextureSize,
            ReloadCircleTextureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "GameCursorReloadCircle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        Vector2 center = new Vector2(
            (ReloadCircleTextureSize - 1) * 0.5f,
            (ReloadCircleTextureSize - 1) * 0.5f);
        float radius = (ReloadCircleTextureSize - 1) * 0.5f;

        for (int y = 0; y < ReloadCircleTextureSize; y++)
        {
            for (int x = 0; x < ReloadCircleTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                reloadCircleTexture.SetPixel(
                    x,
                    y,
                    alpha > 0.0f ? new Color(1.0f, 1.0f, 1.0f, alpha) : clear);
            }
        }

        reloadCircleTexture.Apply();
        reloadCircleSprite = Sprite.Create(
            reloadCircleTexture,
            new Rect(0.0f, 0.0f, ReloadCircleTextureSize, ReloadCircleTextureSize),
            new Vector2(0.5f, 0.5f),
            ReloadCircleTextureSize);
        reloadCircleSprite.name = "GameCursorReloadCircle";
        return reloadCircleSprite;
    }

    private static void SetSystemCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = CursorLockMode.None;
    }

    private static void ApplySystemCursorVisibility()
    {
        SetSystemCursorVisible(false);
    }
}
