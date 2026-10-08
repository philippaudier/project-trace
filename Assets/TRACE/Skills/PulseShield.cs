using TRACE.AI;
using TRACE.Combat;
using UnityEngine;

namespace TRACE.Skills
{
    public sealed class PulseShield : CharacterSkill
    {
        [SerializeField] private SquadController squad;
        [SerializeField, Min(0.1f)] private float capacity = 30f;
        [SerializeField, Min(0.1f)] private float duration = 5f;
        private Shield[] shields;
        public override string SkillName => "Pulse Shield";

        private void Start()
        {
            shields = new Shield[squad.Members.Count];
            for (int i = 0; i < shields.Length; i++) shields[i] = squad.Members[i].GetComponent<Shield>();
        }

        protected override bool Use()
        {
            if (shields == null) return false;
            foreach (var shield in shields) if (shield != null) shield.Grant(capacity, duration);
            return true;
        }
    }
}
