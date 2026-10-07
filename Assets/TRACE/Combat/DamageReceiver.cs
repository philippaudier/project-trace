using UnityEngine;

namespace TRACE.Combat
{
    // Attacks use this optional boundary; Health remains responsible only for HP.
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class DamageReceiver : MonoBehaviour
    {
        private Health health;
        private float invulnerableFrom;
        private float invulnerableUntil;
        public Health Health => health;
        public bool IsInvulnerable => isActiveAndEnabled &&
            Time.time >= invulnerableFrom && Time.time < invulnerableUntil;

        private void Awake() => health = GetComponent<Health>();
        private void OnDisable() => ClearInvulnerability();

        public void GrantInvulnerability(float delay, float duration)
        {
            invulnerableFrom = Time.time + Mathf.Max(0f, delay);
            invulnerableUntil = invulnerableFrom + Mathf.Max(0f, duration);
        }

        public void ClearInvulnerability() => invulnerableUntil = 0f;

        public bool TryTakeDamage(float amount)
        {
            if (!isActiveAndEnabled || health == null || health.IsDead || IsInvulnerable)
                return false;
            float before = health.CurrentHealth;
            health.TakeDamage(amount);
            return health.CurrentHealth < before;
        }
    }
}
