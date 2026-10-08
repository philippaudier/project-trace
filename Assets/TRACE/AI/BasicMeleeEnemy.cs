using TRACE.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.AI
{
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    public sealed class BasicMeleeEnemy : EnemyBrain
    {
        public enum EnemyState { Idle, Chase, Attack, Dead }
        public enum AttackPhase { None, Windup, Active, Recovery }

        [SerializeField] private DamageReceiver target;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private SquadController squad;
        [SerializeField] private GameObject attackCue;
        [SerializeField] private Renderer cueRenderer;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.1f)] private float detectionRange = 7f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 10f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.65f;
        [SerializeField, Min(0.1f)] private float hitRange = 2f;
        [SerializeField, Min(0.1f)] private float hitHalfWidth = 0.7f;
        [SerializeField, Min(0.1f)] private float damage = 20f;
        [SerializeField, Min(0.01f)] private float windup = 0.45f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float recovery = 0.65f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
        [SerializeField] private EnemyState state = EnemyState.Idle;
        [SerializeField, Tooltip("Overlay label: PURSUER by default, BULWARK for the heavy tuning.")]
        private string archetype = "PURSUER";

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly Collider[] hits = new Collider[8];
        private Health health;
        private NavMeshAgent agent;
        private MaterialPropertyBlock cueProperties;
        private Vector3 strikeDirection;
        private float activeAt;
        private float recoveryAt;
        private float attackEndsAt;
        private float nextPathAt;
        private bool strikeConsumed;
        private float nextTargetAt;
        public override DamageReceiver CurrentTarget => target;
        public EnemyState State => state;
        public override string Archetype => archetype;
        public override bool IsEngaged => state != EnemyState.Idle && state != EnemyState.Dead;
        public override bool IsPreparingAttack => Phase == AttackPhase.Windup;
        public override float PreparationRemaining => WindupRemaining;
        public override string StatusLabel => Phase == AttackPhase.Windup ? $"<color=#FFD36A>WIND-UP {WindupRemaining:0.0}s</color>" :
            state == EnemyState.Attack ? "ATTACKING / " + Phase.ToString().ToUpperInvariant() : state.ToString().ToUpperInvariant();
        public AttackPhase Phase { get; private set; }
        public Vector3 StrikeDirection => strikeDirection;
        public float HitRange => hitRange;
        public float HitHalfWidth => hitHalfWidth;
        public float WindupRemaining => Phase == AttackPhase.Windup ? Mathf.Max(0f, activeAt - Time.time) : 0f;
        private Vector3 Origin => transform.position + Vector3.up * 0.9f;

        private void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            agent.stoppingDistance = Mathf.Min(agent.stoppingDistance, attackRange * 0.9f);
            cueProperties = new MaterialPropertyBlock();
            if (target == null || targetCollider == null || attackCue == null || cueRenderer == null)
            {
                Debug.LogError("TRACE melee enemy requires an explicit target, target collider and attack cue.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += Die;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= Die;
            StopAgent();
            HideCue();
        }

        private void Update()
        {
            if (health.IsDead)
            {
                if (state != EnemyState.Dead) Die();
                return;
            }
            if (squad != null)
            {
                if (!squad.LeaderAlive) { Idle(); return; }
                // Never change the committed victim during a swing. A target is held for at least 2s.
                if (state != EnemyState.Attack && (Time.time >= nextTargetAt || target == null ||
                    !target.isActiveAndEnabled || target.Health.IsDead))
                {
                    nextTargetAt = Time.time + 2f;
                    var candidate = squad.NearestLivingMember(transform.position, detectionRange, obstructionMask);
                    if (candidate != null)
                    {
                        target = candidate;
                        targetCollider = candidate.GetComponent<Collider>();
                    }
                }
            }
            if (target == null || targetCollider == null || !target.isActiveAndEnabled || target.Health.IsDead || !targetCollider.enabled)
            {
                Idle();
                return;
            }
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (state == EnemyState.Idle)
            {
                if (distance <= detectionRange && HasLineOfSight())
                {
                    state = EnemyState.Chase;
                    nextPathAt = 0f;
                }
                else return;
            }
            if (distance > loseTargetRange)
            {
                Idle();
                return;
            }
            if (state == EnemyState.Attack)
            {
                UpdateAttack();
                return;
            }
            if (distance <= attackRange && HasLineOfSight())
            {
                BeginAttack();
                return;
            }
            Chase();
        }

        private void Chase()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = false;
            if (Time.time >= nextPathAt)
            {
                nextPathAt = Time.time + repathInterval;
                if (NavMesh.SamplePosition(target.transform.position, out NavMeshHit hit, 1f, agent.areaMask))
                    agent.SetDestination(hit.position);
                else
                    agent.ResetPath(); // Target left the baked area: wait, do not walk off the NavMesh.
            }
            Vector3 direction = agent.desiredVelocity;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), agent.angularSpeed * Time.deltaTime);
        }

        private void BeginAttack()
        {
            StopAgent();
            state = EnemyState.Attack;
            Phase = AttackPhase.Windup;
            strikeDirection = target.transform.position - transform.position;
            strikeDirection.y = 0f;
            strikeDirection = strikeDirection.sqrMagnitude > 0.001f ? strikeDirection.normalized : transform.forward;
            // Commit the hit direction now: no homing turn during windup or active frames.
            transform.rotation = Quaternion.LookRotation(strikeDirection);
            activeAt = Time.time + windup;
            recoveryAt = activeAt + activeDuration;
            attackEndsAt = recoveryAt + recovery;
            strikeConsumed = false;
            ShowCue(false);
        }

        private void UpdateAttack()
        {
            if (Time.time < activeAt) return;
            if (Time.time < recoveryAt)
            {
                Phase = AttackPhase.Active;
                ShowCue(true);
                TryStrike();
                return;
            }
            Phase = AttackPhase.Recovery;
            HideCue();
            if (Time.time >= attackEndsAt)
            {
                state = EnemyState.Chase;
                Phase = AttackPhase.None;
                nextPathAt = 0f;
            }
        }

        private void TryStrike()
        {
            if (strikeConsumed || !HasLineOfSight()) return;
            Vector3 center = Origin + strikeDirection * (hitRange * 0.5f);
            Vector3 extents = new Vector3(hitHalfWidth, 0.65f, hitRange * 0.5f);
            Quaternion orientation = Quaternion.LookRotation(strikeDirection);
            int mask = 1 << targetCollider.gameObject.layer;
            int count = Physics.OverlapBoxNonAlloc(center, extents, hits, orientation, mask, QueryTriggerInteraction.Ignore);
            Collider[] candidates = hits;
            if (count == hits.Length)
            {
                candidates = Physics.OverlapBox(center, extents, orientation, mask, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }
            for (int i = 0; i < count; i++)
            {
                if (candidates[i] != targetCollider) continue;
                // A dodged strike is consumed too: no damage on a later frame of the same swing.
                strikeConsumed = true;
                target.TryTakeDamage(damage);
                return;
            }
        }

        private bool HasLineOfSight() =>
            !Physics.Linecast(Origin, targetCollider.bounds.center, obstructionMask, QueryTriggerInteraction.Ignore);

        private void Idle()
        {
            state = EnemyState.Idle;
            Phase = AttackPhase.None;
            StopAgent();
            HideCue();
        }

        private void Die()
        {
            state = EnemyState.Dead;
            Phase = AttackPhase.None;
            StopAgent();
            agent.enabled = false;
            HideCue();
        }

        private void StopAgent()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // ResetPath clears the previous stop state in this editor version.
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
        }

        private void ShowCue(bool active)
        {
            attackCue.transform.SetPositionAndRotation(
                transform.position + Vector3.up * 0.035f + strikeDirection * (hitRange * 0.5f),
                Quaternion.LookRotation(strikeDirection));
            attackCue.transform.localScale = new Vector3(hitHalfWidth * 2f, 0.03f, hitRange);
            cueRenderer.GetPropertyBlock(cueProperties);
            cueProperties.SetColor(BaseColor, active ? new Color(1f, 0.12f, 0.08f) : new Color(1f, 0.7f, 0.08f));
            cueRenderer.SetPropertyBlock(cueProperties);
            attackCue.SetActive(true);
        }

        private void HideCue()
        {
            if (attackCue != null) attackCue.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Vector3 direction = Application.isPlaying && state == EnemyState.Attack ? strikeDirection : transform.forward;
            Gizmos.matrix = Matrix4x4.TRS(Origin + direction * hitRange * 0.5f, Quaternion.LookRotation(direction), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(hitHalfWidth * 2f, 1.3f, hitRange));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
