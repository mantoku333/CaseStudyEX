using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マウスカーソルの位置に応じて、カメラの注視点をプレイヤーから少しずらすスクリプト
/// 使い方: 
/// 1. このスクリプトをアタッチした空のGameObject（「CameraTarget」など）を作成します。
/// 2. CinemachineCamera の「Follow」に、その GameObject を設定します。
/// 3. インスペクタで「Player Transform」に主人公の Transform を設定します。
/// </summary>
public class MouseLookCameraTarget : MonoBehaviour
{
    [Header("ターゲット設定")]
    [SerializeField, Tooltip("基準となるプレイヤーのTransform")]
    private Transform _playerTransform;

    [Header("見渡し設定")]
    [SerializeField, Tooltip("マウス方向にカメラをずらす最大距離（ワールド座標の単位）")]
    private float _maxOffsetDistance = 3f;

    [SerializeField, Tooltip("オフセットが目標位置に向かう際の滑らかさ（大きいほどもっさりする）")]
    private float _smoothTime = 0.1f;

    private Vector3 _currentVelocity;
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
        
        // 親子関係を切っておく（カメラ独自の遅延をプレイヤーの動きと切り離して綺麗に出すため）
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
    }

    private void LateUpdate()
    {
        if (_playerTransform == null) return;
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        // マウスが接続されていない場合は処理をスキップ
        if (Mouse.current == null) return;

        // 新しい Input System からマウスのスクリーン座標を取得
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // カメラから見た画面上のマウス座標（左下0,0 / 右上1,1）を取得
        Vector3 mouseViewportPos = _mainCamera.ScreenToViewportPoint(new Vector3(mousePos.x, mousePos.y, 0f));

        // 中心(0.5, 0.5)からのズレを -1 ～ 1 の範囲で計算
        float offsetX = (mouseViewportPos.x - 0.5f) * 2f;
        float offsetY = (mouseViewportPos.y - 0.5f) * 2f;

        // 画面外にカーソルが出た場合に備えて上限をクランプしておく
        offsetX = Mathf.Clamp(offsetX, -1f, 1f);
        offsetY = Mathf.Clamp(offsetY, -1f, 1f);

        // 最大距離を掛け合わせて最終的なオフセットを決定
        Vector3 targetOffset = new Vector3(offsetX, offsetY, 0f) * _maxOffsetDistance;

        // 目標となるワールド座標（プレイヤー位置＋オフセット）
        Vector3 targetPosition = _playerTransform.position + targetOffset;

        // 滑らかに移動させる
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);
    }
}
