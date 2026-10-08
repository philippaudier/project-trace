using TRACE.Combat;
using UnityEngine;

namespace TRACE.Skills
{
    internal static class SkillTargeting
    {
        public static Health Find(Transform actor, float range, float halfAngle, LayerMask targets,
            LayerMask obstructions, Collider[] buffer)
        {
            Vector3 origin = actor.position + Vector3.up * 0.9f;
            int count = Physics.OverlapSphereNonAlloc(origin, range, buffer, targets, QueryTriggerInteraction.Ignore);
            if (count == buffer.Length)
            {
                buffer = Physics.OverlapSphere(origin, range, targets, QueryTriggerInteraction.Ignore);
                count = buffer.Length;
            }
            Health best = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var health = buffer[i].GetComponentInParent<Health>();
                if (health == null || !health.isActiveAndEnabled || health.IsDead || health.transform == actor) continue;
                Vector3 direction = health.transform.position - actor.position;
                if (direction.magnitude > range) continue;
                direction.y = 0f;
                float angle = Vector3.Angle(actor.forward, direction);
                if (angle > halfAngle || Physics.Linecast(origin, buffer[i].bounds.center, obstructions, QueryTriggerInteraction.Ignore)) continue;
                float score = angle + direction.magnitude;
                if (score >= bestScore) continue;
                bestScore = score;
                best = health;
            }
            return best;
        }
    }
}
