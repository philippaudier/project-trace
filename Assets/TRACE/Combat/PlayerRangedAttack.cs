using TRACE.AI;
using TRACE.Characters;
using TRACE.Input;
using UnityEngine;

namespace TRACE.Combat
{
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class PlayerRangedAttack : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private CompanionController companion;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Range(0f, 60f)] private float targetingHalfAngle = 35f;
        private readonly Collider[] overlaps = new Collider[32];
        private TargetedAttack attack;
        private TargetingSystem targeting;
        public Health CurrentTarget { get; private set; }

        private void Start() { attack = companion.Attack; targeting = input.GetComponent<TargetingSystem>(); }
        private void OnDisable() { if (attack != null) attack.Cancel(); }
        public void PreferTarget(Health target) => CurrentTarget = target;

        private void Update()
        {
            if (attack == null) attack = companion.Attack;
            if (!input.CombatInputEnabled || motor.IsDodging || motor.IsSkillDashing) { attack.Cancel(); return; }
            if (CurrentTarget != null && (!CurrentTarget.isActiveAndEnabled || CurrentTarget.IsDead)) CurrentTarget = null;
            if (!input.AttackPressed) return;
            Health lockedTarget = targeting != null ? targeting.LockedWithin(transform, attack.Range, obstructionMask) : null;
            Collider body = CurrentTarget != null ? CurrentTarget.GetComponent<Collider>() : null;
            if (lockedTarget != null)
            {
                // Lock beats the aim cone: the shot goes to the chosen target whatever the facing.
                CurrentTarget = lockedTarget;
                body = lockedTarget.GetComponent<Collider>();
            }
            else if (!Valid(body, out _))
            {
                CurrentTarget = null;
                body = null;
                int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.9f,
                    attack.Range, overlaps, targetMask, QueryTriggerInteraction.Ignore);
                Collider[] found = overlaps;
                if (count == overlaps.Length)
                {
                    found = Physics.OverlapSphere(transform.position + Vector3.up * 0.9f,
                        attack.Range, targetMask, QueryTriggerInteraction.Ignore);
                    count = found.Length;
                }
                float best = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (!Valid(found[i], out Health candidate)) continue;
                    float score = Vector3.Angle(transform.forward, candidate.transform.position - transform.position);
                    if (score >= best) continue;
                    best = score;
                    CurrentTarget = candidate;
                    body = found[i];
                }
            }
            if (CurrentTarget != null) attack.TryBegin(CurrentTarget, body, true);
        }

        private bool Valid(Collider body, out Health health)
        {
            health = body != null ? body.GetComponentInParent<Health>() : null;
            if (health == null || !health.isActiveAndEnabled || health.IsDead || !body.enabled ||
                (targetMask.value & (1 << body.gameObject.layer)) == 0) return false;
            Vector3 direction = health.transform.position - transform.position;
            if (direction.magnitude > attack.Range + 0.15f) return false;
            direction.y = 0f;
            return Vector3.Angle(transform.forward, direction) <= targetingHalfAngle &&
                !Physics.Linecast(transform.position + Vector3.up * 0.9f, body.bounds.center,
                    obstructionMask, QueryTriggerInteraction.Ignore);
        }
    }
}
