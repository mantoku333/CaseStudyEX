using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.Player
{
    /// <summary>
    /// SRDebugger等から動的にアタッチされるデバッグ用のCheat（飛行・壁抜け）コントローラー
    /// アタッチされている間は物理演算と当たり判定を無効化し、直接入力を受けて空を飛びます。
    /// </summary>
    public class DebugCheatModeController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 15f;

        private Rigidbody2D _rb2d;
        private RigidbodyType2D _originalBodyType;
        private Collider2D[] _colliders;
        private bool[] _originalColliderStates;

        private void OnEnable()
        {
            // 物理演算の無効化（キネマティックにする）
            _rb2d = GetComponent<Rigidbody2D>();
            if (_rb2d != null)
            {
                _originalBodyType = _rb2d.bodyType;
                _rb2d.bodyType = RigidbodyType2D.Kinematic;
                _rb2d.linearVelocity = Vector2.zero;
            }

            // 当たり判定の無効化（壁抜けするため）
            _colliders = GetComponentsInChildren<Collider2D>();
            _originalColliderStates = new bool[_colliders.Length];
            for (int i = 0; i < _colliders.Length; i++)
            {
                _originalColliderStates[i] = _colliders[i].enabled;
                _colliders[i].enabled = false;
            }
            
            Debug.Log("[CheatMode] Cheat Mode ON");
        }

        private void Update()
        {
            Vector2 inputDir = Vector2.zero;

            // Input System経由でキーボードからWASD / 矢印の入力を直接取得
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) inputDir.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) inputDir.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) inputDir.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) inputDir.x += 1f;
            }

            // ゲームパッドの入力
            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    inputDir += stick; // キーボード入力と合成
                }
            }

            // 斜め移動が速くなりすぎないように正規化
            if (inputDir.sqrMagnitude > 1f)
            {
                inputDir.Normalize();
            }

            // トランスフォームを直接移動（壁を抜ける）
            if (inputDir != Vector2.zero)
            {
                transform.Translate(inputDir * (_moveSpeed * Time.deltaTime), Space.World);
            }
        }

        private void OnDisable()
        {
            // 物理演算を元に戻す
            if (_rb2d != null)
            {
                _rb2d.bodyType = _originalBodyType;
            }

            // 当たり判定を元に戻す
            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null)
                    {
                        _colliders[i].enabled = _originalColliderStates[i];
                    }
                }
            }
            
            Debug.Log("[CheatMode] Cheat Mode OFF");
        }
    }
}
