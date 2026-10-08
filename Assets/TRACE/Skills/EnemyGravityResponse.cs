using TRACE.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.Skills
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class EnemyGravityResponse : MonoBehaviour
    {
        private NavMeshAgent agent;
        private Health health;
        private float originalSpeed;
        private float expiresAt;
        private float pullSpeed;
        private Vector3 center;
        private bool applied;
        public bool IsSlowed => applied && Time.time < expiresAt && !health.IsDead;
        public ComboOpportunity Opportunity { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            Opportunity = GetComponent<ComboOpportunity>();
        }
        public void Apply(Vector3 fieldCenter, float slowFraction, float attractionSpeed, float until)
        {
            if (!isActiveAndEnabled || health.IsDead || !agent.enabled || !agent.isOnNavMesh) return;
            if (!applied) originalSpeed = agent.speed;
            applied = true;
            agent.speed = originalSpeed * (1f - Mathf.Clamp01(slowFraction));
            center = fieldCenter;
            pullSpeed = Mathf.Max(0f, attractionSpeed);
            expiresAt = until;
        }

        private void LateUpdate()
        {
            if (!applied) return;
            if (!IsSlowed || !agent.enabled || !agent.isOnNavMesh) { Restore(); return; }
            Vector3 delta = center - transform.position;
            delta.y = 0f;
            Vector3 destination = transform.position + Vector3.ClampMagnitude(delta, pullSpeed * Time.deltaTime);
            // Navigation constrains attraction at edges and holes; never write the Transform or use forces.
            if (agent.Raycast(destination, out NavMeshHit edge)) destination = edge.position;
            agent.Move(destination - transform.position);
        }

        private void OnDisable() => Restore();
        private void Restore()
        {
            if (applied && agent != null) agent.speed = originalSpeed;
            applied = false;
        }
    }
}
