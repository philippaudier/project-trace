using UnityEngine;

namespace TRACE.Combat
{
    // Straight, unguided shot committed at launch: stepping out of its line or dodging with i-frames avoids it.
    // One instance is owned by its shooter and reused; no pooling or ballistic framework.
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float radius = 0.18f;
        [SerializeField] private LayerMask victimMask = 1 << 2;
        [SerializeField] private LayerMask obstructionMask = 1;
        private Vector3 direction;
        private Vector3 launchPosition;
        private float speed;
        private float damage;
        private float maxDistance;
        public bool InFlight => gameObject.activeSelf;
        public int HitCount { get; private set; }
        public int LandedCount { get; private set; }
        public Health LastVictim { get; private set; }
        public Vector3 Direction => direction;

        public void Launch(Vector3 origin, Vector3 heading, float flightSpeed, float hitDamage, float range)
        {
            if (heading.sqrMagnitude < 0.0001f || flightSpeed <= 0f || range <= 0f) return;
            // Fly in world space: a shooter turning after the shot must not swing the shot with it.
            if (transform.parent != null) transform.SetParent(null, true);
            transform.position = launchPosition = origin;
            direction = heading.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
            speed = flightSpeed;
            damage = hitDamage;
            maxDistance = range;
            gameObject.SetActive(true);
        }

        public void Vanish() => gameObject.SetActive(false);

        private void Update()
        {
            float step = speed * Time.deltaTime;
            if (step <= 0f) return;
            int mask = victimMask | obstructionMask;
            if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, step, mask, QueryTriggerInteraction.Ignore))
            {
                if ((victimMask.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    var receiver = hit.collider.GetComponentInParent<DamageReceiver>();
                    if (receiver != null && !receiver.Health.IsDead)
                    {
                        // A dodged shot is consumed too: it never lingers to hit on a later frame.
                        if (receiver.TryTakeDamage(damage))
                        {
                            HitCount++;
                            LastVictim = receiver.Health;
                        }
                        else LandedCount++;
                        Vanish();
                        return;
                    }
                }
                else
                {
                    Vanish();
                    return;
                }
            }
            transform.position += direction * step;
            if (Vector3.Distance(launchPosition, transform.position) >= maxDistance) Vanish();
        }
    }
}
