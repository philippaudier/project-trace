using System.Collections.Generic;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Input;
using UnityEngine;

namespace TRACE.Combat
{
    // Squad-level manual lock that complements soft targeting. Gameplay asks LockedWithin(...) and falls back to its
    // own soft target when the answer is null. No generic targeting framework, no ADS.
    [DefaultExecutionOrder(-8)]
    [DisallowMultipleComponent]
    public sealed class TargetingSystem : MonoBehaviour
    {
        public struct Candidate
        {
            public Health Health;
            public float Score;
            public Vector2 Viewport;
            public float Distance;
            public bool Visible;
        }

        [SerializeField] private TracePlayerInput input;
        [SerializeField] private SquadController squad;
        [SerializeField] private Transform marker;
        [SerializeField] private LayerMask candidateMask;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(1f)] private float maxLockDistance = 22f;
        [SerializeField, Min(0f)] private float lostSightGracePeriod = 1.25f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Viewport margin: candidates slightly off screen still count.")]
        private float screenMargin = 0.12f;
        [SerializeField, Min(0f)] private float centerWeight = 1f;
        [SerializeField, Min(0f)] private float distanceWeight = 0.5f;
        [SerializeField, Range(0.1f, 1f)] private float stickSwitchThreshold = 0.6f;
        [SerializeField, Min(0f)] private float switchCooldown = 0.3f;
        [SerializeField] private bool showDebug;
        private readonly Collider[] overlaps = new Collider[48];
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly HashSet<Health> seen = new HashSet<Health>();
        private UnityEngine.Camera view;
        private Health locked;
        private Collider lockedBody;
        private float lastSeenAt;
        private bool stickArmed = true;
        private float nextSwitchAt;
        private ThirdPersonMotor facingMotor;
        public float MaxLockDistance => maxLockDistance;
        public float LostSightGracePeriod => lostSightGracePeriod;
        public bool IsLocked => locked != null && locked.isActiveAndEnabled && !locked.IsDead && locked.gameObject.activeInHierarchy;
        public Health LockedTarget => IsLocked ? locked : null;
        public float LostSightTime => IsLocked ? Time.time - lastSeenAt : 0f;
        public IReadOnlyList<Candidate> LastCandidates => candidates;
        public int LockCount { get; private set; }
        public int SwitchCount { get; private set; }
        public string LastReleaseReason { get; private set; } = "";
        private Transform Actor => squad != null && squad.ActiveMember != null ? squad.ActiveMember.transform : null;
        private static Vector3 Eye(Transform actor) => actor.position + Vector3.up * 1.4f;

        private void Awake()
        {
            view = UnityEngine.Camera.main;
            if (input == null || squad == null)
            {
                Debug.LogError("TRACE targeting requires the squad input and controller.", this);
                enabled = false;
            }
            if (marker != null) marker.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Release("disabled");
        }

        private void Update()
        {
            if (view == null) view = UnityEngine.Camera.main;
            ValidateLock();
            if (input.GameplayInputEnabled)
            {
                if (input.TargetLockPressed)
                {
                    if (IsLocked) Release("toggle");
                    else TryLock();
                }
                int wheel = input.TargetSwitchDirection;
                if (wheel != 0) SwitchTarget(wheel);
                // Controller: one clear right-stick impulse per switch, re-armed below the threshold.
                float stick = input.StickLook.x;
                if (Mathf.Abs(stick) < stickSwitchThreshold * 0.6f) stickArmed = true;
                else if (stickArmed && Mathf.Abs(stick) >= stickSwitchThreshold && Time.unscaledTime >= nextSwitchAt)
                {
                    stickArmed = false;
                    nextSwitchAt = Time.unscaledTime + switchCooldown;
                    SwitchTarget(stick > 0f ? 1 : -1);
                }
            }
            UpdateFacing();
        }

