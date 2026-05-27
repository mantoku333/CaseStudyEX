using UnityEngine;

namespace GameName.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SurfaceAudioZone : MonoBehaviour
    {
        [SerializeField] private SurfaceAudioProfile profile;
        [SerializeField] private int priority;

        public SurfaceAudioProfile Profile => profile;
        public int Priority => priority;

#if UNITY_EDITOR
        private void Reset()
        {
            SetColliderAsTrigger();
        }

        private void OnValidate()
        {
            SetColliderAsTrigger();
        }

        private void SetColliderAsTrigger()
        {
            Collider2D zoneCollider = GetComponent<Collider2D>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }
#endif
    }
}
