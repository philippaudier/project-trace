using UnityEngine;

namespace TRACE.Combat
{
    // Bulwark weakness: hits whose origin lies inside the frontal cone are reduced; flanks take full damage.
    // Applied by ComboDamage for player, dash and companion hits. No armor stat framework.
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class FrontalGuard : MonoBehaviour
    {
        [SerializeField, Range(0f, 180f)] private float guardHalfAngle = 60f;
        [SerializeField, Range(0f, 1f)] private float frontalMultiplier = 0.35f;
        [SerializeField] private Renderer plate;
        [SerializeField] private TextMesh label;
        [SerializeField] private LineRenderer arc;
        [SerializeField] private TRACE.Tactical.TacticalFocus focus;
        [SerializeField, Min(0.05f)] private float feedbackDuration = 0.5f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Health health;
        private MaterialPropertyBlock properties;
        private Color plateColor;
        private UnityEngine.Camera view;
        public float GuardHalfAngle => guardHalfAngle;
        public float FrontalMultiplier => frontalMultiplier;
        public int BlockedCount { get; private set; }
        public int FlankCount { get; private set; }
        public float LastBlockedAt { get; private set; } = float.NegativeInfinity;
        public float LastFlankedAt { get; private set; } = float.NegativeInfinity;

        private void Awake()
        {
            health = GetComponent<Health>();
            properties = new MaterialPropertyBlock();
            view = UnityEngine.Camera.main;
            if (plate != null) plateColor = plate.sharedMaterial.GetColor(BaseColor);
        }

        public bool IsFrontal(Vector3 origin)
        {
            Vector3 toOrigin = origin - transform.position;
            toOrigin.y = 0f;
            return toOrigin.sqrMagnitude < 0.0001f || Vector3.Angle(transform.forward, toOrigin) <= guardHalfAngle;
        }

        public float Modify(float damage, Vector3 origin)
        {
            if (!isActiveAndEnabled || health.IsDead || damage <= 0f) return damage;
            if (IsFrontal(origin))
            {
                BlockedCount++;
                LastBlockedAt = Time.time;
                return damage * frontalMultiplier;
            }
            FlankCount++;
            LastFlankedAt = Time.time;
            return damage;
        }

        private void LateUpdate()
        {
            bool blocked = Time.time - LastBlockedAt < feedbackDuration;
            bool flanked = Time.time - LastFlankedAt < feedbackDuration && LastFlankedAt > LastBlockedAt;
            if (plate != null)
            {
                float flash = blocked ? Mathf.Clamp01(1f - (Time.time - LastBlockedAt) / feedbackDuration) : 0f;
                plate.GetPropertyBlock(properties);
                properties.SetColor(BaseColor, Color.Lerp(plateColor, Color.white, flash));
                plate.SetPropertyBlock(properties);
            }
            if (label != null)
            {
                bool show = !health.IsDead && (blocked || flanked);
                label.gameObject.SetActive(show);
                if (show)
                {
                    label.text = flanked ? "FLANC !" : "BLOQUE";
                    label.color = flanked ? new Color(1f, 0.55f, 0.1f) : new Color(0.7f, 0.78f, 0.9f);
                    if (view != null) label.transform.rotation = view.transform.rotation;
                }
            }
            if (arc != null)
            {
                bool show = !health.IsDead && focus != null && focus.IsActive;
                arc.gameObject.SetActive(show);
                if (show)
                {
                    // Ground wedge of the guarded cone, so Tactical Focus explains where not to hit.
                    int count = arc.positionCount;
                    Vector3 origin = transform.position + Vector3.up * 0.08f;
                    arc.SetPosition(0, origin);
                    for (int i = 1; i < count; i++)
                    {
                        float t = (i - 1f) / (count - 2f);
                        float angle = Mathf.Lerp(-guardHalfAngle, guardHalfAngle, t);
                        arc.SetPosition(i, origin + Quaternion.Euler(0f, angle, 0f) * transform.forward * 1.9f);
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (label != null) label.gameObject.SetActive(false);
            if (arc != null) arc.gameObject.SetActive(false);
        }
    }
}
