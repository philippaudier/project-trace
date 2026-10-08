using UnityEngine;

namespace TRACE.Combat
{
    public enum ComboOpportunityType { None, Grouped, Protected }

    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class ComboOpportunity : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float protectedDamageMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float rearmDelay = 0.5f;
        private Health health;
        private ComboOpportunityType type;
        private Object source;
        private float expiresAt;
        private float lockedUntil;
        private Object exposureSource;
        private int exposureGeneration;
        private int lastExposureFrame = -1;
        private float exposure;
        private bool issuedForExposure;
        public ComboOpportunityType Type => isActiveAndEnabled && health != null && !health.IsDead && Time.time < expiresAt
            ? type : ComboOpportunityType.None;
        public float RemainingDuration => Type != ComboOpportunityType.None ? Mathf.Max(0f, expiresAt - Time.time) : 0f;
        public Object Source => Type != ComboOpportunityType.None ? source : null;
        public Health Target => health;
        public float ProtectedDamageMultiplier => protectedDamageMultiplier;
        public int ConsumptionCount { get; private set; }
        public float ConsumedAt { get; private set; } = float.NegativeInfinity;
        public ComboOpportunityType LastConsumedType { get; private set; }

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.OnDeath += Clear;
        private void OnDisable() { health.OnDeath -= Clear; Clear(); }
        private void Clear() { type = ComboOpportunityType.None; source = null; expiresAt = 0f; }

        public bool Offer(ComboOpportunityType opportunity, float duration, Object origin)
        {
            if (!isActiveAndEnabled || health.IsDead || opportunity == ComboOpportunityType.None ||
                duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration) ||
                Time.time < lockedUntil || Type != ComboOpportunityType.None) return false;
            type = opportunity;
            source = origin;
            expiresAt = Time.time + duration;
            lockedUntil = expiresAt + rearmDelay;
            return true;
        }

        // A cast can prepare this target only once, even if it stays in or re-enters the field.
        public void ObserveGravity(Object origin, int generation, float dwellTime, float opportunityDuration)
        {
            if (origin != exposureSource || generation != exposureGeneration)
            {
                exposureSource = origin;
                exposureGeneration = generation;
                exposure = 0f;
                lastExposureFrame = -1;
                issuedForExposure = false;
            }
            if (issuedForExposure || lastExposureFrame == Time.frameCount || Time.deltaTime <= 0f) return;
            if (lastExposureFrame != Time.frameCount - 1) exposure = 0f;
            lastExposureFrame = Time.frameCount;
            exposure += Time.deltaTime;
            if (exposure < dwellTime) return;
            issuedForExposure = true;
            Offer(ComboOpportunityType.Grouped, opportunityDuration, origin);
        }

        public bool TryConsume(ComboOpportunityType expected)
        {
            if (expected == ComboOpportunityType.None || Type != expected || Time.timeScale <= 0f) return false;
            LastConsumedType = expected;
            ConsumedAt = Time.time;
            ConsumptionCount++;
            Clear();
            lockedUntil = Time.time + rearmDelay;
            return true;
        }
    }
}
