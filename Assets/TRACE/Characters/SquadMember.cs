using TRACE.AI;
using TRACE.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.Characters
{
    // One ownership boundary for every member: only the motor or the agent may move it.
    [RequireComponent(typeof(Health), typeof(CharacterController), typeof(CompanionController))]
    [DisallowMultipleComponent]
    public sealed class SquadMember : MonoBehaviour
    {
        private Health health;
        private DamageReceiver receiver;
        private CompanionController companion;
        private NavMeshAgent agent;
        private CharacterController body;
        private ThirdPersonMotor motor;
        private PlayerMeleeAttack melee;
        private PlayerRangedAttack ranged;
        public Health Health => health;
        public DamageReceiver Receiver => receiver;
        public CompanionController Companion => companion;
        public bool IsPlayerControlled { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            receiver = GetComponent<DamageReceiver>();
            companion = GetComponent<CompanionController>();
            agent = GetComponent<NavMeshAgent>();
            body = GetComponent<CharacterController>();
            motor = GetComponent<ThirdPersonMotor>();
            melee = GetComponent<PlayerMeleeAttack>();
            ranged = GetComponent<PlayerRangedAttack>();
        }

        private void OnEnable() => health.OnDeath += Die;
        private void OnDisable() { health.OnDeath -= Die; }

        internal void SetPlayerControlled(bool controlled)
        {
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            Health previousTarget = IsPlayerControlled ?
                (ranged != null ? ranged.CurrentTarget : melee.CurrentTarget) : companion.CurrentTarget;
            float readyAt = Mathf.Max(companion.Attack.ReadyAt, melee != null ? melee.ReadyAt : 0f);
            // Cancel both sides before enabling the next owner, including a committed AI strike.
            companion.enabled = false;
            companion.Attack.Cancel();
            if (melee != null) melee.enabled = false;
            if (ranged != null) ranged.enabled = false;
            motor.enabled = false;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            IsPlayerControlled = controlled && !health.IsDead;
            body.enabled = !health.IsDead;
            companion.Attack.DelayUntil(readyAt);
            if (melee != null) melee.DelayUntil(readyAt);
            if (!health.IsDead)
            {
                if (IsPlayerControlled)
                {
                    if (ranged != null) ranged.PreferTarget(previousTarget);
                    if (melee != null) melee.PreferTarget(previousTarget);
                    motor.enabled = true;
                    if (melee != null) melee.enabled = true;
                    if (ranged != null) ranged.enabled = true;
                }
                else
                {
                    agent.updatePosition = false;
                    // Off-mesh players keep their exact position until V0.4 recovery finds safe ground.
                    if (NavMesh.SamplePosition(position, out NavMeshHit hit, 0.5f, agent.areaMask))
                    {
                        agent.enabled = true;
                        if (agent.isOnNavMesh) agent.nextPosition = hit.position;
                    }
                    companion.ResumeFollowing();
                    companion.enabled = true;
                    companion.PreferTarget(previousTarget);
                }
            }
            // Enabling an agent may bind it to nearby ground; ownership transfer itself never moves the actor.
            transform.SetPositionAndRotation(position, rotation);
        }

        private void Die()
        {
            IsPlayerControlled = false;
            motor.enabled = false;
            if (melee != null) melee.enabled = false;
            if (ranged != null) ranged.enabled = false;
            companion.enabled = false;
            companion.Attack.Cancel();
            agent.enabled = false;
            body.enabled = false;
            receiver.ClearInvulnerability();
        }
    }
}
