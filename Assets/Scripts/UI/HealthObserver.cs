using System;

namespace LetsDash.UI
{
    /// <summary>
    /// Decoupled Observer Pattern channel broadcasting entity health changes.
    /// Allows placing any number of health bars in the scene without hardcoding counts.
    /// </summary>
    public static class HealthObserver
    {
        public static event Action<int, float, float> OnHealthChanged;

        public static void NotifyHealthChanged(int entityID, float currentHP, float maxHP)
        {
            OnHealthChanged?.Invoke(entityID, currentHP, maxHP);
        }
    }
}
