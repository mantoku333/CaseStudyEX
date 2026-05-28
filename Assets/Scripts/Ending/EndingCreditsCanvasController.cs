using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameName.Ending
{
    [DisallowMultipleComponent]
    public sealed class EndingCreditsCanvasController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image endingImage;
        [SerializeField] private TextMeshProUGUI creditsText;

        [Header("Images")]
        [SerializeField] private Sprite imageSprite;
        [SerializeField] private Sprite[] imageSprites;
        [SerializeField, Min(0f)] private float imageFadeDuration = 3f;
        [SerializeField, Min(0.1f)] private float imageHoldSeconds = 4f;
        [SerializeField, Min(0f)] private float imageCrossfadeSeconds = 1f;
        [SerializeField] private bool loopImages = true;

        [Header("Text")]
        [SerializeField] private bool useCreditLines;
        [SerializeField] private string[] creditLines =
        {
            "制作",
            "",
            "企画",
            "〇〇 〇〇",
            "",
            "プログラム",
            "〇〇 〇〇",
            "",
            "アート",
            "〇〇 〇〇",
            "",
            "サウンド",
            "〇〇 〇〇",
            "",
            "Thank you for playing"
        };

        [Header("Motion")]
        [SerializeField, Min(0f)] private float canvasFadeInDuration;
        [SerializeField, Min(1f)] private float scrollSpeed = 55f;
        [SerializeField, Min(0f)] private float endPadding = 320f;

        private Coroutine imageRotationRoutine;

        private void Awake()
        {
            AutoBind();
            SetImmediateHidden();
        }

        private void OnDisable()
        {
            StopImageRotation();
        }

        public void SetImmediateHidden()
        {
            AutoBind();
            SetCanvasAlpha(0f);
            SetImageAlpha(0f);
            ConfigureInitialImage();
        }

        public IEnumerator Play(Func<bool> shouldCancel)
        {
            AutoBind();
            ApplyCreditsText();
            ConfigureInitialImage();

            if (creditsText == null || canvasGroup == null)
            {
                yield break;
            }

            SetCanvasAlpha(0f);
            SetImageAlpha(0f);

            creditsText.ForceMeshUpdate();
            RectTransform textRect = creditsText.rectTransform;
            float preferredHeight = Mathf.Max(creditsText.preferredHeight, 800f);
            textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, preferredHeight + 80f);

            const float referenceHeight = 1080f;
            float startY = -referenceHeight * 0.5f - textRect.sizeDelta.y * 0.5f;
            float endY = referenceHeight * 0.5f + textRect.sizeDelta.y * 0.5f + endPadding;
            textRect.anchoredPosition = new Vector2(0f, startY);

            yield return FadeCanvas(1f, canvasFadeInDuration, shouldCancel);
            yield return FadeImage(1f, imageFadeDuration, shouldCancel);

            StartImageRotation(shouldCancel);

            while (!IsCancelled(shouldCancel) && textRect.anchoredPosition.y < endY)
            {
                Vector2 position = textRect.anchoredPosition;
                position.y += scrollSpeed * Time.unscaledDeltaTime;
                textRect.anchoredPosition = position;
                yield return null;
            }

            StopImageRotation();
        }

        public void StopPlayback()
        {
            StopImageRotation();
        }

        private void AutoBind()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (endingImage == null)
            {
                Transform imageTransform = transform.Find("EndingImage");
                if (imageTransform != null)
                {
                    endingImage = imageTransform.GetComponent<Image>();
                }
            }

            if (creditsText == null)
            {
                Transform textTransform = transform.Find("CreditsText");
                if (textTransform != null)
                {
                    creditsText = textTransform.GetComponent<TextMeshProUGUI>();
                }
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ApplyCreditsText()
        {
            if (creditsText == null)
            {
                return;
            }

            if (useCreditLines || string.IsNullOrEmpty(creditsText.text))
            {
                creditsText.text = creditLines == null || creditLines.Length == 0
                    ? "Thank you for playing"
                    : string.Join("\n", creditLines);
            }
        }

        private void ConfigureInitialImage()
        {
            if (endingImage == null)
            {
                return;
            }

            Sprite sprite = ResolveImageAt(0);
            if (sprite != null)
            {
                endingImage.sprite = sprite;
                endingImage.preserveAspect = true;
            }
        }

        private IEnumerator FadeCanvas(float targetAlpha, float duration, Func<bool> shouldCancel)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            if (duration <= 0f)
            {
                SetCanvasAlpha(targetAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && !IsCancelled(shouldCancel))
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetCanvasAlpha(Mathf.Lerp(startAlpha, targetAlpha, SmoothStep(t)));
                yield return null;
            }

            if (!IsCancelled(shouldCancel))
            {
                SetCanvasAlpha(targetAlpha);
            }
        }

        private IEnumerator FadeImage(float targetAlpha, float duration, Func<bool> shouldCancel)
        {
            if (endingImage == null)
            {
                yield break;
            }

            float startAlpha = endingImage.color.a;
            if (duration <= 0f)
            {
                SetImageAlpha(targetAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && !IsCancelled(shouldCancel))
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetImageAlpha(Mathf.Lerp(startAlpha, targetAlpha, SmoothStep(t)));
                yield return null;
            }

            if (!IsCancelled(shouldCancel))
            {
                SetImageAlpha(targetAlpha);
            }
        }

        private void StartImageRotation(Func<bool> shouldCancel)
        {
            StopImageRotation();

            if (endingImage == null || GetImageCount() <= 1)
            {
                return;
            }

            imageRotationRoutine = StartCoroutine(RotateImages(shouldCancel));
        }

        private void StopImageRotation()
        {
            if (imageRotationRoutine == null)
            {
                return;
            }

            StopCoroutine(imageRotationRoutine);
            imageRotationRoutine = null;
        }

        private IEnumerator RotateImages(Func<bool> shouldCancel)
        {
            int index = 0;

            while (!IsCancelled(shouldCancel))
            {
                yield return new WaitForSecondsRealtime(imageHoldSeconds);

                if (IsCancelled(shouldCancel))
                {
                    yield break;
                }

                int imageCount = GetImageCount();
                if (imageCount <= 1)
                {
                    yield break;
                }

                int nextIndex = index + 1;
                if (nextIndex >= imageCount)
                {
                    if (!loopImages)
                    {
                        yield break;
                    }

                    nextIndex = 0;
                }

                Sprite nextSprite = ResolveImageAt(nextIndex);
                if (nextSprite == null)
                {
                    index = nextIndex;
                    continue;
                }

                yield return CrossfadeImage(nextSprite, imageCrossfadeSeconds, shouldCancel);
                index = nextIndex;
            }
        }

        private IEnumerator CrossfadeImage(Sprite nextSprite, float duration, Func<bool> shouldCancel)
        {
            if (endingImage == null || nextSprite == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                endingImage.sprite = nextSprite;
                SetImageAlpha(1f);
                yield break;
            }

            yield return FadeImage(0f, duration * 0.5f, shouldCancel);
            if (IsCancelled(shouldCancel))
            {
                yield break;
            }

            endingImage.sprite = nextSprite;
            endingImage.preserveAspect = true;
            yield return FadeImage(1f, duration * 0.5f, shouldCancel);
        }

        private int GetImageCount()
        {
            if (imageSprites != null && imageSprites.Length > 0)
            {
                return imageSprites.Length;
            }

            return ResolveImageAt(0) != null ? 1 : 0;
        }

        private Sprite ResolveImageAt(int index)
        {
            if (imageSprites != null && index >= 0 && index < imageSprites.Length && imageSprites[index] != null)
            {
                return imageSprites[index];
            }

            return imageSprite;
        }

        private void SetCanvasAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private void SetImageAlpha(float alpha)
        {
            if (endingImage == null)
            {
                return;
            }

            Color color = endingImage.color;
            color.a = Mathf.Clamp01(alpha);
            endingImage.color = color;
        }

        private static bool IsCancelled(Func<bool> shouldCancel)
        {
            return shouldCancel != null && shouldCancel();
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
