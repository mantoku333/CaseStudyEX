#if UNITY_EDITOR
using System.IO;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only record of what the camera system decided each time something changed, written to
/// Logs/CameraTrace.log. It exists because a screen recording shows that the picture and the fog
/// disagree but not which of them is wrong: this logs the active room, the room the fog shows,
/// the live virtual camera and the rendered camera pose on the same frame, after the brain ran.
/// Only runs in scenes whose name contains "CameraRework". Delete once the camera is settled.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class CameraTraceLog : MonoBehaviour
{
    private const int FramesLoggedAfterChange = 4;
    private const float JumpThreshold = 1.5f;

    private StreamWriter writer;
    private string lastKey;
    private Vector3 lastCameraPosition;
    private int framesToLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!SceneManager.GetActiveScene().name.Contains("CameraRework"))
        {
            return;
        }

        GameObject host = new GameObject(nameof(CameraTraceLog));
        DontDestroyOnLoad(host);
        host.AddComponent<CameraTraceLog>();
    }

    private void OnEnable()
    {
        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
        Directory.CreateDirectory(directory);
        writer = new StreamWriter(Path.Combine(directory, "CameraTrace.log"), false) { AutoFlush = true };
        writer.WriteLine($"# {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} scene={SceneManager.GetActiveScene().name}");
        writer.WriteLine("# time frame why | room fog live | cam(x,y,ortho) player(x,y) timeScale encounter");
    }

    private void OnDisable()
    {
        writer?.Dispose();
        writer = null;
    }

    private void LateUpdate()
    {
        Camera main = Camera.main;
        if (writer == null || main == null)
        {
            return;
        }

        RoomCameraTrigger room = RoomCameraTrigger.ActiveRoom;
        RoomCameraTrigger fog = RoomFogRevealManager.CurrentRoomForDiagnostics;
        CinemachineBrain brain = CinemachineBrain.ActiveBrainCount > 0 ? CinemachineBrain.GetActiveBrain(0) : null;
        string live = brain != null && brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "-";
        bool encounter = EncounterCameraOwnership.IsActive;
        string key = $"{Name(room)} {Name(fog)} {live} {encounter}";

        Vector3 position = main.transform.position;
        bool changed = key != lastKey;
        bool jumped = ((Vector2)(position - lastCameraPosition)).magnitude > JumpThreshold;
        if (changed || jumped)
        {
            framesToLog = FramesLoggedAfterChange;
        }

        if (framesToLog > 0)
        {
            string why = changed ? "CHANGE" : jumped ? "JUMP" : "after";
            string mismatch = room != fog ? " FOG!=ROOM" : string.Empty;
            PlayerCameraColliderUtility.TryGetCameraPointFromPlayerTag("Player", out Vector3 player);
            writer.WriteLine(
                $"{Time.realtimeSinceStartup:F3} {Time.frameCount} {why} | {Name(room)} {Name(fog)} {live} | " +
                $"cam({position.x:F2},{position.y:F2},{main.orthographicSize:F2}) " +
                $"player({player.x:F2},{player.y:F2}) ts={Time.timeScale:F2} enc={(encounter ? 1 : 0)}{mismatch}");
            framesToLog--;
        }

        lastKey = key;
        lastCameraPosition = position;
    }

    private static string Name(Object target) => target != null ? target.name : "-";
}
#endif
