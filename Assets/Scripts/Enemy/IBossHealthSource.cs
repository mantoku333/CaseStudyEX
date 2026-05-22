using System;

namespace GameName.Enemy
{
    public interface IBossHealthSource
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }

        event Action<int, int> HealthChanged;
        event Action Died;

        void ResetHealthToFull();
    }
}
