using TRACE.Characters;
using UnityEngine;

namespace TRACE.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class CompanionFeedback : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer bodyRenderer;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Health health;
        private DamageReceiver receiver;
        private SquadMember member;
        private MaterialPropertyBlock properties;
        private Color normalColor;
        private float flashUntil;

        private void Awake()
        {
            health = GetComponent<Health>();
            receiver = GetComponent<DamageReceiver>();
            member = GetComponent<SquadMember>();
            properties = new MaterialPropertyBlock();
            normalColor = bodyRenderer.sharedMaterial.GetColor(BaseColor);
        }

        private void OnEnable() { health.OnDamaged += Hit; health.OnDeath += Die; }
        private void OnDisable() { health.OnDamaged -= Hit; health.OnDeath -= Die; }
        private void Hit(float damage) => flashUntil = Time.time + 0.18f;
        private void Die()
        {
            // Keep the body in the scene. No despawn, revive or squad game-over logic.
            visual.localPosition = Vector3.up * 0.35f;
            visual.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private void Update()
        {
            Color color = health.IsDead ? new Color(0.22f, 0.23f, 0.24f) : normalColor;
            if (!health.IsDead && member != null && member.IsPlayerControlled && receiver.IsInvulnerable)
                color = new Color(0.15f, 0.95f, 1f);
            if (Time.time < flashUntil) color = Color.Lerp(color, Color.white, (flashUntil - Time.time) / 0.18f);
            bodyRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            bodyRenderer.SetPropertyBlock(properties);
        }
    }
}
