using Spine;
using Spine.Unity;
using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// Spineアニメーションの制御を行うコンポーネント。
    /// PlayerPlatformerMockControllerの状態に応じてアニメーションを切り替える。
    /// プレイヤーの子オブジェクトにアタッチして使用する。
    /// </summary>
    [RequireComponent(typeof(SkeletonAnimation))]
    public class PlayerSpineAnimator : MonoBehaviour
    {
        [Header("アニメーション名（Spineデータに合わせて設定）")]
        [SpineAnimation] [SerializeField] private string idleAnimation = "idle";
        [SpineAnimation] [SerializeField] private string runAnimation = "run";
        [SpineAnimation] [SerializeField] private string jumpAnimation = "jump";

        [Header("参照")]
        [Tooltip("親のPlayerPlatformerMockControllerを設定。未設定なら親から自動取得")]
        [SerializeField] private PlayerPlatformerMockController controller;

        [Header("トランジション設定")]
        [Tooltip("アニメーション切り替え時のミックス時間（秒）")]
        [SerializeField, Min(0f)] private float defaultMixDuration = 0.15f;

        private SkeletonAnimation _skeletonAnimation;
        private Spine.AnimationState _animationState;
        private Skeleton _skeleton;
        private string _currentAnimationName;

        private void Awake()
        {
            _skeletonAnimation = GetComponent<SkeletonAnimation>();
            _animationState = _skeletonAnimation.AnimationState;
            _skeleton = _skeletonAnimation.Skeleton;

            // コントローラーが未設定なら親から自動取得
            if (controller == null)
            {
                controller = GetComponentInParent<PlayerPlatformerMockController>();
            }

            if (controller == null)
            {
                Debug.LogError("PlayerSpineAnimator: PlayerPlatformerMockControllerが見つかりません", this);
                enabled = false;
                return;
            }

            // デフォルトのミックス時間を設定
            _animationState.Data.DefaultMix = defaultMixDuration;
        }

        private void OnEnable()
        {
            // 初期アニメーションを設定
            _currentAnimationName = null;
            SetAnimation(idleAnimation, true);
        }

        private void Update()
        {
            if (controller == null) return;

            // 向きの反映（SpineはScaleXで反転する）
            UpdateFacing();

            // 状態に応じたアニメーションの更新
            UpdateAnimation();
        }

        /// <summary>
        /// プレイヤーの向きをSpineスケルトンに反映する。
        /// SpriteRendererのflipXの代わりに、SkeletonのScaleXを使う。
        /// </summary>
        private void UpdateFacing()
        {
            _skeleton.ScaleX = controller.IsFacingRight ? 1f : -1f;
        }

        /// <summary>
        /// プレイヤーの状態に応じたアニメーションを決定・設定する。
        /// 優先度: グライド > 空中(jump) > 走り(run) > 待機(idle)
        /// </summary>
        private void UpdateAnimation()
        {
            string targetAnimation;
            var shouldLoop = true;

            if (controller.IsGliding)
            {
                // グライド中（Spineboyには専用アニメがないのでjumpで代用）
                targetAnimation = jumpAnimation;
            }
            else if (!controller.IsGrounded)
            {
                // 空中
                targetAnimation = jumpAnimation;
            }
            else if (controller.IsMoving)
            {
                // 地上で移動中
                targetAnimation = runAnimation;
            }
            else
            {
                // 地上で静止
                targetAnimation = idleAnimation;
            }

            // 同じアニメーションなら再設定しない
            SetAnimation(targetAnimation, shouldLoop);
        }

        /// <summary>
        /// アニメーションを設定する。同じアニメーションは再設定しない。
        /// </summary>
        /// <param name="animationName">アニメーション名</param>
        /// <param name="loop">ループ再生するか</param>
        private void SetAnimation(string animationName, bool loop)
        {
            if (_currentAnimationName == animationName) return;
            if (string.IsNullOrEmpty(animationName)) return;

            _currentAnimationName = animationName;
            _animationState.SetAnimation(0, animationName, loop);
        }
    }
}
