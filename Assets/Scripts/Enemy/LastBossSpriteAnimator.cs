using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class LastBossSpriteAnimator : MonoBehaviour
    {
        public enum AnimationState
        {
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

        public enum OffsetEase
        {
            Linear,
            EaseIn,
            EaseOut,
            EaseInOut
        }

        [System.Serializable]
        public sealed class AnimationMotionOffsetSet
        {
            [SerializeField] private AnimationState state;
            [SerializeField] private string stateName;
            [SerializeField, Min(0f)] private float clipLength;
            [SerializeField] private bool loop;
            [SerializeField] private MotionOffsetKey[] keys = new MotionOffsetKey[0];

            public AnimationState State => state;
            public string StateName => stateName;
            public float ClipLength => clipLength;
            public bool Loop => loop;
            public MotionOffsetKey[] Keys => keys;
        }

        [System.Serializable]
        public struct MotionOffsetKey
        {
            [SerializeField, Min(0f)] private float time;
            [SerializeField] private Vector3 offset;
            [SerializeField] private OffsetEase easeToNext;

            public float Time => time;
            public Vector3 Offset => offset;
            public OffsetEase EaseToNext => easeToNext;

            public void SetOffset(Vector3 value)
            {
                offset = value;
            }
        }

        [System.Serializable]
        public sealed class AnimationFrameOffsetSet
        {
            [SerializeField] private AnimationState state;
            [SerializeField] private string stateName;
            [SerializeField, Min(0f)] private float clipLength;
            [SerializeField] private bool loop;
            [SerializeField] private FrameOffset[] frames = new FrameOffset[0];

            public AnimationState State => state;
            public string StateName => stateName;
            public float ClipLength => clipLength;
            public bool Loop => loop;
            public FrameOffset[] Frames => frames;
        }

        [System.Serializable]
        public struct FrameOffset
        {
            [SerializeField, Min(0f)] private float time;
            [SerializeField] private Sprite sprite;
            [SerializeField] private Vector3 offset;

            public float Time => time;
            public Sprite Sprite => sprite;
            public Vector3 Offset => offset;

            public void SetOffset(Vector3 value)
            {
                offset = value;
            }
        }

        [Header("Renderer")]
        [SerializeField] private SpriteRenderer mainRenderer;
        [SerializeField] private bool flipXWhenFacingRight = false;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private int animatorLayer = 0;
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string moveStateName = "move";
        [SerializeField] private string normalAttackStateName = "attack_normal";
        [SerializeField] private string horizontalStartStateName = "attack_horizontal_start";
        [SerializeField] private string horizontalEndStateName = "attack_horizontal_end";
        [SerializeField] private string verticalStartStateName = "attack_vertical_start";
        [SerializeField] private string verticalEndStateName = "attack_vertical_end";
        [SerializeField] private string downStartStateName = "down_start";
        [SerializeField] private string downHoldStateName = "down_hold";
        [SerializeField] private string downEndStateName = "down_end";

        [Header("Timing")]
        [SerializeField, Min(0f)] private float downStartDuration = 3.75f;
        [SerializeField, Min(0f)] private float downEndDuration = 3.0833333f;

        [SerializeField, HideInInspector] private Vector3 idleOffset;
        [SerializeField, HideInInspector] private Vector3 moveOffset;
        [SerializeField, HideInInspector] private Vector3 normalAttackOffset;
        [SerializeField, HideInInspector] private Vector3 horizontalStartOffset;
        [SerializeField, HideInInspector] private Vector3 horizontalEndOffset;
        [SerializeField, HideInInspector] private Vector3 verticalStartOffset;
        [SerializeField, HideInInspector] private Vector3 verticalEndOffset;
        [SerializeField, HideInInspector] private Vector3 downStartOffset;
        [SerializeField, HideInInspector] private Vector3 downHoldOffset;
        [SerializeField, HideInInspector] private Vector3 downEndOffset;
        [SerializeField, HideInInspector] private Vector3 deadOffset;
        [SerializeField, HideInInspector] private AnimationMotionOffsetSet[] motionOffsetSets = new AnimationMotionOffsetSet[0];
        [SerializeField, HideInInspector] private AnimationFrameOffsetSet[] frameOffsetSets = new AnimationFrameOffsetSet[0];

        private AnimationState? currentState;
        private Vector3 baseLocalPosition;
        private bool baseLocalPositionCaptured;
        private Color defaultColor = Color.white;
        private bool warnedNoAnimator;

        public SpriteRenderer MainRenderer
        {
            get
            {
                ResolveReferences();
                return mainRenderer;
            }
        }

        public Color DefaultColor => defaultColor;
        public float DownStartDuration => downStartDuration;
        public float DownEndDuration => downEndDuration;

        private void Awake()
        {
            ResolveReferences();
            CaptureBaseLocalPosition();
            if (mainRenderer != null)
            {
                defaultColor = mainRenderer.color;
            }
        }

        private void OnEnable()
        {
            CaptureBaseLocalPosition();
            currentState = null;
            PlayIdle();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && baseLocalPositionCaptured)
            {
                ApplyCurrentOffset();
            }
        }

        private void LateUpdate()
        {
            if (baseLocalPositionCaptured && currentState.HasValue)
            {
                ApplyCurrentOffset();
            }
        }

        public void SetAnimationOffset(AnimationState state, Vector3 offset)
        {
            SetOffsetValue(state, offset);
            if (currentState == state)
            {
                ApplyCurrentOffset();
            }
        }

        public Vector3 GetAnimationOffset(AnimationState state)
        {
            return GetOffsetValue(state);
        }

        public Vector3 GetCurrentAnimationOffset()
        {
            return currentState.HasValue ? GetCurrentOffset() : Vector3.zero;
        }

        public void ResetAnimationOffsets()
        {
            idleOffset = Vector3.zero;
            moveOffset = Vector3.zero;
            normalAttackOffset = Vector3.zero;
            horizontalStartOffset = Vector3.zero;
            horizontalEndOffset = Vector3.zero;
            verticalStartOffset = Vector3.zero;
            verticalEndOffset = Vector3.zero;
            downStartOffset = Vector3.zero;
            downHoldOffset = Vector3.zero;
            downEndOffset = Vector3.zero;
            deadOffset = Vector3.zero;
            ResetMotionOffsets();
            ResetFrameOffsets();
            ApplyCurrentOffset();
        }

        public Vector3 GetAnimationMotionOffset(AnimationState state, float time)
        {
            AnimationMotionOffsetSet offsetSet = FindMotionOffsetSet(state);
            return offsetSet != null ? EvaluateMotionOffset(offsetSet, time) : Vector3.zero;
        }

        public Vector3 GetAnimationFrameOffset(AnimationState state, float time)
        {
            AnimationFrameOffsetSet offsetSet = FindFrameOffsetSet(state);
            return offsetSet != null ? EvaluateFrameOffset(offsetSet, time) : Vector3.zero;
        }

        public void ResetMotionOffsets()
        {
            if (motionOffsetSets == null)
            {
                return;
            }

            for (int setIndex = 0; setIndex < motionOffsetSets.Length; setIndex++)
            {
                AnimationMotionOffsetSet offsetSet = motionOffsetSets[setIndex];
                if (offsetSet == null || offsetSet.Keys == null)
                {
                    continue;
                }

                MotionOffsetKey[] keys = offsetSet.Keys;
                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    SetMotionOffsetKeyValue(ref keys[keyIndex], Vector3.zero);
                }
            }
        }

        public void ResetFrameOffsets()
        {
            if (frameOffsetSets == null)
            {
                return;
            }

            for (int setIndex = 0; setIndex < frameOffsetSets.Length; setIndex++)
            {
                AnimationFrameOffsetSet offsetSet = frameOffsetSets[setIndex];
                if (offsetSet == null || offsetSet.Frames == null)
                {
                    continue;
                }

                FrameOffset[] frames = offsetSet.Frames;
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    SetFrameOffsetValue(ref frames[frameIndex], Vector3.zero);
                }
            }
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
            PlayState(AnimationState.Idle, idleStateName);
        }

        public void PlayMove()
        {
            PlayState(AnimationState.Move, moveStateName);
        }

        public void PlayNormalAttack()
        {
            PlayState(AnimationState.NormalAttack, normalAttackStateName);
        }

        public void PlayHorizontalStart()
        {
            PlayState(AnimationState.HorizontalStart, horizontalStartStateName);
        }

        public void PlayHorizontalEnd()
        {
            PlayState(AnimationState.HorizontalEnd, horizontalEndStateName);
        }

        public void PlayVerticalStart()
        {
            PlayState(AnimationState.VerticalStart, verticalStartStateName);
        }

        public void PlayVerticalEnd()
        {
            PlayState(AnimationState.VerticalEnd, verticalEndStateName);
        }

        public void PlayDownStart()
        {
            PlayState(AnimationState.DownStart, downStartStateName);
        }

        public void PlayDownHold()
        {
            PlayState(AnimationState.DownHold, downHoldStateName);
        }

        public void PlayDownEnd()
        {
            PlayState(AnimationState.DownEnd, downEndStateName);
        }

        public void PlayDead()
        {
            currentState = AnimationState.Dead;
            ApplyCurrentOffset();
        }

        private void PlayState(AnimationState nextState, string stateName)
        {
            if (currentState == nextState)
            {
                return;
            }

            currentState = nextState;
            ApplyCurrentOffset();
            if (string.IsNullOrEmpty(stateName) || !IsAnimatorReady())
            {
                return;
            }

            animator.Play(stateName, animatorLayer, 0f);
        }

        private void CaptureBaseLocalPosition()
        {
            if (baseLocalPositionCaptured)
            {
                return;
            }

            baseLocalPosition = transform.localPosition;
            baseLocalPositionCaptured = true;
        }

        private void ApplyCurrentOffset()
        {
            CaptureBaseLocalPosition();
            transform.localPosition = baseLocalPosition + GetCurrentOffset();
        }

        private Vector3 GetCurrentOffset()
        {
            if (!currentState.HasValue)
            {
                return Vector3.zero;
            }

            AnimationState state = currentState.Value;
            return GetOffsetValue(state) + GetMotionOffsetValue(state) + GetFrameOffsetValue(state);
        }

        private Vector3 GetMotionOffsetValue(AnimationState state)
        {
            AnimationMotionOffsetSet offsetSet = FindMotionOffsetSet(state);
            if (offsetSet == null || offsetSet.Keys == null || offsetSet.Keys.Length == 0)
            {
                return Vector3.zero;
            }

            return EvaluateMotionOffset(offsetSet, GetCurrentAnimationTime(offsetSet.ClipLength, offsetSet.Loop));
        }

        private Vector3 GetFrameOffsetValue(AnimationState state)
        {
            AnimationFrameOffsetSet offsetSet = FindFrameOffsetSet(state);
            if (offsetSet == null || offsetSet.Frames == null || offsetSet.Frames.Length == 0)
            {
                return Vector3.zero;
            }

            return EvaluateFrameOffset(offsetSet, GetCurrentAnimationTime(offsetSet.ClipLength, offsetSet.Loop));
        }

        private float GetCurrentAnimationTime(float clipLength, bool loop)
        {
            clipLength = Mathf.Max(0f, clipLength);
            if (clipLength <= 0f || !IsAnimatorReady())
            {
                return 0f;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            float normalizedTime = stateInfo.normalizedTime;
            float normalizedPosition = loop
                ? Mathf.Repeat(normalizedTime, 1f)
                : Mathf.Clamp01(normalizedTime);
            return normalizedPosition * clipLength;
        }

        private AnimationMotionOffsetSet FindMotionOffsetSet(AnimationState state)
        {
            if (motionOffsetSets == null)
            {
                return null;
            }

            for (int i = 0; i < motionOffsetSets.Length; i++)
            {
                AnimationMotionOffsetSet offsetSet = motionOffsetSets[i];
                if (offsetSet != null && offsetSet.State == state)
                {
                    return offsetSet;
                }
            }

            return null;
        }

        private AnimationFrameOffsetSet FindFrameOffsetSet(AnimationState state)
        {
            if (frameOffsetSets == null)
            {
                return null;
            }

            for (int i = 0; i < frameOffsetSets.Length; i++)
            {
                AnimationFrameOffsetSet offsetSet = frameOffsetSets[i];
                if (offsetSet != null && offsetSet.State == state)
                {
                    return offsetSet;
                }
            }

            return null;
        }

        private static Vector3 EvaluateFrameOffset(AnimationFrameOffsetSet offsetSet, float time)
        {
            FrameOffset[] frames = offsetSet.Frames;
            if (frames == null || frames.Length == 0)
            {
                return Vector3.zero;
            }

            Vector3 offset = frames[0].Offset;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].Time > time)
                {
                    break;
                }

                offset = frames[i].Offset;
            }

            return offset;
        }

        private static Vector3 EvaluateMotionOffset(AnimationMotionOffsetSet offsetSet, float time)
        {
            MotionOffsetKey[] keys = offsetSet.Keys;
            if (keys == null || keys.Length == 0)
            {
                return Vector3.zero;
            }

            if (keys.Length == 1)
            {
                return keys[0].Offset;
            }

            int firstIndex = 0;
            int lastIndex = 0;
            float firstTime = keys[0].Time;
            float lastTime = keys[0].Time;
            for (int i = 1; i < keys.Length; i++)
            {
                if (keys[i].Time < firstTime)
                {
                    firstTime = keys[i].Time;
                    firstIndex = i;
                }

                if (keys[i].Time > lastTime)
                {
                    lastTime = keys[i].Time;
                    lastIndex = i;
                }
            }

            if (time <= firstTime)
            {
                return keys[firstIndex].Offset;
            }

            if (time >= lastTime)
            {
                return keys[lastIndex].Offset;
            }

            int previousIndex = firstIndex;
            int nextIndex = lastIndex;
            float previousTime = float.NegativeInfinity;
            float nextTime = float.PositiveInfinity;
            for (int i = 0; i < keys.Length; i++)
            {
                float keyTime = keys[i].Time;
                if (keyTime <= time && keyTime >= previousTime)
                {
                    previousTime = keyTime;
                    previousIndex = i;
                }

                if (keyTime >= time && keyTime <= nextTime)
                {
                    nextTime = keyTime;
                    nextIndex = i;
                }
            }

            if (previousIndex == nextIndex || Mathf.Approximately(previousTime, nextTime))
            {
                return keys[previousIndex].Offset;
            }

            MotionOffsetKey current = keys[previousIndex];
            MotionOffsetKey next = keys[nextIndex];
            float duration = Mathf.Max(0.0001f, nextTime - previousTime);
            float t = Mathf.Clamp01((time - previousTime) / duration);
            return Vector3.LerpUnclamped(current.Offset, next.Offset, ApplyEase(t, current.EaseToNext));
        }

        private static float ApplyEase(float t, OffsetEase ease)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case OffsetEase.EaseIn:
                    return t * t;
                case OffsetEase.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case OffsetEase.EaseInOut:
                    return t * t * (3f - 2f * t);
                default:
                    return t;
            }
        }

        private static void SetMotionOffsetKeyValue(ref MotionOffsetKey key, Vector3 offset)
        {
            key.SetOffset(offset);
        }

        private static void SetFrameOffsetValue(ref FrameOffset frame, Vector3 offset)
        {
            frame.SetOffset(offset);
        }

        private Vector3 GetOffsetValue(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle:
                    return idleOffset;
                case AnimationState.Move:
                    return moveOffset;
                case AnimationState.NormalAttack:
                    return normalAttackOffset;
                case AnimationState.HorizontalStart:
                    return horizontalStartOffset;
                case AnimationState.HorizontalEnd:
                    return horizontalEndOffset;
                case AnimationState.VerticalStart:
                    return verticalStartOffset;
                case AnimationState.VerticalEnd:
                    return verticalEndOffset;
                case AnimationState.DownStart:
                    return downStartOffset;
                case AnimationState.DownHold:
                    return downHoldOffset;
                case AnimationState.DownEnd:
                    return downEndOffset;
                case AnimationState.Dead:
                    return deadOffset;
                default:
                    return Vector3.zero;
            }
        }

        private void SetOffsetValue(AnimationState state, Vector3 offset)
        {
            switch (state)
            {
                case AnimationState.Idle:
                    idleOffset = offset;
                    break;
                case AnimationState.Move:
                    moveOffset = offset;
                    break;
                case AnimationState.NormalAttack:
                    normalAttackOffset = offset;
                    break;
                case AnimationState.HorizontalStart:
                    horizontalStartOffset = offset;
                    break;
                case AnimationState.HorizontalEnd:
                    horizontalEndOffset = offset;
                    break;
                case AnimationState.VerticalStart:
                    verticalStartOffset = offset;
                    break;
                case AnimationState.VerticalEnd:
                    verticalEndOffset = offset;
                    break;
                case AnimationState.DownStart:
                    downStartOffset = offset;
                    break;
                case AnimationState.DownHold:
                    downHoldOffset = offset;
                    break;
                case AnimationState.DownEnd:
                    downEndOffset = offset;
                    break;
                case AnimationState.Dead:
                    deadOffset = offset;
                    break;
            }
        }

        private bool IsAnimatorReady()
        {
            bool isReady = animator != null &&
                           animatorLayer >= 0 &&
                           animator.runtimeAnimatorController != null;
            if (!isReady && !warnedNoAnimator)
            {
                Debug.LogWarning("LastBossSpriteAnimator: Animator is missing or has no controller.", this);
                warnedNoAnimator = true;
            }

            if (isReady)
            {
                warnedNoAnimator = false;
            }

            return isReady;
        }

        private void ResolveReferences()
        {
            if (mainRenderer == null)
            {
                mainRenderer = GetComponent<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }
    }
}
