using UnityEngine;

namespace TRACE.Combat
{
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class Shield : MonoBehaviour
    {
        [SerializeField] private GameObject halo;
        [SerializeField, Min(0.1f)] private float protectedThreshold = 20f;
        [SerializeField, Min(0.1f)] private float protectedDuration = 3f;
        private ComboOpportunity opportunity;
        private float absorbedThisShield;
        private bool opportunityIssued;
        private Health health;
        private float amount;
        private float expiresAt;
        public float CurrentAmount => isActiveAndEnabled && health != null && !health.IsDead && Time.time < expiresAt ? amount : 0f;
        public float RemainingDuration => CurrentAmount > 0f ? Mathf.Max(0f, expiresAt - Time.time) : 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
            opportunity = GetComponent<ComboOpportunity>();
            Clear();
        }
        private void OnDisable() => Clear();

        // Refresh replaces the previous shield; it never stacks or revives a dead member.
        public void Grant(float capacity, float duration)
        {
            if (!isActiveAndEnabled || health.IsDead || capacity <= 0f || duration <= 0f ||
                float.IsNaN(capacity) || float.IsInfinity(capacity) || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            amount = capacity;
            absorbedThisShield = 0f;
            opportunityIssued = false;
            expiresAt = Time.time + duration;
            if (halo != null) halo.SetActive(true);
        }

        public float Absorb(float damage)
        {
            if (damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return damage;
            float absorbed = Mathf.Min(CurrentAmount, damage);
            amount = Mathf.Max(0f, amount - absorbed);
            absorbedThisShield += absorbed;
            if (!opportunityIssued && absorbedThisShield >= protectedThreshold)
            {
                opportunityIssued = true;
                if (opportunity != null) opportunity.Offer(ComboOpportunityType.Protected, protectedDuration, this);
            }
            if (CurrentAmount <= 0f) Clear();
            return damage - absorbed;
        }

        private void Update()
        {
            if (CurrentAmount <= 0f) { Clear(); return; }
            if (halo != null)
                halo.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.04f);
        }

        private void Clear()
        {
            amount = expiresAt = 0f;
            if (halo != null) halo.SetActive(false);
        }
    }
}
