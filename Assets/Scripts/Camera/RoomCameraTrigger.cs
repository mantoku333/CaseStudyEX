using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Activates a room camera when the player enters this trigger.
/// Corridor triggers can instead release back to the default follow camera.
/// </summary>
public class RoomCameraTrigger : MonoBehaviour
{
    private static RoomCameraTrigger _activeTrigger;
    private static readonly List<RoomCameraTrigger> _occupiedRoomTriggers = new();
    private static int _defaultTriggerOverlapCount;

    public static event Action<RoomCameraTrigger> ActiveRoomChanged;

    [Header("Camera Settings")]
    [SerializeField]
    private CinemachineCamera _roomCamera;

    [SerializeField]
    private bool _useDefaultCameraWhenEntered;

    [SerializeField]
    private int _activePriority = 20;

    [SerializeField]
    private int _inactivePriority = 0;

    [Header("Detection Settings")]
    [SerializeField]
    private string _playerTag = "Player";

    private int _overlapCount;

    private bool IsDefaultTrigger => _useDefaultCameraWhenEntered;
    private bool HasRoomCamera => _roomCamera != null;

    public static RoomCameraTrigger ActiveRoom => _activeTrigger;
    public bool UsesDefaultCameraWhenEntered => IsDefaultTrigger;
    public bool HasAssignedRoomCamera => HasRoomCamera;

    private void Awake()
    {
        if (!IsDefaultTrigger && !HasRoomCamera)
        {
            Debug.LogWarning($"[{gameObject.name}] RoomCameraTrigger has no CinemachineCamera assigned.", this);
            return;
        }

        DeactivateOwnCamera();
    }

    private void OnDisable()
    {
        if (_overlapCount > 0)
        {
            if (IsDefaultTrigger)
            {
                _defaultTriggerOverlapCount = Mathf.Max(0, _defaultTriggerOverlapCount - 1);
            }
            else
            {
                _occupiedRoomTriggers.Remove(this);
            }
        }

        if (_activeTrigger == this)
        {
            _activeTrigger = null;
            NotifyActiveRoomChanged();
        }

        _overlapCount = 0;
        DeactivateOwnCamera();

        if (_defaultTriggerOverlapCount == 0)
        {
            ActivateBestAvailableRoomTrigger();
        }
    }

    public void ActivateCamera()
    {
        if (IsDefaultTrigger)
        {
            ReleaseToDefaultCamera();
            return;
        }

        if (!HasRoomCamera)
        {
            return;
        }

        if (_activeTrigger != null && _activeTrigger != this)
        {
            _activeTrigger.DeactivateOwnCamera();
        }

        if (_activeTrigger != this)
        {
            _activeTrigger = this;
            NotifyActiveRoomChanged();
        }

        _roomCamera.Priority.Value = _activePriority;
        _roomCamera.Priority.Enabled = true;
    }

    public void DeactivateCamera()
    {
        if (IsDefaultTrigger)
        {
            return;
        }

        if (_activeTrigger == this)
        {
            _activeTrigger = null;
            NotifyActiveRoomChanged();
        }

        DeactivateOwnCamera();
    }

    private void DeactivateOwnCamera()
    {
        if (!HasRoomCamera)
        {
            return;
        }

        _roomCamera.Priority.Value = _inactivePriority;
        _roomCamera.Priority.Enabled = true;
    }

    private static void ReleaseToDefaultCamera()
    {
        if (_activeTrigger == null)
        {
            return;
        }

        RoomCameraTrigger previousTrigger = _activeTrigger;
        _activeTrigger = null;
        NotifyActiveRoomChanged();
        previousTrigger.DeactivateOwnCamera();
    }

    private static void NotifyActiveRoomChanged()
    {
        ActiveRoomChanged?.Invoke(_activeTrigger);
    }

    private static void ActivateBestAvailableRoomTrigger()
    {
        if (_defaultTriggerOverlapCount > 0)
        {
            return;
        }

        for (int i = _occupiedRoomTriggers.Count - 1; i >= 0; i--)
        {
            RoomCameraTrigger trigger = _occupiedRoomTriggers[i];
            if (trigger == null || !trigger.isActiveAndEnabled || !trigger.HasRoomCamera)
            {
                _occupiedRoomTriggers.RemoveAt(i);
                continue;
            }

            trigger.ActivateCamera();
            return;
        }
    }

    private void HandlePlayerEntered()
    {
        _overlapCount++;
        if (_overlapCount != 1)
        {
            return;
        }

        if (IsDefaultTrigger)
        {
            _defaultTriggerOverlapCount++;
            ReleaseToDefaultCamera();
            return;
        }

        if (!HasRoomCamera)
        {
            return;
        }

        _occupiedRoomTriggers.Remove(this);
        _occupiedRoomTriggers.Add(this);

        if (_defaultTriggerOverlapCount == 0)
        {
            ActivateCamera();
        }
    }

    private void HandlePlayerExited()
    {
        if (_overlapCount <= 0)
        {
            return;
        }

        _overlapCount--;
        if (_overlapCount != 0)
        {
            return;
        }

        if (IsDefaultTrigger)
        {
            _defaultTriggerOverlapCount = Mathf.Max(0, _defaultTriggerOverlapCount - 1);
            if (_defaultTriggerOverlapCount == 0)
            {
                ActivateBestAvailableRoomTrigger();
            }

            return;
        }

        _occupiedRoomTriggers.Remove(this);

        if (_activeTrigger == this)
        {
            _activeTrigger = null;
            NotifyActiveRoomChanged();
            DeactivateOwnCamera();
            ActivateBestAvailableRoomTrigger();
        }
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        Vector2 point2D = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D[] colliders2D = GetComponents<Collider2D>();
        for (int i = 0; i < colliders2D.Length; i++)
        {
            Collider2D roomCollider = colliders2D[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.OverlapPoint(point2D))
            {
                return true;
            }
        }

        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider roomCollider = colliders[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.bounds.Contains(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(_playerTag))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(_playerTag))
        {
            HandlePlayerExited();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_playerTag))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_playerTag))
        {
            HandlePlayerExited();
        }
    }
}
