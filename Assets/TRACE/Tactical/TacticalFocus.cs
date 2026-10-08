using TRACE.AI;
using TRACE.Characters;
using TRACE.Input;
using UnityEngine;

namespace TRACE.Tactical
{
    // Scene-local owner. Snapshot normal values and never overwrite a newer external time owner.
    [DefaultExecutionOrder(-15)]
    [DisallowMultipleComponent]
    public sealed class TacticalFocus : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private SquadController squad;
        [SerializeField, Range(0.01f, 1f)] private float focusScale = 0.15f;
        [SerializeField, Min(0f)] private float enterDuration = 0.15f;
        [SerializeField, Min(0f)] private float exitDuration = 0.15f;
        private bool ownsTime;
        private bool requireRelease;
        private float normalScale;
        private float normalFixedDelta;
        private float lastScale;
        private float lastFixedDelta;
        private float transitionFrom;
        private float transitionElapsed;
        private SquadMember observedMember;
        public bool IsActive { get; private set; }
        public bool IsRestoring => ownsTime && !IsActive;
        public bool BlocksCombat => isActiveAndEnabled && IsActive;

        private void Update()
        {
            bool held = input != null && input.TacticalFocusHeld;
            if (!held) requireRelease = false;
            if (ownsTime && (!Mathf.Approximately(Time.timeScale, lastScale) ||
                !Mathf.Approximately(Time.fixedDeltaTime, lastFixedDelta)))
            {
                // Pause (or another time effect) has priority. It owns its new values.
                Abort();
                return;
            }
            if (ownsTime && (input == null || !input.GameplayInputEnabled || squad.IsDefeated ||
                observedMember == null || observedMember.Health.IsDead || !observedMember.gameObject.activeInHierarchy))
            {
                Abort();
                return;
            }
            bool desired = held && !requireRelease && squad.ActiveMember != null &&
                !squad.ActiveMember.Health.IsDead && !squad.IsDefeated && Time.timeScale > 0f;
            if (desired != IsActive)
            {
                if (desired && !ownsTime)
                {
                    normalScale = Time.timeScale;
                    normalFixedDelta = Time.fixedDeltaTime;
                    lastScale = normalScale;
                    lastFixedDelta = normalFixedDelta;
                    ownsTime = true;
                }
                IsActive = desired;
                transitionFrom = Time.timeScale;
                transitionElapsed = 0f;
                if (desired) squad.ActiveMember.GetComponent<ThirdPersonMotor>().CancelActionMovement();
            }
            if (!ownsTime) return;
            observedMember = squad.ActiveMember;
            transitionElapsed += Time.unscaledDeltaTime;
            float duration = IsActive ? enterDuration : exitDuration;
            float progress = duration > 0f ? Mathf.Clamp01(transitionElapsed / duration) : 1f;
            float target = IsActive ? normalScale * focusScale : normalScale;
            lastScale = Mathf.Lerp(transitionFrom, target, Mathf.SmoothStep(0f, 1f, progress));
            lastFixedDelta = normalFixedDelta * lastScale / normalScale;
            Time.timeScale = lastScale;
            Time.fixedDeltaTime = lastFixedDelta;
            // Unity quantizes fixedDeltaTime internally. Track the accepted values, not the requested floats.
            lastScale = Time.timeScale;
            lastFixedDelta = Time.fixedDeltaTime;
            if (!IsActive && progress >= 1f) Restore();
        }

        private void Abort()
        {
            Restore();
            requireRelease = true;
        }
        private void Restore()
        {
            if (ownsTime)
            {
                if (Mathf.Approximately(Time.timeScale, lastScale)) Time.timeScale = normalScale;
                if (Mathf.Approximately(Time.fixedDeltaTime, lastFixedDelta)) Time.fixedDeltaTime = normalFixedDelta;
            }
            ownsTime = IsActive = false;
            observedMember = null;
        }
        private void OnDisable() => Abort();
        private void OnApplicationFocus(bool focused) { if (!focused && ownsTime) Abort(); }
        private void OnApplicationPause(bool paused) { if (paused && ownsTime) Abort(); }
    }
}
