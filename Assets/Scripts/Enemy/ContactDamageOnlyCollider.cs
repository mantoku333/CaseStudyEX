using UnityEngine;

namespace Metroidvania.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ContactDamageOnlyCollider : MonoBehaviour
    {
    }
}