        private void LateUpdate()
        {
            if (marker == null) return;
            bool show = IsLocked;
            marker.gameObject.SetActive(show);
            if (!show) return;
            Vector3 center = lockedBody != null ? lockedBody.bounds.center : locked.transform.position + Vector3.up * 0.9f;
            // Billboard slightly toward the camera so the body itself never hides the diamond.
            marker.position = center + Vector3.up * 0.15f - (view != null ? view.transform.forward * 0.7f : Vector3.zero);
            if (view != null) marker.rotation = view.transform.rotation;
            marker.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f);
        }

        private void ValidateLock()
        {
            if (locked == null) return;
            if (!IsLocked) { Release("invalid"); return; }
            Transform actor = Actor;
            if (actor == null) { Release("no actor"); return; }
            if (Vector3.Distance(actor.position, locked.transform.position) > maxLockDistance * 1.1f) { Release("range"); return; }
            if (HasLineOfSight(actor, lockedBody)) lastSeenAt = Time.time;
            else if (Time.time - lastSeenAt > lostSightGracePeriod) Release("sight");
        }

        private void UpdateFacing()
        {
            var member = squad.ActiveMember;
            var motor = member != null ? member.GetComponent<ThirdPersonMotor>() : null;
            if (facingMotor != null && facingMotor != motor) facingMotor.FacingTarget = null;
            facingMotor = motor;
            if (motor != null) motor.FacingTarget = IsLocked ? locked.transform : null;
        }

        public bool TryLock()
        {
            Transform actor = Actor;
            if (actor == null || view == null) return false;
            Collect(actor);
            Health best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (!candidate.Visible || candidate.Score >= bestScore) continue;
                bestScore = candidate.Score;
                best = candidate.Health;
            }
            if (best == null) return false;
            LockTarget(best);
            return true;
        }

        // Explicit lock for tests, debug tools and future UI; applies the same validity rules.
        public bool LockTarget(Health target)
        {
            if (target == null || !target.isActiveAndEnabled || target.IsDead || !target.gameObject.activeInHierarchy ||
                target.GetComponent<SquadMember>() != null) return false;
            locked = target;
            lockedBody = target.GetComponent<Collider>();
            lastSeenAt = Time.time;
            LockCount++;
            UpdateFacing();
            return true;
        }

        public void Unlock() => Release("manual");

        private void Release(string reason)
        {
            if (locked == null) return;
            locked = null;
            lockedBody = null;
            LastReleaseReason = reason;
            if (facingMotor != null) facingMotor.FacingTarget = null;
        }

        // Screen-space neighbour on the requested side of the current target; never an arbitrary list walk.
        public bool SwitchTarget(int direction)
        {
            Transform actor = Actor;
            if (!IsLocked || direction == 0 || actor == null || view == null) return false;
            Collect(actor);
            Vector3 current = view.WorldToViewportPoint(lockedBody != null ? lockedBody.bounds.center : locked.transform.position);
            Health best = null;
            float bestCost = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (!candidate.Visible || candidate.Health == locked) continue;
                float dx = candidate.Viewport.x - current.x;
                if (Mathf.Sign(dx) != Mathf.Sign(direction) || Mathf.Abs(dx) < 0.02f) continue;
                float cost = Mathf.Abs(dx) + Mathf.Abs(candidate.Viewport.y - current.y) * 0.5f;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = candidate.Health;
            }
            if (best == null) return false;
            LockTarget(best);
            SwitchCount++;
            return true;
        }

        // The locked target when it is a usable target for this actor; null lets soft targeting decide.
        public Health LockedWithin(Transform actor, float range, LayerMask obstructions)
        {
            if (!IsLocked || actor == null) return null;
            if (Vector3.Distance(actor.position, locked.transform.position) > range + 0.15f) return null;
            Vector3 end = lockedBody != null ? lockedBody.bounds.center : locked.transform.position + Vector3.up * 0.9f;
            if (Physics.Linecast(actor.position + Vector3.up * 0.9f, end, obstructions, QueryTriggerInteraction.Ignore)) return null;
            return locked;
        }

        private void Collect(Transform actor)
        {
            candidates.Clear();
            seen.Clear();
            int count = Physics.OverlapSphereNonAlloc(actor.position, maxLockDistance, overlaps, candidateMask, QueryTriggerInteraction.Ignore);
            Collider[] found = overlaps;
            if (count == overlaps.Length)
            {
                found = Physics.OverlapSphere(actor.position, maxLockDistance, candidateMask, QueryTriggerInteraction.Ignore);
                count = found.Length;
            }
            for (int i = 0; i < count; i++)
            {
                var health = found[i].GetComponentInParent<Health>();
                if (health == null || seen.Contains(health) || !health.isActiveAndEnabled || health.IsDead ||
                    health.GetComponent<SquadMember>() != null) continue;
                seen.Add(health);
                float distance = Vector3.Distance(actor.position, health.transform.position);
                if (distance > maxLockDistance) continue;
                Vector3 viewport = view.WorldToViewportPoint(found[i].bounds.center);
                if (viewport.z <= 0f || viewport.x < -screenMargin || viewport.x > 1f + screenMargin ||
                    viewport.y < -screenMargin || viewport.y > 1f + screenMargin) continue;
                bool visible = HasLineOfSight(actor, found[i]);
                // Lower is better: screen-center proximity first, then distance; hidden targets are never acquired.
                float score = centerWeight * Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f)) / 0.5f +
                    distanceWeight * distance / maxLockDistance + (visible ? 0f : 10f);
                candidates.Add(new Candidate { Health = health, Score = score, Viewport = viewport, Distance = distance, Visible = visible });
            }
        }

        private bool HasLineOfSight(Transform actor, Collider body)
        {
            Vector3 end = body != null ? body.bounds.center : (locked != null ? locked.transform.position + Vector3.up * 0.9f : actor.position);
            return !Physics.Linecast(Eye(actor), end, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmos()
        {
            if (!showDebug || !Application.isPlaying) return;
            Transform actor = Actor;
            if (actor == null) return;
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(actor.position, maxLockDistance);
            foreach (var candidate in candidates)
            {
                if (candidate.Health == null) continue;
                Gizmos.color = candidate.Visible ? Color.green : Color.red;
                Gizmos.DrawLine(Eye(actor), candidate.Health.transform.position + Vector3.up * 0.9f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(candidate.Health.transform.position + Vector3.up * 2.4f, $"{candidate.Score:0.00}");
#endif
            }
            if (IsLocked)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(locked.transform.position + Vector3.up * 0.9f, 0.6f);
                Gizmos.DrawLine(actor.position + Vector3.up, locked.transform.position + Vector3.up);
            }
        }
    }
}
