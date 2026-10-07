using System;
using UnityEngine;

namespace TRACE.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Tooltip("Runtime value, initialized to Max Health in Awake.")]
        private float currentHealth;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;
        public event Action<float> OnDamaged;
        public event Action OnDeath;

        private void Awake()
        {
            maxHealth = IsPositiveFinite(maxHealth) ? maxHealth : 100f;
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (!isActiveAndEnabled || IsDead || !IsPositiveFinite(amount))
                return;
            float applied = Mathf.Min(amount, currentHealth);
            currentHealth -= applied;
            bool died = IsDead;
            OnDamaged?.Invoke(applied);
            // Capture the transition before callbacks, so nested damage cannot emit death twice.
            if (died)
                OnDeath?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!isActiveAndEnabled || IsDead || !IsPositiveFinite(amount))
                return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        private static bool IsPositiveFinite(float amount) =>
            amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);
    }
}
