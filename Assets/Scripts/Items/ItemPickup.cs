using Metroidvania.Data;
using System.Collections;
using Player;
using UnityEngine;
using UnityEngine.UI;

public class ItemPickup : MonoBehaviour, ISaveDataModule
{
    //--------------アイテムデータ関連------------------
    [SerializeField] private ItemData itemData;

    [Header("Debug")]
    [SerializeField] private bool equipOnPickupForDebug;

    [Header("Equipment Pickup Notification")]
    [SerializeField] private Sprite equipmentPickupNotificationSprite;
    [SerializeField] private Vector2 equipmentPickupNotificationSize = new Vector2(512f, 130f);
    [SerializeField] private Vector2 equipmentPickupNotificationBottomLeftOffset = new Vector2(32f, 32f);
    [SerializeField, Min(0f)] private float equipmentPickupNotificationSlideInDuration = 0.45f;
    [SerializeField, Min(0f)] private float equipmentPickupNotificationHoldSeconds = 1.2f;
    [SerializeField, Min(0f)] private float equipmentPickupNotificationSlideOutDuration = 0.35f;

    //--------------状態関連------------------
    private bool isPickedUp = false;

    public int Priority => 250;

    private void OnEnable()
    {
        SaveManager.RegisterModule(this);
    }

    private void OnDisable()
    {
        SaveManager.UnregisterModule(this);
    }

    private void Start()
    {
        BoxCollider2D collider2D = GetComponent<BoxCollider2D>();

        if (collider2D == null)
        {
            Debug.LogError("BoxCollider2Dが付いていません", this);
            return;
        }

        if (!collider2D.isTrigger)
        {
            Debug.LogWarning("BoxCollider2DがTriggerになっていません", this);
        }

        if (IsAlreadyPickedUp())
        {
            Destroy(gameObject);
        }
    }

    private void Reset()
    {
        BoxCollider2D collider2D = GetComponent<BoxCollider2D>();

        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")){ return; }

        Debug.Log($"GunAbilityItemに触れた: {other.name}", this);

        if (isPickedUp) { return; }

        if (itemData == null)
        {
            Debug.LogWarning("ItemDataが設定されていません", this);
            return;
        }

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        }

        PlayerAbilityController abilityController = other.GetComponent<PlayerAbilityController>();

        if (abilityController == null)
        {
            abilityController = other.GetComponentInParent<PlayerAbilityController>();
        }

        bool isApplied = ApplyItem(playerHealth, abilityController);

        if (!isApplied) { return; }

        if (IsSaveTargetItem())
        {
            GameProgressFlags.Set(itemData.itemId, true);
        }

        Debug.Log($"{itemData.itemName} を取得しました！");
        PlayEquipmentPickupNotification();

