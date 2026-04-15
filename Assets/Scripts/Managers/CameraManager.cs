using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private CinemachineImpulseSource _impulseSource;

    private bool _isFollowCamActive = true;
    private CinemachineCamera _followCam;
    private CinemachineCamera _directFollowCam;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // Keep only the manager object persistent.
            // Persisting transform.root can also carry an AudioListener and cause duplicates after scene loads.
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Initialize()
    {
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        if (_impulseSource == null)
        {
            // 動的に追加しておく（後でInspectorからNoise Profile等を入れる想定）
            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        // カメラの初期取得
        FindCameras();
    }

    private void FindCameras()
    {
        var cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            if (cam.gameObject.name == "CN_FollowCam") _followCam = cam;
            else if (cam.gameObject.name == "CN_DirectFollowCam") _directFollowCam = cam;
        }
    }

    /// <summary>
    /// SROptions等からカメラの優先度を切り替える
    /// </summary>
    public void ToggleCamera()
    {
        if (_followCam == null || _directFollowCam == null)
        {
            FindCameras();
        }
        
        if (_followCam == null || _directFollowCam == null)
        {
            Debug.LogWarning("[CameraManager] 切り替え対象のカメラが見つかりません。名前が CN_FollowCam / CN_DirectFollowCam か確認してください。");
            return;
        }

        _isFollowCamActive = !_isFollowCamActive;

        if (_isFollowCamActive)
        {
            _followCam.Priority.Value = 10;
            _directFollowCam.Priority.Value = 0;
            _followCam.Priority.Enabled = true;
            _directFollowCam.Priority.Enabled = true;
            Debug.Log("[CameraManager] カメラ切り替え → CN_FollowCam");
        }
        else
        {
            _followCam.Priority.Value = 0;
            _directFollowCam.Priority.Value = 10;
            _followCam.Priority.Enabled = true;
            _directFollowCam.Priority.Enabled = true;
            Debug.Log("[CameraManager] カメラ切り替え → CN_DirectFollowCam");
        }
    }

    /// <summary>
    /// 現在アクティブなカメラの名前を取得
    /// </summary>
    public string GetActiveCameraName()
    {
        return _isFollowCamActive ? "CN_FollowCam" : "CN_DirectFollowCam";
    }

    /// <summary>
    /// 画面揺れ（シェイク）を発生させる
    /// </summary>
    /// <param name="force">揺れの強さ</param>
    public void PlayShake(float force)
    {
        if (_impulseSource != null)
        {
            // デフォルトのインパルスを再生
            _impulseSource.GenerateImpulseWithForce(force);
            Debug.Log($"[CameraManager] PlayShake: {force}");
        }
    }
}
