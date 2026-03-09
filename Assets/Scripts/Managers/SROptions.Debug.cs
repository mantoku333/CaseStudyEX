using System.ComponentModel;
using Metroidvania.Player;
using UnityEngine;

public partial class SROptions
{
    [Category("Debug")]
    [DisplayName("Reset Player Position (0,0,0)")]
    [Sort(-100)]
    public void ResetPlayerPositionToOrigin()
    {
        var player = Object.FindFirstObjectByType<PlayerPlatformerMockController>();
        if (player == null)
        {
            Debug.LogWarning("[SROptions] PlayerPlatformerMockController not found.");
            return;
        }

        player.transform.position = Vector3.zero;

        var rigidbody2D = player.GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
        }
    }
}
