using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// ステージの特定エリアに入ったときにカメラを切り替えるトリガー
/// メトロイドヴァニアの部屋遷移や、ボス部屋に入った時のカメラ固定などに使用します。
/// </summary>
public class RoomCameraTrigger : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField, Tooltip("この部屋に入ったときに有効になるカメラ（子オブジェクト等に配置したものをアサイン）")]
    private CinemachineCamera _roomCamera;

    [SerializeField, Tooltip("プレイヤー侵入時の優先度（デフォルトカメラの優先度より高く設定する）")]
    private int _activePriority = 20;

    [SerializeField, Tooltip("プレイヤー退出時（非アクティブ時）の優先度（デフォルトカメラの優先度より低く設定する）")]
    private int _inactivePriority = 0;

    [Header("Detection Settings")]
    [SerializeField, Tooltip("侵入を検知する対象のタグ名")]
    private string _playerTag = "Player";

    private void Awake()
    {
        if (_roomCamera == null)
        {
            Debug.LogWarning($"[{gameObject.name}] RoomCameraTriggerにCinemachineCameraが設定されていません！", this);
        }
        else
        {
            // ゲーム開始時は必ず優先度を下げておく（エディタ上の設定のまま意図せずアクティブになるのを防ぐため）
            DeactivateCamera();
        }
    }

    /// <summary>
    /// カメラをアクティブにする（優先度を上げる）
    /// </summary>
    public void ActivateCamera()
    {
        if (_roomCamera != null)
        {
            _roomCamera.Priority.Value = _activePriority;
            _roomCamera.Priority.Enabled = true;
        }
    }

    /// <summary>
    /// カメラを元の状態に戻す（優先度を下げる）
    /// </summary>
    public void DeactivateCamera()
    {
        if (_roomCamera != null)
        {
            _roomCamera.Priority.Value = _inactivePriority;
        }
    }

    // --- 2D 当たり判定 ---
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(_playerTag))
        {
            ActivateCamera();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(_playerTag))
        {
            DeactivateCamera();
        }
    }

    // --- 3D(2.5D) 当たり判定 ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_playerTag))
        {
            ActivateCamera();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_playerTag))
        {
            DeactivateCamera();
        }
    }
}
