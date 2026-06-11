using System;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class LastBossBladeVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private string visualChildName = "BladeEffectVisual";

        private GridSpriteSheetPlayer player;
        private Transform visualTransform;
        private SpriteRenderer rootRenderer;

        public SpriteRenderer Renderer => targetRenderer;

        private void Awake()
        {
            CacheComponents();
        }

        public bool PlayGround(
            GridSpriteSheetClip clip,
            int uprightFrameIndex,
            Vector2 targetWorldSize,
            Action uprightFrameReached)
        {
            if (!clip.IsValid)
            {
                return false;
            }

            CacheComponents();
            ConfigureTargetSize(targetWorldSize);
            ResetAlpha();

            bool notified = false;
            int clampedFrame = Mathf.Clamp(uprightFrameIndex, 0, Mathf.Max(0, clip.FrameCount - 1));
            return player.Play(
                clip,
                loop: false,
                holdLast: true,
                hideOnComplete: false,
                frameChanged: frameIndex =>
                {
                    if (notified || frameIndex < clampedFrame)
                    {
                        return;
                    }

                    notified = true;
                    uprightFrameReached?.Invoke();
                });
        }

        public bool PlayRainIn(GridSpriteSheetClip clip, Vector2 targetWorldSize)
        {
            if (!clip.IsValid)
            {
                return false;
            }

            CacheComponents();
            ConfigureTargetSize(targetWorldSize);
            ResetAlpha();
            return player.Play(
                clip,
                loop: false,
                holdLast: true,
                hideOnComplete: false);
        }

        public bool PlayRainOut(GridSpriteSheetClip clip, Vector2 targetWorldSize, Action completed)
        {
            if (!clip.IsValid)
            {
                completed?.Invoke();
                return false;
            }

            CacheComponents();
            ConfigureTargetSize(targetWorldSize);
            ResetAlpha();
            return player.Play(
                clip,
                loop: false,
                holdLast: false,
                hideOnComplete: true,
                completed: completed);
        }

        public void StopAndHide()
        {
            CacheComponents();
            player?.StopPlayback();
        }

        private void ConfigureTargetSize(Vector2 targetWorldSize)
        {
            if (player == null)
            {
                return;
            }

            if (targetWorldSize.x > 0.001f && targetWorldSize.y > 0.001f)
            {
                player.SetTargetWorldSize(targetWorldSize);
            }
            else
            {
                player.ClearTargetWorldSize();
            }
        }

        private void ResetAlpha()
        {
            if (targetRenderer == null)
            {
                return;
            }

            Color color = targetRenderer.color;
            color.a = 1f;
            targetRenderer.color = color;
        }

        private void CacheComponents()
        {
            if (rootRenderer == null)
            {
                rootRenderer = GetComponent<SpriteRenderer>();
            }

            EnsureVisualTransform();

            if (targetRenderer == null || targetRenderer.transform == transform)
            {
                targetRenderer = visualTransform.GetComponent<SpriteRenderer>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            if (player == null || player.transform != visualTransform)
            {
                player = visualTransform.GetComponent<GridSpriteSheetPlayer>();
            }

            if (player == null)
            {
                player = visualTransform.gameObject.AddComponent<GridSpriteSheetPlayer>();
            }

            ApplyRootRendererSettings();
            player.ConfigureRenderer(targetRenderer);
        }

        private void EnsureVisualTransform()
        {
            if (visualTransform != null)
            {
                return;
            }

            if (targetRenderer != null && targetRenderer.transform != transform)
            {
                visualTransform = targetRenderer.transform;
                return;
            }

            Transform existingChild = transform.Find(visualChildName);
            if (existingChild != null)
            {
                visualTransform = existingChild;
                return;
            }

            GameObject visualObject = new GameObject(visualChildName);
            visualTransform = visualObject.transform;
            visualTransform.SetParent(transform, false);
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;
        }

        private void ApplyRootRendererSettings()
        {
            if (rootRenderer == null || targetRenderer == null || rootRenderer == targetRenderer)
            {
                return;
            }

            targetRenderer.sortingLayerID = rootRenderer.sortingLayerID;
            targetRenderer.sortingOrder = rootRenderer.sortingOrder;
            targetRenderer.sharedMaterial = rootRenderer.sharedMaterial;
            targetRenderer.color = rootRenderer.color;
            rootRenderer.enabled = false;
        }
    }
}
