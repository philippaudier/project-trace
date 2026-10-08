using UnityEngine;

namespace TRACE.Skills
{
    // A scene-owned zone survives control transfer (and the caster's death) until its own expiry.
    public sealed class GravityField : MonoBehaviour
    {
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.1f)] private float radius = 3f;
        [SerializeField, Min(0.1f)] private float duration = 3f;
        [SerializeField, Range(0f, 0.95f)] private float slowFraction = 0.4f;
        [SerializeField, Min(0f)] private float attractionSpeed = 1.2f;
        [SerializeField] private Transform visual;
        [Header("V0.7 opportunity")]
        [SerializeField, Min(0.1f)] private float groupedDwellTime = 0.6f;
        [SerializeField, Min(0.1f)] private float groupedDuration = 2.5f;
        private int generation;
        private readonly Collider[] overlaps = new Collider[32];
        private float expiresAt;
        public float Radius => radius;
        public float RemainingDuration => isActiveAndEnabled ? Mathf.Max(0f, expiresAt - Time.time) : 0f;

        public void Spawn(Vector3 center)
        {
            transform.position = center;
            expiresAt = Time.time + duration;
            generation++;
            if (visual != null) visual.localScale = new Vector3(radius, 1f, radius);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (Time.time >= expiresAt) { gameObject.SetActive(false); return; }
            if (Time.deltaTime <= 0f) return;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlaps, enemyMask, QueryTriggerInteraction.Ignore);
            Collider[] found = overlaps;
            if (count == overlaps.Length)
            {
                found = Physics.OverlapSphere(transform.position, radius, enemyMask, QueryTriggerInteraction.Ignore);
                count = found.Length;
            }
            for (int i = 0; i < count; i++)
            {
                var response = found[i].GetComponentInParent<EnemyGravityResponse>();
                if (response == null || Physics.Linecast(transform.position + Vector3.up * 0.6f,
                    found[i].bounds.center, obstructionMask, QueryTriggerInteraction.Ignore)) continue;
                response.Apply(transform.position, slowFraction, attractionSpeed, Mathf.Min(expiresAt, Time.time + 0.1f));
                if (response.IsSlowed && response.Opportunity != null)
                    response.Opportunity.ObserveGravity(this, generation, groupedDwellTime, groupedDuration);
            }
        }
    }
}
