using System.Collections.Generic;
using TRACE.Input;
using TRACE.Characters;
using UnityEngine;

namespace TRACE.Combat
{
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class PlayerMeleeAttack : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private GameObject attackVisual;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.01f)] private float damage = 25f;
        [SerializeField, Min(0.1f)] private float range = 1.8f;
        [SerializeField, Min(0.05f)] private float halfWidth = 0.5f;
        [SerializeField, Min(0.05f)] private float halfHeight = 0.6f;
        [SerializeField, Min(0f)] private float attackHeight = 0.9f;
        [SerializeField, Min(0f)] private float windup = 0.08f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.12f;
        [SerializeField, Min(0.01f), Tooltip("Minimum time between attack starts.")]
        private float cooldown = 0.45f;
        [SerializeField, Range(0f, 60f)] private float targetingHalfAngle = 35f;
        [SerializeField, Range(0f, 30f)] private float maximumAimCorrection = 15f;

        private readonly Collider[] overlaps = new Collider[32];
        private readonly HashSet<Health> damagedThisAttack = new HashSet<Health>();
        private Vector3 attackDirection;
        private float activeAt;
        private float endsAt;
        private float readyAt;
        private bool attacking;
        private bool sampledActive;
        private Health preferredTarget;
        private TargetingSystem targeting;
        private float counterMultiplier = 1f;
        private bool counterEvaluated;
        public bool IsAttacking => attacking;
        public Health CurrentTarget { get; private set; }
        public void PreferTarget(Health target) => preferredTarget = target;
        public float ReadyAt => readyAt;
        public void DelayUntil(float time) => readyAt = Mathf.Max(readyAt, time);
        private Vector3 Origin => transform.position + Vector3.up * attackHeight;

        private void Awake()
        {
            if (input == null)
            {
                Debug.LogError("TRACE melee attack requires player input.", this);
                enabled = false;
            }
            if (input != null) targeting = input.GetComponent<TargetingSystem>();
            SetVisual(false);
        }

        private void OnDisable() => CancelAttack();

        private void Update()
        {
            if (!input.CombatInputEnabled || (motor != null && (motor.IsDodging || motor.IsSkillDashing)))
            {
                CancelAttack();
                return;
            }
            if (input.AttackPressed && !attacking && Time.time >= readyAt && Time.timeScale > 0f)
            {
                attackDirection = AssistedDirection();
                damagedThisAttack.Clear();
                activeAt = Time.time + windup;
                endsAt = activeAt + activeDuration;
                readyAt = Time.time + Mathf.Max(cooldown, windup + activeDuration);
                attacking = true;
                sampledActive = false;
                counterMultiplier = 1f;
                counterEvaluated = false;
            }
            if (!attacking || Time.time < activeAt)
                return;

            if (Time.time >= endsAt && sampledActive)
            {
                CancelAttack();
                return;
            }
            // Evaluate at least once if a slow frame crossed the whole active window.
            DetectHits();
            sampledActive = true;
            if (Time.time >= endsAt)
                CancelAttack();
        }

        private Vector3 AssistedDirection()
        {
            Health preferred = preferredTarget;
            preferredTarget = null;
            CurrentTarget = null;
            Vector3 facing = transform.forward;
            Vector3 direction = facing;
            Health lockedTarget = targeting != null ? targeting.LockedWithin(transform, range, obstructionMask) : null;
            if (lockedTarget != null)
            {
                // A reachable locked target wins over the cone scan; the swing may turn up to 90 degrees toward it.
                CurrentTarget = lockedTarget;
                Vector3 toLocked = lockedTarget.transform.position - transform.position;
                toLocked.y = 0f;
                if (toLocked.sqrMagnitude > 0.001f)
                    return Vector3.RotateTowards(facing, toLocked.normalized, 90f * Mathf.Deg2Rad, 0f).normalized;
            }
            float bestAngle = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;
            int count = Physics.OverlapSphereNonAlloc(Origin, range, overlaps, targetMask, QueryTriggerInteraction.Ignore);
            Collider[] candidates = overlaps;
            if (count == overlaps.Length)
            {
                // Do not silently drop targets when many colliders fill the reusable buffer.
                candidates = Physics.OverlapSphere(Origin, range, targetMask, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }
            for (int i = 0; i < count; i++)
            {
                Collider candidate = candidates[i];
                if (!ValidTarget(candidate, out Health health) || !Visible(candidate))
                    continue;
                Vector3 toTarget = candidate.bounds.center - Origin;
                toTarget.y = 0f;
                float angle = Vector3.Angle(facing, toTarget);
                float distance = toTarget.sqrMagnitude;
                if (health == preferred && angle <= targetingHalfAngle)
                {
                    CurrentTarget = health;
                    direction = toTarget.normalized;
                    break;
                }
                if (angle > targetingHalfAngle || (angle > bestAngle + 0.1f) ||
                    (Mathf.Abs(angle - bestAngle) <= 0.1f && distance >= bestDistance))
                    continue;
                bestAngle = angle;
                bestDistance = distance;
                direction = toTarget.normalized;
                CurrentTarget = health;
            }
            // Correct the hit direction, never snap the character or camera rotation.
            return Vector3.RotateTowards(facing, direction, maximumAimCorrection * Mathf.Deg2Rad, 0f).normalized;
        }

        private void DetectHits()
        {
            Quaternion rotation = Quaternion.LookRotation(attackDirection, Vector3.up);
            Vector3 center = Origin + attackDirection * (range * 0.5f);
            Vector3 extents = new Vector3(halfWidth, halfHeight, range * 0.5f);
            if (attackVisual != null)
            {
                attackVisual.transform.SetPositionAndRotation(center, rotation);
                attackVisual.transform.localScale = new Vector3(halfWidth * 2f, 0.04f, range);
            }
            SetVisual(true);
            int count = Physics.OverlapBoxNonAlloc(center, extents, overlaps, rotation, targetMask, QueryTriggerInteraction.Ignore);
            Collider[] candidates = overlaps;
            if (count == overlaps.Length)
            {
                candidates = Physics.OverlapBox(center, extents, rotation, targetMask, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }
            for (int i = 0; i < count; i++)
            {
                Collider candidate = candidates[i];
                if (Vector3.Dot(candidate.bounds.center - Origin, attackDirection) <= 0f ||
                    !ValidTarget(candidate, out Health health) || damagedThisAttack.Contains(health) || !Visible(candidate))
                    continue;
                damagedThisAttack.Add(health);
                if (!ComboDamage.CanHit(health)) continue;
                if (!counterEvaluated)
                {
                    counterMultiplier = ComboDamage.ConsumeProtected(gameObject);
                    counterEvaluated = true;
                }
                ComboDamage.Apply(health, damage * counterMultiplier, transform.position);
            }
        }

        private bool ValidTarget(Collider candidate, out Health health)
        {
            health = candidate.GetComponentInParent<Health>();
            return health != null && health.isActiveAndEnabled && !health.IsDead &&
                !candidate.transform.IsChildOf(transform);
        }

        private bool Visible(Collider candidate) =>
            !Physics.Linecast(Origin, candidate.bounds.center, obstructionMask, QueryTriggerInteraction.Ignore);

        private void CancelAttack()
        {
            attacking = false;
            damagedThisAttack.Clear();
            SetVisual(false);
        }

        private void SetVisual(bool visible)
        {
            if (attackVisual != null)
                attackVisual.SetActive(visible);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 direction = Application.isPlaying && attacking ? attackDirection : transform.forward;
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(Origin + direction * range * 0.5f,
                Quaternion.LookRotation(direction), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, halfHeight * 2f, range));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
