using System;
using UnityEngine;

namespace Metroidvania.Environment
{
    /// <summary>
    /// Parallax background controller for three-layer metroidvania mock.
    /// Each layer only needs a center tile. Left/right tiles are auto-created.
    /// </summary>
    public sealed class TripleParallaxBackground : MonoBehaviour
    {
        [Serializable]
        private sealed class Layer
        {
            [SerializeField] private string name = "Layer";
            [SerializeField] private Transform centerTile;
            [SerializeField, Range(0f, 1f)] private float parallaxFactor = 0.5f;
            [SerializeField] private bool followCameraY = true;
            [SerializeField, Range(0f, 1f)] private float verticalParallaxFactor = 1f;
            [SerializeField, Min(0f)] private float tileWidthOverride;

            private Transform _leftTile;
            private Transform _rightTile;
            private float _tileWidth;
            private float _centerX;
            private float _centerY;
            private float _offsetFromCamera;
            private float _offsetYFromCamera;
            private bool _isValid;

            public string Name => name;
            public bool IsValid => _isValid;

            public void Initialize(Transform controllerRoot, Transform cameraTarget)
            {
                _isValid = false;

                if (centerTile == null)
                {
                    return;
                }

                var centerRenderer = centerTile.GetComponent<SpriteRenderer>();
                if (centerRenderer == null || centerRenderer.sprite == null)
                {
                    return;
                }

                _tileWidth = tileWidthOverride > 0f ? tileWidthOverride : centerRenderer.bounds.size.x;
                if (_tileWidth <= 0f)
                {
                    return;
                }

                _leftTile = CreateCopy(controllerRoot, centerTile, centerRenderer, "Left");
                _rightTile = CreateCopy(controllerRoot, centerTile, centerRenderer, "Right");

                var camX = cameraTarget != null ? cameraTarget.position.x : 0f;
                var camY = cameraTarget != null ? cameraTarget.position.y : 0f;
                _centerX = centerTile.position.x;
                _centerY = centerTile.position.y;
                _offsetFromCamera = _centerX - (camX * parallaxFactor);
                _offsetYFromCamera = _centerY - (camY * verticalParallaxFactor);
                LayoutTiles();
                _isValid = true;
            }

            public void Tick(float cameraX, float cameraY)
            {
                if (!_isValid)
                {
                    return;
                }

                var targetCenterX = (cameraX * parallaxFactor) + _offsetFromCamera;
                var delta = targetCenterX - _centerX;

                if (delta >= _tileWidth || delta <= -_tileWidth)
                {
                    _centerX += Mathf.Floor(delta / _tileWidth) * _tileWidth;
                }
                else
                {
                    _centerX = targetCenterX;
                }

                if (followCameraY)
                {
                    _centerY = (cameraY * verticalParallaxFactor) + _offsetYFromCamera;
                }

                LayoutTiles();
            }

            private void LayoutTiles()
            {
                SetTilePosition(_leftTile, _centerX - _tileWidth, _centerY);
                SetTilePosition(centerTile, _centerX, _centerY);
                SetTilePosition(_rightTile, _centerX + _tileWidth, _centerY);
            }

            private static Transform CreateCopy(
                Transform controllerRoot,
                Transform sourceTransform,
                SpriteRenderer sourceRenderer,
                string suffix)
            {
                var copy = new GameObject($"{sourceTransform.name}_{suffix}");
                copy.transform.SetParent(controllerRoot, true);
                copy.transform.position = sourceTransform.position;
                copy.transform.rotation = sourceTransform.rotation;
                copy.transform.localScale = sourceTransform.localScale;

                var copyRenderer = copy.AddComponent<SpriteRenderer>();
                CopyRendererProperties(sourceRenderer, copyRenderer);
                return copy.transform;
            }

            private static void CopyRendererProperties(SpriteRenderer source, SpriteRenderer destination)
            {
                destination.sprite = source.sprite;
                destination.color = source.color;
                destination.flipX = source.flipX;
                destination.flipY = source.flipY;
                destination.drawMode = source.drawMode;
                destination.size = source.size;
                destination.tileMode = source.tileMode;
                destination.maskInteraction = source.maskInteraction;
                destination.sortingLayerID = source.sortingLayerID;
                destination.sortingOrder = source.sortingOrder;
                destination.sharedMaterial = source.sharedMaterial;
            }

            private static void SetTilePosition(Transform tile, float x, float y)
            {
                if (tile == null)
                {
                    return;
                }

                var position = tile.position;
                position.x = x;
                position.y = y;
                tile.position = position;
            }
        }

        [Header("Camera")]
        [SerializeField] private Transform cameraTarget;

        [Header("Layers (Far -> Near)")]
        [SerializeField] private Layer[] layers = new Layer[3];

        private void Awake()
        {
            if (cameraTarget == null && Camera.main != null)
            {
                cameraTarget = Camera.main.transform;
            }

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null)
                {
                    continue;
                }

                layer.Initialize(transform, cameraTarget);
                if (!layer.IsValid)
                {
                    Debug.LogWarning($"TripleParallaxBackground: Layer '{layer.Name}' is not configured.", this);
                }
            }
        }

        private void LateUpdate()
        {
            if (cameraTarget == null)
            {
                return;
            }

            var camX = cameraTarget.position.x;
            var camY = cameraTarget.position.y;
            for (var i = 0; i < layers.Length; i++)
            {
                layers[i]?.Tick(camX, camY);
            }
        }
    }
}
