using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class LastBossSpriteAnimator : MonoBehaviour
    {
        [Header("Renderer")]
        [SerializeField] private SpriteRenderer mainRenderer;
        [SerializeField] private bool flipXWhenFacingRight = true;
        [SerializeField] private Sprite idleSprite;

        [Header("Frames")]
        [SerializeField] private Sprite[] moveFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] normalAttackFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] horizontalStartFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] horizontalEndFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] verticalStartFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] verticalEndFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] downStartFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] downEndFrames = System.Array.Empty<Sprite>();

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float moveFramesPerSecond = 10f;
        [SerializeField, Min(0.01f)] private float attackFramesPerSecond = 12f;
        [SerializeField, Min(0.01f)] private float downFramesPerSecond = 10f;

        private enum VisualState
        {
            None,
            Idle,
            Move,
            NormalAttack,
            HorizontalStart,
            HorizontalEnd,
            VerticalStart,
            VerticalEnd,
            DownStart,
            DownHold,
            DownEnd,
            Dead
        }

        private VisualState currentState = VisualState.None;
        private Sprite defaultSprite;
        private Color defaultColor = Color.white;
        private Sprite[] activeFrames = System.Array.Empty<Sprite>();
        private float activeFramesPerSecond = 12f;
        private float stateElapsed;
        private int activeFrameIndex = -1;
        private bool loopActiveFrames;
        private bool holdLastFrame;

        public SpriteRenderer MainRenderer
        {
            get
            {
                ResolveRenderer();
                return mainRenderer;
            }
        }

        public Color DefaultColor => defaultColor;
        public float DownEndDuration => GetDuration(downEndFrames, downFramesPerSecond);

        private void Awake()
        {
            ResolveRenderer();
            if (mainRenderer != null)
            {
                defaultSprite = mainRenderer.sprite;
                defaultColor = mainRenderer.color;
            }
        }

        private void OnEnable()
        {
            currentState = VisualState.None;
            PlayIdle();
        }

        private void Update()
        {
            AdvanceFrames(Time.deltaTime);
        }

        public void SetFacing(int facingDirection)
        {
            if (MainRenderer == null)
            {
                return;
            }

            mainRenderer.flipX = facingDirection >= 0 ? flipXWhenFacingRight : !flipXWhenFacingRight;
        }

        public void SetColor(Color color)
        {
            if (MainRenderer != null)
            {
                mainRenderer.color = color;
            }
        }

        public void PlayIdle()
        {
            if (currentState == VisualState.Idle)
            {
                return;
            }

            currentState = VisualState.Idle;
            StopFramePlayback();
            ApplySprite(idleSprite != null ? idleSprite : defaultSprite);
        }

        public void PlayMove()
        {
            PlayFrames(VisualState.Move, moveFrames, moveFramesPerSecond, true, true);
        }

        public void PlayNormalAttack()
        {
            PlayFrames(VisualState.NormalAttack, normalAttackFrames, attackFramesPerSecond, false, true);
        }

        public void PlayHorizontalStart()
        {
            PlayFrames(VisualState.HorizontalStart, horizontalStartFrames, attackFramesPerSecond, false, true);
        }

        public void PlayHorizontalEnd()
        {
            PlayFrames(VisualState.HorizontalEnd, horizontalEndFrames, attackFramesPerSecond, false, true);
        }

        public void PlayVerticalStart()
        {
            PlayFrames(VisualState.VerticalStart, verticalStartFrames, attackFramesPerSecond, false, true);
        }

        public void PlayVerticalEnd()
        {
            PlayFrames(VisualState.VerticalEnd, verticalEndFrames, attackFramesPerSecond, false, true);
        }

        public void PlayDownStart()
        {
            PlayFrames(VisualState.DownStart, downStartFrames, downFramesPerSecond, false, true);
        }

        public void PlayDownHold()
        {
            if (currentState == VisualState.DownHold)
            {
                return;
            }

            currentState = VisualState.DownHold;
            StopFramePlayback();
            Sprite holdSprite = GetLastFrame(downStartFrames);
            ApplySprite(holdSprite != null ? holdSprite : idleSprite != null ? idleSprite : defaultSprite);
        }

        public void PlayDownEnd()
        {
            PlayFrames(VisualState.DownEnd, downEndFrames, downFramesPerSecond, false, true);
        }

        public void PlayDead()
        {
            currentState = VisualState.Dead;
            StopFramePlayback();
        }

        private void PlayFrames(
            VisualState nextState,
            Sprite[] frames,
            float framesPerSecond,
            bool loop,
            bool holdLast)
        {
            if (currentState == nextState)
            {
                return;
            }

            currentState = nextState;
            activeFrames = frames ?? System.Array.Empty<Sprite>();
            activeFramesPerSecond = Mathf.Max(0.01f, framesPerSecond);
            loopActiveFrames = loop;
            holdLastFrame = holdLast;
            stateElapsed = 0f;
            activeFrameIndex = -1;

            if (activeFrames.Length == 0)
            {
                StopFramePlayback();
                if (nextState == VisualState.Move)
                {
                    ApplySprite(idleSprite != null ? idleSprite : defaultSprite);
                }

                return;
            }

            ApplyFrame(0);
        }

        private void AdvanceFrames(float deltaTime)
        {
            if (activeFrames == null || activeFrames.Length == 0)
            {
                return;
            }

            stateElapsed += Mathf.Max(0f, deltaTime);
            int nextFrameIndex = Mathf.FloorToInt(stateElapsed * activeFramesPerSecond);

            if (loopActiveFrames)
            {
                nextFrameIndex %= activeFrames.Length;
            }
            else if (nextFrameIndex >= activeFrames.Length)
            {
                if (holdLastFrame)
                {
                    nextFrameIndex = activeFrames.Length - 1;
                }
                else
                {
                    PlayIdle();
                    return;
                }
            }

            ApplyFrame(nextFrameIndex);
        }

        private void ApplyFrame(int frameIndex)
        {
            if (frameIndex == activeFrameIndex || frameIndex < 0 || frameIndex >= activeFrames.Length)
            {
                return;
            }

            activeFrameIndex = frameIndex;
            ApplySprite(activeFrames[frameIndex]);
        }

        private void ApplySprite(Sprite sprite)
        {
            if (sprite == null || MainRenderer == null)
            {
                return;
            }

            mainRenderer.sprite = sprite;
        }

        private void StopFramePlayback()
        {
            activeFrames = System.Array.Empty<Sprite>();
            stateElapsed = 0f;
            activeFrameIndex = -1;
            loopActiveFrames = false;
            holdLastFrame = false;
        }

        private void ResolveRenderer()
        {
            if (mainRenderer == null)
            {
                mainRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private static Sprite GetLastFrame(Sprite[] frames)
        {
            return frames != null && frames.Length > 0 ? frames[frames.Length - 1] : null;
        }

        private static float GetDuration(Sprite[] frames, float framesPerSecond)
        {
            if (frames == null || frames.Length == 0)
            {
                return 0f;
            }

            return frames.Length / Mathf.Max(0.01f, framesPerSecond);
        }
    }
}
