namespace Player
{
    /// <summary>
    /// Exposes player state needed by view components.
    /// </summary>
    public interface IPlayerViewStateProvider
    {
        bool IsGrounded { get; }
        bool IsMoving { get; }
        bool IsGliding { get; }
        bool IsUmbrellaOpen { get; }
        bool IsFacingRight { get; }
        bool IsDodging { get; }
        bool IsParrying { get; }
        bool IsUmbrellaChanging { get; }
        bool IsAttacking { get; }
    }
}
