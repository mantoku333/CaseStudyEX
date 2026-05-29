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

    [SerializeField] private Canvas canvas;
    [SerializeField] private Image cursorImage;
    [SerializeField] private Image reticleImage;
    [SerializeField] private Image reloadImage;
    [SerializeField] private Vector2 cursorHotspotOffset = Vector2.zero;
    [SerializeField] private Vector2 reticleSize = new Vector2(64.0f, 64.0f);
    [SerializeField] private Vector2 reloadSize = new Vector2(64.0f, 64.0f);
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
        SetSystemCursorVisible(false);
        HideAllImages();
    }

    private void OnEnable()
    {
        SetSystemCursorVisible(false);
    }

    private void Update()
    {
        SetSystemCursorVisible(false);
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

        if (hasGunAbility &&
            umbrellaOpen &&
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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            SetSystemCursorVisible(false);
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
        ConfigureImage(cursorImage, Vector2.zero, ResolveCursorSize(), new Vector2(0.0f, 1.0f));
        ConfigureImage(reticleImage, Vector2.zero, reticleSize, new Vector2(0.5f, 0.5f));
        ConfigureImage(reloadImage, Vector2.zero, reloadSize, new Vector2(0.5f, 0.5f));

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

        if (!PlayerHasGunAbility(player))
        {
            worldPosition = Vector3.zero;
            return false;
        }

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
        MoveImage(cursorImage, screenPosition, cursorHotspotOffset);
    }

    private void ShowReticle(Vector2 screenPosition)
    {
        SetImageVisible(cursorImage, false);
        SetImageVisible(reticleImage, true);
        SetImageVisible(reloadImage, false);
        MoveImage(reticleImage, screenPosition, Vector2.zero);
    }

    private void ShowReload(Vector2 screenPosition)
    {
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
        SetImageVisible(cursorImage, false);
        SetImageVisible(reticleImage, false);
        SetImageVisible(reloadImage, false);
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
}
