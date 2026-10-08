using TRACE.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.AI
{
    [RequireComponent(typeof(Health), typeof(DamageReceiver), typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class CompanionController : MonoBehaviour
    {
        public enum CombatRole { Melee, Ranged }
        public enum CompanionState { Follow, Combat, Recall, Dead }

        [SerializeField] private SquadController squad;
        [SerializeField] private CombatRole role;
        [SerializeField] private Vector3 formationOffset = new Vector3(-1.6f, 0f, -2.2f);
        [Header("Follow")]
        [SerializeField, Min(0.1f)] private float followSpeed = 4.2f;
        [SerializeField, Min(0.1f)] private float catchUpSpeed = 7.2f;
        [SerializeField, Min(0.1f)] private float stoppingDistance = 0.45f;
        [SerializeField, Min(0.1f)] private float resumeDistance = 0.85f;
        [SerializeField, Min(0.1f)] private float separationRadius = 1.15f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
        [Header("Combat")]
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.1f)] private float detectionRange = 7f;
        [SerializeField, Min(0.1f)] private float combatLeash = 10f;
        [SerializeField, Min(0.1f)] private float recallDistance = 9f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.65f;
        [SerializeField, Min(0.1f)] private float preferredRange = 1.25f;
        [SerializeField, Min(0.1f)] private float damage = 12f;
        [SerializeField, Min(0.1f)] private float cooldown = 1f;
        [SerializeField, Min(0.01f)] private float windup = 0.25f;
        [SerializeField] private GameObject attackCue;
        [Header("Prototype recovery")]
        [SerializeField, Min(1f)] private float teleportDistance = 18f;
        [SerializeField, Min(0.1f)] private float teleportDelay = 2f;
        [SerializeField, Min(0.1f)] private float stuckDelay = 4f;

        private readonly Collider[] candidates = new Collider[32];
        private NavMeshPath path;
        private NavMeshAgent agent;
        private Health health;
        private DamageReceiver receiver;
        private Health target;
        private Collider targetCollider;
        private float nextScanAt;
        private float nextPathAt;
        private float farTime;
        private float stalledTime;
        private float nextRecoveryAt;
        private Vector3 progressPosition;
        private Vector3 destination;
        private bool moving;
        private bool hasDestination;
        public CompanionState State { get; private set; }
        public CombatRole Role => role;
        public Health Health => health;
        public DamageReceiver Receiver => receiver;
        public Health CurrentTarget => target;
        public Vector3 FormationOffset => formationOffset;
        public Vector3 Destination => destination;
        public int RecoveryCount { get; private set; }
        public TargetedAttack Attack { get; private set; }
        private Vector3 Origin => transform.position + Vector3.up * 0.9f;
        private bool AgentReady => agent.enabled && agent.isOnNavMesh;

        private void Awake()
        {
            health = GetComponent<Health>();
            receiver = GetComponent<DamageReceiver>();
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
            agent.stoppingDistance = stoppingDistance;
            progressPosition = transform.position;
            // Add lazily for previously serialized V0.4 scenes; V0.5 authors this component explicitly.
            Attack = GetComponent<TargetedAttack>();
            if (Attack == null) Attack = gameObject.AddComponent<TargetedAttack>();
            Attack.Configure(role == CombatRole.Ranged, damage, cooldown, windup, attackRange, attackCue, obstructionMask);
            if (squad == null)
            {
                Debug.LogError("TRACE companion requires an explicit squad.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += Die;
            if (squad != null) squad.IgnoreMemberCollisions(this);
        }
        private void OnDisable()
        {
            if (health != null) health.OnDeath -= Die;
            CancelCombat();
            Stop();
        }

        private void Update()
        {
            // Rejoin navigation on the frame after a transfer, never inside SwitchToMember.
            if (AgentReady && !agent.updatePosition) agent.updatePosition = true;
            if (health.IsDead) { if (State != CompanionState.Dead) Die(); return; }
            if (!squad.LeaderAlive) { CancelCombat(); Stop(); return; }
            float leaderDistance = Vector3.Distance(transform.position, squad.Leader.transform.position);
            if (RecoverIfNecessary(leaderDistance)) return;
            if (leaderDistance > recallDistance)
            {
                CancelCombat();
                State = CompanionState.Recall;
            }
            if (State == CompanionState.Recall)
            {
                if (leaderDistance > 4.5f) { Follow(); return; }
                State = CompanionState.Follow;
            }
            if (!TargetValid()) CancelCombat();
            if (target == null && Time.time >= nextScanAt)
            {
                nextScanAt = Time.time + 0.25f;
                FindTarget();
            }
            if (target != null) Fight();
            else { State = CompanionState.Follow; Follow(); }
        }

        private void Follow()
        {
            Vector3 desired = squad.SeparateDestination(this, squad.FormationPosition(formationOffset), separationRadius);
            float distance = Vector3.Distance(transform.position, desired);
            agent.speed = distance > 4f ? catchUpSpeed : followSpeed;
            // Two thresholds prevent alternating start/stop commands around the formation slot.
            if (distance <= stoppingDistance || (!moving && distance < resumeDistance)) { Stop(); return; }
            Navigate(desired);
        }

        private void FindTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(Origin, detectionRange, candidates, enemyMask, QueryTriggerInteraction.Ignore);
            Collider[] found = candidates;
            if (count == candidates.Length)
            {
                found = Physics.OverlapSphere(Origin, detectionRange, enemyMask, QueryTriggerInteraction.Ignore);
                count = found.Length;
            }
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var enemy = found[i].GetComponentInParent<EnemyBrain>();
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                Health candidate = enemy.GetComponent<Health>();
                if (candidate.IsDead || Vector3.Distance(candidate.transform.position, squad.Leader.transform.position) > combatLeash ||
                    !Visible(found[i]) || !Reachable(candidate.transform.position)) continue;
                float score = Vector3.Distance(transform.position, candidate.transform.position);
                if (enemy.CurrentTarget == squad.Leader && enemy.IsEngaged) score -= 20f;
                if (score >= bestScore) continue;
                bestScore = score;
                target = candidate;
                targetCollider = found[i];
            }
            // Keep this target until death, deactivation, leash break or failed navigation.
        }

        private bool TargetValid() => target != null && target.isActiveAndEnabled && !target.IsDead &&
            targetCollider != null && targetCollider.enabled &&
            Vector3.Distance(target.transform.position, squad.Leader.transform.position) <= combatLeash;

        private void Fight()
        {
            State = CompanionState.Combat;
            agent.speed = followSpeed;
            Vector3 toTarget = target.transform.position - transform.position;
            float distance = toTarget.magnitude;
            toTarget.y = 0f;
            if (Attack.IsWindingUp) return;
            Vector3 separated = squad.SeparateDestination(this, transform.position, separationRadius);
            if ((separated - transform.position).sqrMagnitude > 0.04f)
            {
                Navigate(separated);
                return;
            }
            if (role == CombatRole.Ranged && distance < preferredRange - 1f)
            {
                Vector3 away = toTarget.sqrMagnitude > 0.01f ? -toTarget.normalized : transform.right;
                Navigate(squad.SeparateDestination(this, target.transform.position + away * preferredRange, separationRadius));
                return;
            }
            if (distance <= attackRange && Visible(targetCollider))
            {
                Stop();
                if (toTarget.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), 540f * Time.deltaTime);
                Attack.TryBegin(target, targetCollider);
                return;
            }
            Vector3 side = (squad.FormationPosition(formationOffset) - target.transform.position);
            side.y = 0f;
            if (side.sqrMagnitude < 0.01f) side = transform.right;
            Navigate(squad.SeparateDestination(this, target.transform.position + side.normalized * preferredRange, separationRadius));
        }

        private void Navigate(Vector3 desired)
        {
            moving = true;
            if (!AgentReady || Time.time < nextPathAt) return;
            nextPathAt = Time.time + repathInterval;
            if (!NavMesh.SamplePosition(desired, out NavMeshHit sample, 1.5f, agent.areaMask)) return;
            if (hasDestination && (destination - sample.position).sqrMagnitude < 0.16f && agent.hasPath &&
                agent.pathStatus == NavMeshPathStatus.PathComplete) return;
            destination = sample.position;
            hasDestination = true;
            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        private bool Reachable(Vector3 point) => AgentReady &&
            NavMesh.SamplePosition(point, out NavMeshHit sample, 1.5f, agent.areaMask) &&
            agent.CalculatePath(sample.position, path) && path.status == NavMeshPathStatus.PathComplete;

        private bool Visible(Collider body) => !Physics.Linecast(Origin, body.bounds.center, obstructionMask, QueryTriggerInteraction.Ignore);

        private bool RecoverIfNecessary(float leaderDistance)
        {
            farTime = leaderDistance > teleportDistance ? farTime + Time.deltaTime : 0f;
            if (!AgentReady || (moving && Vector3.Distance(transform.position, progressPosition) < 0.25f))
                stalledTime += Time.deltaTime;
            else { stalledTime = 0f; progressPosition = transform.position; }
            if ((farTime < teleportDelay && stalledTime < stuckDelay) || Time.time < nextRecoveryAt) return false;
            nextRecoveryAt = Time.time + 1f;
            // Search small safe slots; never teleport into walls, onto another member or across floors.
            Vector3 slot = squad.FormationPosition(formationOffset);
            for (int i = 0; i < 17; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 desired = i == 0 ? slot : slot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (i <= 8 ? 1.5f : 3f);
                if (!NavMesh.SamplePosition(desired, out NavMeshHit sample, 1.2f, agent.areaMask) ||
                    Mathf.Abs(sample.position.y - squad.Leader.transform.position.y) > 1.5f ||
                    !squad.IsRecoverySpaceFree(this, sample.position) ||
                    Physics.CheckCapsule(sample.position + Vector3.up * 0.5f, sample.position + Vector3.up * 1.4f,
                        0.4f, obstructionMask, QueryTriggerInteraction.Ignore)) continue;
                if (!NavMesh.SamplePosition(squad.Leader.transform.position, out NavMeshHit leaderPoint, 2f, agent.areaMask) ||
                    !NavMesh.CalculatePath(sample.position, leaderPoint.position, agent.areaMask, path) ||
                    path.status != NavMeshPathStatus.PathComplete) continue;
                if (!agent.enabled)
                {
                    // The previous player may have left the NavMesh. This is the delayed safety only.
                    transform.position = sample.position;
                    agent.enabled = true;
                }
                if (!agent.Warp(sample.position)) continue;
                agent.updatePosition = true;
                CancelCombat();
                Stop();
                State = CompanionState.Follow;
                farTime = stalledTime = 0f;
                progressPosition = transform.position;
                RecoveryCount++;
                return true;
            }
            return false;
        }

        private void Stop()
        {
            if (AgentReady && (moving || agent.hasPath || !agent.isStopped))
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
            moving = false;
            hasDestination = false;
        }

        private void CancelCombat()
        {
            target = null;
            targetCollider = null;
            if (Attack != null) Attack.Cancel();
        }

        private void Die()
        {
            CancelCombat();
            Stop();
            State = CompanionState.Dead;
            agent.enabled = false;
            GetComponent<Collider>().enabled = false;
        }

        public void SetFormationOffset(Vector3 offset) => formationOffset = offset;

        public void PreferTarget(Health preferred)
        {
            if (preferred == null || !preferred.isActiveAndEnabled || preferred.IsDead) return;
            var enemy = preferred.GetComponent<EnemyBrain>();
            var body = preferred.GetComponent<Collider>();
            if (enemy == null || !enemy.isActiveAndEnabled || body == null || !body.enabled ||
                Vector3.Distance(preferred.transform.position, squad.Leader.transform.position) > combatLeash ||
                !Visible(body) || !Reachable(preferred.transform.position)) return;
            target = preferred;
            targetCollider = body;
        }

        public void ResumeFollowing()
        {
            CancelCombat();
            State = CompanionState.Follow;
            farTime = stalledTime = 0f;
            nextScanAt = nextPathAt = nextRecoveryAt = 0f;
            progressPosition = transform.position;
            moving = hasDestination = false;
        }

        private void OnDrawGizmos()
        {
            if (squad == null || !squad.ShowDebug) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, recallDistance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            if (target != null) Gizmos.DrawLine(Origin, target.transform.position + Vector3.up);
            if (Application.isPlaying && hasDestination)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, destination);
                Gizmos.DrawWireSphere(destination, stoppingDistance);
            }
        }
    }
}
