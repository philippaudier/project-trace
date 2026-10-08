using System.Collections.Generic;
using TRACE.Combat;
using UnityEngine;

namespace TRACE.Skills
{
    public sealed class DashStrike : CharacterSkill
    {
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.1f)] private float targetRange = 6f;
        [SerializeField, Range(0f, 90f)] private float targetingHalfAngle = 45f;
        [SerializeField, Min(0.1f)] private float damage = 35f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float untargetedDistance = 3f;
        [SerializeField, Min(0.1f)] private float arrivalRange = 1.5f;
        [SerializeField] private GameObject dashVisual;
        [Header("V0.7 Grouped combo")]
        [SerializeField, Min(1f)] private float groupedDamageMultiplier = 1.5f;
        [SerializeField, Min(0.1f)] private float comboRadius = 2f;
        [SerializeField, Range(0f, 1f)] private float splashDamageFraction = 0.5f;
        private readonly Collider[] splash = new Collider[32];
        private readonly HashSet<Health> struck = new HashSet<Health>();
        private readonly Collider[] candidates = new Collider[32];
        private Health target;
        public override string SkillName => "Dash Strike";

        private void OnEnable() => Motor.SkillDashCompleted += Strike;
        private void OnDisable()
        {
            Motor.SkillDashCompleted -= Strike;
            target = null;
            if (dashVisual != null) dashVisual.SetActive(false);
        }

        protected override bool Use()
        {
            target = Targeting != null ? Targeting.LockedWithin(transform, targetRange, obstructionMask) : null;
            if (target == null) target = SkillTargeting.Find(transform, targetRange, targetingHalfAngle, targetMask, obstructionMask, candidates);
            Vector3 displacement;
            if (target != null)
            {
                displacement = target.transform.position - transform.position;
                displacement.y = 0f;
                displacement = displacement.normalized * Mathf.Max(0f, displacement.magnitude - 0.9f);
            }
            else displacement = Motor.MovementDirection * untargetedDistance;
            return Motor.TryBeginSkillDash(displacement, dashDuration);
        }

        private void LateUpdate()
        {
            if (dashVisual != null) dashVisual.SetActive(Motor.IsSkillDashing);
            if (!Motor.IsSkillDashing) target = null;
        }

        private void Strike()
        {
            if (!Member.IsPlayerControlled || Member.Health.IsDead || target == null || !target.isActiveAndEnabled || target.IsDead) return;
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            if (Vector3.Distance(transform.position, target.transform.position) > arrivalRange ||
                Physics.Linecast(origin, target.transform.position + Vector3.up * 0.9f, obstructionMask, QueryTriggerInteraction.Ignore)) return;
            if (!ComboDamage.CanHit(target)) return;
            Vector3 center = target.transform.position;
            var opportunity = target.GetComponent<ComboOpportunity>();
            bool grouped = opportunity != null && opportunity.TryConsume(ComboOpportunityType.Grouped);
            // Grouped takes priority: do not stack the two synergies into a three-character combo.
            float multiplier = grouped ? groupedDamageMultiplier : ComboDamage.ConsumeProtected(gameObject);
            ComboDamage.Apply(target, damage * multiplier, transform.position);
            if (grouped) Splash(center, target);
            // EnemyDummy's existing hit recoil provides the visual stagger without moving its agent.
        }

        private void Splash(Vector3 center, Health primary)
        {
            struck.Clear();
            struck.Add(primary);
            int count = Physics.OverlapSphereNonAlloc(center + Vector3.up * 0.9f, comboRadius, splash, targetMask, QueryTriggerInteraction.Ignore);
            Collider[] found = splash;
            if (count == splash.Length)
            {
                found = Physics.OverlapSphere(center + Vector3.up * 0.9f, comboRadius, targetMask, QueryTriggerInteraction.Ignore);
                count = found.Length;
            }
            for (int i = 0; i < count; i++)
            {
                var victim = found[i].GetComponentInParent<Health>();
                if (victim == null || struck.Contains(victim) || !ComboDamage.CanHit(victim) ||
                    Vector3.Distance(center, victim.transform.position) > comboRadius ||
                    Physics.Linecast(center + Vector3.up * 0.9f, found[i].bounds.center, obstructionMask, QueryTriggerInteraction.Ignore)) continue;
                struck.Add(victim);
                ComboDamage.Apply(victim, damage * splashDamageFraction, transform.position);
            }
        }
    }
}
