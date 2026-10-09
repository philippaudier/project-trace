using TRACE.Characters;
using TRACE.Input;
using UnityEngine;

namespace TRACE.Skills
{
    // Shared activation rules and per-character clock; effects stay in three concrete components.
    [DefaultExecutionOrder(-5)]
    [RequireComponent(typeof(SquadMember))]
    public abstract class CharacterSkill : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField, Min(0f)] private float cooldown = 5f;
        protected SquadMember Member { get; private set; }
        protected ThirdPersonMotor Motor { get; private set; }
        protected TRACE.Combat.TargetingSystem Targeting { get; private set; }
        private float readyAt;
        public abstract string SkillName { get; }
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public bool IsReady => CooldownRemaining <= 0f;
        public void ResetCooldown() => readyAt = 0f;
        // Raised after a successful use; presentation (puppet gestures, equipment) listens, gameplay does not.
        public event System.Action Activated;

        protected virtual void Awake()
        {
            Member = GetComponent<SquadMember>();
            Motor = GetComponent<ThirdPersonMotor>();
            if (input != null) Targeting = input.GetComponent<TRACE.Combat.TargetingSystem>();
        }

        public bool CanUse() => isActiveAndEnabled && Member.IsPlayerControlled && !Member.Health.IsDead &&
            Motor.isActiveAndEnabled && !Motor.IsDodging && !Motor.IsSkillDashing && IsReady &&
            Time.timeScale > 0f && input != null && input.CombatInputEnabled && !input.DodgePressed;

        public bool Activate()
        {
            if (!CanUse() || !Use()) return false;
            readyAt = Time.time + cooldown;
            Activated?.Invoke();
            return true;
        }

        protected abstract bool Use();
        private void Update() { if (input != null && input.SkillPressed) Activate(); }
    }
}
