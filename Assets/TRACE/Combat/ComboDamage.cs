using TRACE.Characters;
using UnityEngine;

namespace TRACE.Combat
{
    // Small shared hit boundary for melee, ranged and dash; no new damage/stat framework.
    internal static class ComboDamage
    {
        public static bool CanHit(Health target)
        {
            if (target == null || !target.isActiveAndEnabled || target.IsDead) return false;
            var receiver = target.GetComponent<DamageReceiver>();
            return receiver == null || (receiver.isActiveAndEnabled && !receiver.IsInvulnerable);
        }

        public static float ConsumeProtected(GameObject attacker)
        {
            var member = attacker.GetComponent<SquadMember>();
            var opportunity = attacker.GetComponent<ComboOpportunity>();
            if (member == null || !member.IsPlayerControlled || member.Health.IsDead || opportunity == null) return 1f;
            return opportunity.TryConsume(ComboOpportunityType.Protected) ? opportunity.ProtectedDamageMultiplier : 1f;
        }

        // origin is the attacker's position: a FrontalGuard reduces hits coming from its guarded cone.
        public static bool Apply(Health target, float damage, Vector3 origin)
        {
            var guard = target.GetComponent<FrontalGuard>();
            if (guard != null) damage = guard.Modify(damage, origin);
            var receiver = target.GetComponent<DamageReceiver>();
            if (receiver != null) return receiver.TryTakeDamage(damage);
            float before = target.CurrentHealth;
            target.TakeDamage(damage);
            return target.CurrentHealth < before;
        }
    }
}
