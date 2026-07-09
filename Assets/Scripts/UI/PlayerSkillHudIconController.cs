using Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameName.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkillHudIconController : MonoBehaviour
    {
        private const string DownAttackIconName = "SkillIcon_DownAttack";
        private const string DodgeIconName = "SkillIcon_Dodge";

        [SerializeField] private Transform downAttackIcon;
        [SerializeField] private Transform dodgeIcon;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private Color readyColor = Color.white;
        [SerializeField] private Color unavailableColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [SerializeField] private Color cooldownOverlayColor = new Color(0f, 0f, 0f, 0.58f);
        [SerializeField, Min(0)] private int overlaySortingOrderOffset = 1;

        private PlayerAbilityController abilityController;
        private DodgeController dodgeController;
        private PlayerDiveAttackController diveAttackController;
        private SkillIconView downAttackView;
        private SkillIconView dodgeView;

        private void Awake()
        {
            ResolveIconReferences();
            ResolvePlayerReferences();
        }

        private void OnValidate()
        {
            overlaySortingOrderOffset = Mathf.Max(0, overlaySortingOrderOffset);
        }

        private void Update()
        {
            ResolveIconReferences();
            ResolvePlayerReferences();

            bool hasDownAttack = GameProgressFlags.Get(GameProgressKeys.AbilityDiveAttackUnlocked) ||
                (abilityController != null && abilityController.GetCanDiveAttack());
            bool hasDodge = GameProgressFlags.Get(GameProgressKeys.AbilityDodgeUnlocked) ||
                (abilityController != null && abilityController.GetCanDodge());

            Color downAttackColor = hasDownAttack ? GetDownAttackIconColor() : unavailableColor;
            downAttackView.Update(hasDownAttack, downAttackColor, cooldownOverlayColor, 0f, overlaySortingOrderOffset);
            dodgeView.Update(hasDodge, readyColor, cooldownOverlayColor, GetDodgeOverlayAmount(), overlaySortingOrderOffset);
        }

        private float GetDodgeOverlayAmount()
        {
            return dodgeController != null ? dodgeController.GetDodgeCooldownRemaining01() : 0f;
        }

        private Color GetDownAttackIconColor()
        {
            if (diveAttackController == null)
            {
                return unavailableColor;
            }

            return diveAttackController.CanStartDiveAttackFromAir() ? readyColor : unavailableColor;
        }

        private void ResolveIconReferences()
        {
            if (downAttackIcon == null)
            {
                downAttackIcon = FindDeepChild(transform, DownAttackIconName);
            }

            if (dodgeIcon == null)
            {
                dodgeIcon = FindDeepChild(transform, DodgeIconName);
            }

            downAttackView.Resolve(downAttackIcon);
            dodgeView.Resolve(dodgeIcon);
        }

        private void ResolvePlayerReferences()
        {
            if (!autoFindPlayer || (abilityController != null && dodgeController != null && diveAttackController != null))
            {
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject == null)
            {
                return;
            }

            if (abilityController == null)
            {
                abilityController = playerObject.GetComponent<PlayerAbilityController>();
            }

            if (dodgeController == null)
            {
                dodgeController = playerObject.GetComponent<DodgeController>();
            }

            if (diveAttackController == null)
            {
                diveAttackController = playerObject.GetComponent<PlayerDiveAttackController>();
            }
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform result = FindDeepChild(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private struct SkillIconView
        {
            private Transform root;
            private SpriteRenderer sourceRenderer;
            private RectTransform iconRect;
            private Image iconImage;
            private Image overlayImage;

            public void Resolve(Transform iconRoot)
            {
                if (root == iconRoot && iconImage != null)
                {
                    return;
                }

                root = iconRoot;
                sourceRenderer = root != null ? root.GetComponent<SpriteRenderer>() : null;
                iconRect = null;
                iconImage = null;
                overlayImage = null;
            }

            public void Update(bool isVisible, Color iconColor, Color overlayColor, float overlayAmount, int overlaySortingOrderOffset)
            {
                EnsureUiImage();
                if (iconImage == null)
                {
                    return;
                }

                if (sourceRenderer != null)
                {
                    sourceRenderer.enabled = false;
                }

                iconImage.enabled = isVisible;
                iconImage.color = iconColor;

                if (overlayImage == null)
                {
                    return;
                }

                overlayAmount = Mathf.Clamp01(overlayAmount);
                overlayImage.enabled = isVisible && overlayAmount > 0f;
                overlayImage.color = overlayColor;
                overlayImage.fillAmount = overlayAmount;
            }

            private void EnsureUiImage()
            {
                if (iconImage != null || root == null || sourceRenderer == null || sourceRenderer.sprite == null)
                {
                    return;
                }

                Transform parent = root.parent != null ? root.parent : root;
                Transform existingImage = parent.Find(root.name + "_UIImage");
                GameObject imageObject = existingImage != null
                    ? existingImage.gameObject
                    : new GameObject(root.name + "_UIImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageObject.transform.SetParent(parent, false);
                imageObject.layer = root.gameObject.layer;

                iconRect = imageObject.transform as RectTransform;
                iconImage = imageObject.GetComponent<Image>();
                iconImage.raycastTarget = false;
                iconImage.sprite = sourceRenderer.sprite;
                iconImage.preserveAspect = true;

                Vector3 sourcePosition = root.localPosition;
                Vector3 sourceScale = root.localScale;
                Bounds spriteBounds = sourceRenderer.sprite.bounds;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(sourcePosition.x, sourcePosition.y);
                iconRect.localRotation = Quaternion.identity;
                iconRect.localScale = Vector3.one;
                iconRect.sizeDelta = new Vector2(
                    Mathf.Abs(spriteBounds.size.x * sourceScale.x),
                    Mathf.Abs(spriteBounds.size.y * sourceScale.y));
                iconRect.SetAsLastSibling();

                GameObject overlayObject = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlayObject.transform.SetParent(iconRect, false);
                overlayObject.layer = imageObject.layer;

                RectTransform overlayRect = overlayObject.transform as RectTransform;
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;

                overlayImage = overlayObject.GetComponent<Image>();
                overlayImage.raycastTarget = false;
                overlayImage.sprite = sourceRenderer.sprite;
                overlayImage.type = Image.Type.Filled;
                overlayImage.fillMethod = Image.FillMethod.Vertical;
                overlayImage.fillOrigin = (int)Image.OriginVertical.Top;
                overlayImage.fillClockwise = true;
                overlayImage.preserveAspect = true;
                overlayImage.enabled = false;
            }
        }
    }
}
