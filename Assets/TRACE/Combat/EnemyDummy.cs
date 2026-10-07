using UnityEngine;

namespace TRACE.Combat
{
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class EnemyDummy : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Collider[] hitColliders;
        [SerializeField, Min(0.01f)] private float hitDuration = 0.18f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.18f;
        [SerializeField, Min(0.01f)] private float deathDelay = 0.55f;
        [SerializeField] private Color hitColor = new Color(1f, 0.9f, 0.25f);
        [SerializeField] private Color deadColor = new Color(0.22f, 0.23f, 0.24f);

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Health health;
        private MaterialPropertyBlock properties;
        private Color normalColor;
        private Vector3 restingPosition;
        private Vector3 restingScale;
        private float hitUntil;
        private float deathAt = -1f;
        private bool started;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (visual == null || bodyRenderer == null || hitColliders == null || hitColliders.Length == 0)
            {
                Debug.LogError("TRACE dummy requires a visual, renderer and hit colliders.", this);
                enabled = false;
                return;
            }
            properties = new MaterialPropertyBlock();
            normalColor = bodyRenderer.sharedMaterial.GetColor(BaseColor);
            restingPosition = visual.localPosition;
            restingScale = visual.localScale;
        }

        private void OnEnable()
        {
            if (health == null || properties == null)
                return;
            health.OnDamaged += ReactToHit;
            health.OnDeath += Die;
            if (started && health.IsDead)
                Die();
        }

        private void Start()
        {
            // All Awake calls (including Health initialization) have completed now.
            started = true;
            if (health.IsDead)
                Die();
        }

        private void OnDisable()
        {
            if (health == null)
                return;
            health.OnDamaged -= ReactToHit;
            health.OnDeath -= Die;
        }

        private void ReactToHit(float amount)
        {
            hitUntil = Time.time + hitDuration;
        }

        private void Die()
        {
            foreach (Collider hitCollider in hitColliders)
                if (hitCollider != null)
                    hitCollider.enabled = false;
            deathAt = Time.time + deathDelay;
        }

        private void Update()
        {
            float hit = Mathf.Clamp01((hitUntil - Time.time) / hitDuration);
            // Recoil affects only the visual; the stationary dummy cannot be pushed through walls.
            visual.localPosition = restingPosition + Vector3.back * (recoilDistance * hit);
            Color color = Color.Lerp(health.IsDead ? deadColor : normalColor, hitColor, hit);
            bodyRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            bodyRenderer.SetPropertyBlock(properties);
            if (!health.IsDead)
                return;
            float remaining = Mathf.Clamp01((deathAt - Time.time) / deathDelay);
            visual.localScale = restingScale * Mathf.Lerp(0.25f, 1f, remaining);
            if (Time.time >= deathAt)
                gameObject.SetActive(false);
        }
    }
}
