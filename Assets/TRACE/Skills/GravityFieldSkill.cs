using UnityEngine;
using UnityEngine.AI;

namespace TRACE.Skills
{
    public sealed class GravityFieldSkill : CharacterSkill
    {
        [SerializeField] private GravityField field;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.1f)] private float castRange = 6f;
        [SerializeField, Min(0.1f)] private float forwardDistance = 4f;
        private readonly Collider[] candidates = new Collider[32];
        public override string SkillName => "Gravity Field";
        public GravityField Field => field;

        protected override bool Use()
        {
            var target = Targeting != null ? Targeting.LockedWithin(transform, castRange, obstructionMask) : null;
            if (target == null) target = SkillTargeting.Find(transform, castRange, 55f, targetMask, obstructionMask, candidates);
            Vector3 center = target != null ? target.transform.position : transform.position + transform.forward * forwardDistance;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Linecast(origin, center + Vector3.up * 0.5f, out RaycastHit wall, obstructionMask, QueryTriggerInteraction.Ignore))
                center = wall.point - transform.forward * 0.5f;
            if (field == null || !NavMesh.SamplePosition(center, out NavMeshHit ground, 1.5f, NavMesh.AllAreas)) return false;
            field.Spawn(ground.position);
            return true;
        }
    }
}