        CompletePickup(playerHealth);
    }

    private bool ApplyItem(PlayerHealth playerHealth, PlayerAbilityController abilityController)
    {
        bool isApplied = false;

        if (itemData.healAmount > 0)
        {
            if (playerHealth == null) { return false; }

            playerHealth.Heal(itemData.healAmount);
            isApplied = true;
        }

        if (itemData.maxHealthBonus > 0)
        {
            if (playerHealth == null) { return false; }

            playerHealth.AddMaxHealth(itemData.maxHealthBonus, true);
            isApplied = true;
        }

        if (itemData.abilityType != PlayerAbilityType.None)
        {
            if (abilityController == null) { return false; }

            UnlockAbility(abilityController);
            isApplied = true;
        }

        if (!isApplied && IsInventoryItem())
        {
            if (itemData.itemType == ItemType.Equipment)
            {
                GameItems.SetCount(itemData.itemId, 1);

                if (equipOnPickupForDebug)
                {
                    PlayerEquipmentState.Equip(itemData);
                }
            }
            else
            {
                GameItems.AddCount(itemData.itemId, 1);
            }

            isApplied = true;
        }

        return isApplied;
    }

    private void UnlockAbility(PlayerAbilityController abilityController)
    {
        if (itemData.abilityType == PlayerAbilityType.Dodge)
        {
            abilityController.SetCanDodge(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, true);
            return;
        }

        if (itemData.abilityType == PlayerAbilityType.Glide)
        {
            abilityController.SetCanGlide(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, true);
            return;
        }

        if (itemData.abilityType == PlayerAbilityType.GunRecoil)
        {
            abilityController.SetCanGunRecoil(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, true);
            return;
        }

        if (itemData.abilityType == PlayerAbilityType.Parry)
        {
            abilityController.SetCanParry(true);
            return;
        }
    }

    private bool IsAlreadyPickedUp()
    {
        if (itemData == null)
        {
            return false;
        }

        if (!IsSaveTargetItem())
        {
            return false;
        }

        return GameProgressFlags.Get(itemData.itemId);
    }

    private bool IsSaveTargetItem()
    {
        if (itemData == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(itemData.itemId))
        {
            return false;
        }

        if (itemData.abilityType != PlayerAbilityType.None)
        {
            return true;
        }

        if (itemData.maxHealthBonus > 0)
        {
            return true;
        }

        if (IsInventoryItem())
        {
            return true;
        }

        return false;
    }

    private bool IsInventoryItem()
    {
        if (itemData == null || string.IsNullOrWhiteSpace(itemData.itemId))
        {
            return false;
        }

        return itemData.itemType == ItemType.KeyItem ||
               itemData.itemType == ItemType.Equipment ||
               itemData.itemType == ItemType.Collectible;
    }

    private void CompletePickup(PlayerHealth playerHealth)
    {
        isPickedUp = true;

        ItemEffectController effectController = GetComponent<ItemEffectController>();
        if (effectController != null)
        {
            if (itemData != null && itemData.healAmount > 0)
            {
                effectController.PlayHealEffectOnPlayer(playerHealth);
            }

            if (effectController.PlayPickupEffectAndDestroy())
            {
                return;
            }
        }

        Destroy(gameObject);
    }

    private void PlayEquipmentPickupNotification()
    {
        if (itemData == null || itemData.itemType != ItemType.Equipment || equipmentPickupNotificationSprite == null)
        {
            return;
        }

        EquipmentPickupNotificationPlayer.Play(
            equipmentPickupNotificationSprite,
            equipmentPickupNotificationSize,
            equipmentPickupNotificationBottomLeftOffset,
            equipmentPickupNotificationSlideInDuration,
            equipmentPickupNotificationHoldSeconds,
            equipmentPickupNotificationSlideOutDuration);
    }

    public void Capture(SaveGameData saveData)
    {
    }

    public void Restore(SaveGameData saveData)
    {
        if (IsAlreadyPickedUp())
        {
            Destroy(gameObject);
        }
    }

    private sealed class EquipmentPickupNotificationPlayer : MonoBehaviour
    {
        private Sprite notificationSprite;
        private Vector2 notificationSize;
        private Vector2 bottomLeftOffset;
        private float slideInDuration;
        private float holdSeconds;
        private float slideOutDuration;
        private RectTransform notificationRect;
        private CanvasGroup canvasGroup;
        private Image notificationImage;

        public static void Play(
            Sprite sprite,
            Vector2 size,
            Vector2 offset,
            float slideIn,
            float hold,
            float slideOut)
        {
            if (sprite == null)
            {
                return;
            }

            GameObject playerObject = new GameObject("EquipmentPickupNotification");
            EquipmentPickupNotificationPlayer player = playerObject.AddComponent<EquipmentPickupNotificationPlayer>();
            player.notificationSprite = sprite;
            player.notificationSize = size;
            player.bottomLeftOffset = offset;
            player.slideInDuration = slideIn;
            player.holdSeconds = hold;
            player.slideOutDuration = slideOut;
            player.StartCoroutine(player.PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            EnsureNotification();

            Vector2 shownPosition = bottomLeftOffset;
            Vector2 hiddenPosition = new Vector2(
                -Mathf.Max(1f, notificationSize.x) - 32f,
                shownPosition.y);

            canvasGroup.alpha = 1f;
            notificationRect.anchoredPosition = hiddenPosition;
            notificationImage.enabled = true;

            yield return MoveNotification(hiddenPosition, shownPosition, slideInDuration);

            if (holdSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(holdSeconds);
            }

            yield return MoveNotification(shownPosition, hiddenPosition, slideOutDuration);
            Destroy(gameObject);
        }

        private void EnsureNotification()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject imageObject = new GameObject("EquipmentPickupNotificationImage");
            imageObject.transform.SetParent(transform, false);

            notificationRect = imageObject.AddComponent<RectTransform>();
            notificationRect.anchorMin = Vector2.zero;
            notificationRect.anchorMax = Vector2.zero;
            notificationRect.pivot = Vector2.zero;
            notificationRect.sizeDelta = notificationSize;

            notificationImage = imageObject.AddComponent<Image>();
            notificationImage.raycastTarget = false;
            notificationImage.preserveAspect = true;
            notificationImage.sprite = notificationSprite;
        }

        private IEnumerator MoveNotification(Vector2 from, Vector2 to, float duration)
        {
            if (duration <= 0f)
            {
                notificationRect.anchoredPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                notificationRect.anchoredPosition = Vector2.LerpUnclamped(from, to, SmootherStep(t));
                yield return null;
            }

            notificationRect.anchoredPosition = to;
        }

        private static float SmootherStep(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
