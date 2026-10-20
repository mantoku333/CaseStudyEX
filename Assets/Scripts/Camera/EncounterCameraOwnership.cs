using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns Brain selection, never the camera's pose, lens, targets or movement limits.
/// An override also takes precedence over Cinemachine Timeline tracks, which do
/// not participate in the virtual-camera priority queue.
/// </summary>
public static class EncounterCameraOwnership
{
    private sealed class BrainOverride
    {
        public CinemachineBrain Brain;
        public int Id;
        public EntryView From;
        public CinemachineBlendDefinition Blend;
        public double StartedAt;
    }

    // Freeze the rendered view, including any room blend already in progress.
    // The outgoing room/story camera must not keep driving the entry transition.
    private sealed class EntryView : ICinemachineCamera
    {
        public EntryView(CameraState state)
        {
            state.BlendHint &= ~CameraState.BlendHints.FreezeWhenBlendingOut;
            State = state;
        }

        public string Name => "Encounter entry view";
        public string Description => Name;
        public CameraState State { get; }
        public bool IsValid => true;
        public ICinemachineMixer ParentCamera => null;
        public void UpdateCameraState(Vector3 worldUp, float deltaTime) { }
        public void OnCameraActivated(ICinemachineCamera.ActivationEventParams evt) { }
    }

    private static readonly List<BrainOverride> overrides = new();
    private static BossAreaController owner;
    private static CinemachineCamera camera;

    public static bool IsActive => owner != null && camera != null;
    public static bool IsOwner(BossAreaController candidate) => IsActive && owner == candidate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clear();
    }

    public static bool Acquire(BossAreaController candidate, CinemachineCamera encounterCamera)
    {
        if (candidate == null || encounterCamera == null || !encounterCamera.isActiveAndEnabled ||
            (IsActive && owner != candidate))
        {
            return false;
        }

        if (!IsActive)
        {
            Clear();
            owner = candidate;
            camera = encounterCamera;
            RoomCameraTrigger.ClearPortalCameraHold();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        Maintain(candidate);
        return true;
    }

    public static void Maintain(BossAreaController candidate)
    {
        if (!IsOwner(candidate))
        {
            return;
        }

        for (int i = overrides.Count - 1; i >= 0; i--)
        {
            if (overrides[i].Brain == null || !overrides[i].Brain.isActiveAndEnabled)
            {
                if (overrides[i].Brain != null)
                {
                    overrides[i].Brain.ReleaseCameraOverride(overrides[i].Id);
                }
                overrides.RemoveAt(i);
            }
        }

        for (int i = 0; i < CinemachineBrain.ActiveBrainCount; i++)
        {
            CinemachineBrain brain = CinemachineBrain.GetActiveBrain(i);
            if (((uint)brain.ChannelMask & (uint)camera.OutputChannel) == 0)
            {
                continue;
            }

            int index = overrides.FindIndex(entry => entry.Brain == brain);
            BrainOverride entry;
            if (index < 0)
            {
                entry = new BrainOverride { Brain = brain, Id = -1, StartedAt = Time.unscaledTimeAsDouble };
                ICinemachineCamera outgoing = brain.ActiveVirtualCamera;
                if (outgoing != null && outgoing != camera)
                {
                    entry.Blend = CinemachineBlenderSettings.LookupBlend(
                        outgoing, camera, brain.DefaultBlend, brain.CustomBlends, brain);
                    if (entry.Blend.BlendTime > 0f)
                    {
                        entry.From = new EntryView(brain.State);
                    }
                }
                overrides.Add(entry);
            }
            else
            {
                entry = overrides[index];
            }

            float weight = 1f;
            if (entry.From != null)
            {
                // Unscaled time also finishes the handoff when dialogue pauses gameplay.
                // Repeated acquisition/maintenance calls never restart or accelerate it.
                float progress = Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - entry.StartedAt) / entry.Blend.BlendTime);
                weight = Mathf.Clamp01(entry.Blend.BlendCurve.Evaluate(progress));
                if (progress >= 1f)
                {
                    entry.From = null;
                    weight = 1f;
                }
            }

            entry.Id = brain.SetCameraOverride(entry.Id, int.MaxValue, entry.From, camera, weight, Time.deltaTime);
        }
    }

    public static bool Release(BossAreaController candidate)
    {
        if (owner != candidate)
        {
            return false;
        }

        Clear();
        return true;
    }

    private static void Clear()
    {
        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].Brain != null)
            {
                overrides[i].Brain.ReleaseCameraOverride(overrides[i].Id);
            }
        }
        overrides.Clear();
        owner = null;
        camera = null;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (owner != null && owner.gameObject.scene != next)
        {
            owner.ReleaseEncounterCameraOwnership(restoreRoomCamera: false);
        }
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (owner == null || owner.gameObject.scene == scene)
        {
            Clear();
        }
    }
}
