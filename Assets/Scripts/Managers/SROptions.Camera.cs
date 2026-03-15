using System.ComponentModel;
using Unity.Cinemachine;
using UnityEngine;

public partial class SROptions
{
    /// <summary>
    /// 現在アクティブなカメラがFollowCamかどうか（falseならDirectFollowCam）
    /// </summary>
    private bool _isFollowCamActive = true;

    [Category("Camera")]
    [DisplayName("カメラ切り替え (Follow ⇔ DirectFollow)")]
    [Sort(0)]
    public void ToggleCameraPriority()
    {
        // シーン内の全CinemachineCameraから名前で検索
        var cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        CinemachineCamera followCam = null;
        CinemachineCamera directFollowCam = null;

        foreach (var cam in cameras)
        {
            if (cam.gameObject.name == "CN_FollowCam")
            {
                followCam = cam;
            }
            else if (cam.gameObject.name == "CN_DirectFollowCam")
            {
                directFollowCam = cam;
            }
        }

        if (followCam == null || directFollowCam == null)
        {
            Debug.LogWarning(
                $"[SROptions] カメラが見つかりません。" +
                $" CN_FollowCam: {(followCam != null ? "OK" : "見つからない")}" +
                $" CN_DirectFollowCam: {(directFollowCam != null ? "OK" : "見つからない")}");
            return;
        }

        // 切り替え
        _isFollowCamActive = !_isFollowCamActive;

        if (_isFollowCamActive)
        {
            // FollowCamを優先
            followCam.Priority.Value = 10;
            directFollowCam.Priority.Value = 0;
            followCam.Priority.Enabled = true;
            directFollowCam.Priority.Enabled = true;
            Debug.Log("[SROptions] カメラ切り替え → CN_FollowCam");
        }
        else
        {
            // DirectFollowCamを優先
            followCam.Priority.Value = 0;
            directFollowCam.Priority.Value = 10;
            followCam.Priority.Enabled = true;
            directFollowCam.Priority.Enabled = true;
            Debug.Log("[SROptions] カメラ切り替え → CN_DirectFollowCam");
        }
    }

    [Category("Camera")]
    [DisplayName("現在のカメラ")]
    [Sort(1)]
    public string ActiveCameraName => _isFollowCamActive ? "CN_FollowCam" : "CN_DirectFollowCam";
}
