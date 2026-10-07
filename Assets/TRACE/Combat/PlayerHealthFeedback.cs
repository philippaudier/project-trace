using TRACE.Characters;
using TRACE.Input;
using UnityEngine;

namespace TRACE.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class PlayerHealthFeedback : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private PlayerMeleeAttack attack;
        [SerializeField] private DamageReceiver receiver;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.18f;
        [SerializeField] private Color hitColor = new Color(1f, 0.15f, 0.15f);
        [SerializeField] private Color dodgeColor = new Color(0.15f, 0.95f, 1f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Health health;
        private MaterialPropertyBlock properties;
        private Color normalColor;
        private float flashUntil;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (input == null || motor == null || attack == null || receiver == null || bodyRenderer == null)
            {
                Debug.LogError("TRACE player health feedback requires its player components and body renderer.", this);
                enabled = false;
                return;
            }
            properties = new MaterialPropertyBlock();
            normalColor = bodyRenderer.sharedMaterial.GetColor(BaseColor);
        }

        private void OnEnable()
        {
            if (health == null || properties == null) return;
            health.OnDamaged += Damaged;
            health.OnDeath += Die;
        }

        private void Start()
        {
            if (health.IsDead) Die();
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.OnDamaged -= Damaged;
            health.OnDeath -= Die;
        }

        private void Damaged(float amount) => flashUntil = Time.time + flashDuration;

        private void Die()
        {
            attack.enabled = false;
            motor.enabled = false;
            input.enabled = false;
            receiver.ClearInvulnerability();
        }

        private void Update()
        {
            Color color = health.IsDead ? Color.gray : receiver.IsInvulnerable ? dodgeColor : normalColor;
            color = Color.Lerp(color, hitColor, Mathf.Clamp01((flashUntil - Time.time) / flashDuration));
            bodyRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            bodyRenderer.SetPropertyBlock(properties);
        }
    }
}
