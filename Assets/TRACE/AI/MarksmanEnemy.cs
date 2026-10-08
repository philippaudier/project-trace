using TRACE.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.AI
{
    // Ranged pressure: keeps its distance, aims at the controlled member with a visible line,
    // then fires one straight projectile. Repositioning is rationed so it stays shootable.
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    public sealed class MarksmanEnemy : EnemyBrain
    {
        public enum MarksmanState { Idle, Hold, Reposition, Aim, Recover, Dead }

        [SerializeField] private SquadController squad;
        [SerializeField] private Projectile projectile;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.1f)] private float detectionRange = 20f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 26f;
        [SerializeField, Min(0.1f)] private float preferredRange = 8f;
        [SerializeField, Min(0.1f)] private float minimumRange = 5f;
        [SerializeField, Min(0.1f)] private float maximumShotRange = 12f;
        [SerializeField, Min(0.1f)] private float damage = 14f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 11f;
        [SerializeField, Min(0.05f)] private float aimDuration = 1.1f;
        [SerializeField, Min(0.01f)] private float recovery = 0.5f;
        [SerializeField, Min(0f)] private float cooldown = 2.4f;
        [SerializeField, Min(0.1f)] private float retreatDistance = 4f;
        [SerializeField, Min(0.1f)] private float repositionInterval = 1.6f;
        [SerializeField, Min(0.1f)] private float retargetInterval = 1.5f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Health health;
        private NavMeshAgent agent;
        private MaterialPropertyBlock lineProperties;
        private DamageReceiver target;
        private Collider targetCollider;
        private MarksmanState state;
        private float fireAt;
        private float recoverUntil;
        private float nextShotAt;
        private float nextRepositionAt;
        private float nextRetargetAt;
        private float nextPathAt;
        private float flashUntil;
        public MarksmanState State => state;
        public int ShotsFired { get; private set; }
        public Projectile Projectile => projectile;
        public override string Archetype => "MARKSMAN";
        public override DamageReceiver CurrentTarget => target;
        public override bool IsEngaged => state != MarksmanState.Idle && state != MarksmanState.Dead;
        public override bool IsPreparingAttack => state == MarksmanState.Aim;
        public override float PreparationRemaining => state == MarksmanState.Aim ? Mathf.Max(0f, fireAt - Time.time) : 0f;
        public bool IsAimLineVisible => aimLine != null && aimLine.gameObject.activeSelf;
        public override string StatusLabel => state switch
        {
            MarksmanState.Aim => $"<color=#FFD36A>VISE {PreparationRemaining:0.0}s</color>",
            MarksmanState.Reposition => "REPOSITION",
            MarksmanState.Hold => "EN POSITION",
            MarksmanState.Recover => "TIR",
            _ => state.ToString().ToUpperInvariant()
        };
        private Vector3 Muzzle => transform.position + Vector3.up * 1.2f;

        private void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            lineProperties = new MaterialPropertyBlock();
            if (squad == null || projectile == null || aimLine == null)
            {
                Debug.LogError("TRACE marksman requires a squad, a projectile and an aim line.", this);
                enabled = false;
            }
        }

        private void OnEnable() { if (health != null) health.OnDeath += Die; }
        private void OnDisable()
        {
            if (health != null) health.OnDeath -= Die;
            StopAgent();
            HideLine();
            if (state == MarksmanState.Aim) state = MarksmanState.Hold;
        }

        private void Update()
        {
            if (health.IsDead) { if (state != MarksmanState.Dead) Die(); return; }
            if (!squad.LeaderAlive) { Idle(); return; }
            if (state != MarksmanState.Aim && (Time.time >= nextRetargetAt || !TargetValid()))
            {
                nextRetargetAt = Time.time + retargetInterval;
                // The controlled member is the priority: pressure follows the player's choice.
                DamageReceiver candidate = squad.Leader;
                if (candidate == null || Vector3.Distance(transform.position, candidate.transform.position) > detectionRange ||
                    !Visible(candidate.GetComponent<Collider>()))
                    candidate = squad.NearestLivingMember(transform.position, detectionRange, obstructionMask);
                if (candidate != null) { target = candidate; targetCollider = candidate.GetComponent<Collider>(); }
            }
            if (!TargetValid()) { Idle(); return; }
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (state == MarksmanState.Idle)
            {
                if (distance <= detectionRange && Visible(targetCollider)) state = MarksmanState.Hold;
                else return;
            }
            if (distance > loseTargetRange) { Idle(); return; }
            switch (state)
            {
                case MarksmanState.Aim: UpdateAim(); return;
                case MarksmanState.Recover:
                    if (Time.time >= flashUntil) HideLine();
                    if (Time.time >= recoverUntil) state = MarksmanState.Hold;
                    return;
            }
            bool visible = Visible(targetCollider);
            if (distance < minimumRange && Time.time >= nextRepositionAt && TryRetreat())
            {
                nextRepositionAt = Time.time + repositionInterval;
                state = MarksmanState.Reposition;
            }
            else if (distance > maximumShotRange || !visible)
            {
                Approach();
                state = MarksmanState.Reposition;
            }
            else if (state == MarksmanState.Reposition && agent.enabled && agent.isOnNavMesh && agent.hasPath &&
                agent.remainingDistance > agent.stoppingDistance + 0.1f)
            {
                // Finish the current limited move before shooting again.
            }
            else
            {
                StopAgent();
                state = MarksmanState.Hold;
                Face(target.transform.position - transform.position, 360f);
                if (Time.time >= nextShotAt && distance <= maximumShotRange && visible) BeginAim();
                return;
            }
            Vector3 velocity = agent.desiredVelocity;
            if (velocity.sqrMagnitude > 0.01f) Face(velocity, agent.angularSpeed);
            else Face(target.transform.position - transform.position, 360f);
        }

        private bool TryRetreat()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            Vector3 away = transform.position - target.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();
            foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f })
            {
                Vector3 desired = transform.position + Quaternion.Euler(0f, angle, 0f) * away * retreatDistance;
                if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 1.5f, agent.areaMask) ||
                    Vector3.Distance(hit.position, transform.position) < retreatDistance * 0.5f) continue;
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                return true;
            }
            return false;
        }

        private void Approach()
        {
            if (!agent.enabled || !agent.isOnNavMesh || Time.time < nextPathAt) return;
            nextPathAt = Time.time + repathInterval;
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            Vector3 desired = target.transform.position - toTarget.normalized * preferredRange;
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 2f, agent.areaMask))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }

        private void BeginAim()
        {
            state = MarksmanState.Aim;
            fireAt = Time.time + aimDuration;
            aimLine.gameObject.SetActive(true);
        }

        private void UpdateAim()
        {
            if (!TargetValid()) { HideLine(); state = MarksmanState.Hold; return; }
            Vector3 end = targetCollider.bounds.center;
            Face(end - transform.position, 360f);
            float progress = aimDuration > 0f ? Mathf.Clamp01(1f - PreparationRemaining / aimDuration) : 1f;
            DrawLine(Muzzle, end, Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.2f, 0.1f), progress), 0.03f + progress * 0.05f);
            if (Time.time < fireAt) return;
            // Commit the shot now: the projectile keeps this direction whatever the target does next.
            Vector3 heading = end - Muzzle;
            if (Visible(targetCollider))
            {
                projectile.Launch(Muzzle, heading, projectileSpeed, damage, maximumShotRange + 4f);
                ShotsFired++;
                DrawLine(Muzzle, end, Color.white, 0.1f);
                flashUntil = Time.time + 0.08f;
            }
            else HideLine();
            state = MarksmanState.Recover;
            recoverUntil = Time.time + recovery;
            nextShotAt = recoverUntil + cooldown;
        }

        private void DrawLine(Vector3 from, Vector3 to, Color color, float width)
        {
            aimLine.gameObject.SetActive(true);
            aimLine.SetPosition(0, from);
            aimLine.SetPosition(1, to);
            aimLine.widthMultiplier = width;
            aimLine.GetPropertyBlock(lineProperties);
            lineProperties.SetColor(BaseColor, color);
            aimLine.SetPropertyBlock(lineProperties);
        }

        private void HideLine() { if (aimLine != null) aimLine.gameObject.SetActive(false); }

        private void Face(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
        }

        private bool TargetValid() => target != null && targetCollider != null && target.isActiveAndEnabled &&
            !target.Health.IsDead && targetCollider.enabled;

        private bool Visible(Collider body) => body != null &&
            !Physics.Linecast(Muzzle, body.bounds.center, obstructionMask, QueryTriggerInteraction.Ignore);

        private void Idle()
        {
            state = MarksmanState.Idle;
            StopAgent();
            HideLine();
        }

        private void Die()
        {
            state = MarksmanState.Dead;
            StopAgent();
            agent.enabled = false;
            HideLine();
        }

        private void StopAgent()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, maximumShotRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, minimumRange);
        }
    }
}
