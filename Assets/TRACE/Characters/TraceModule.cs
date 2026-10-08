using TRACE.Tactical;
using UnityEngine;

namespace TRACE.Characters
{
    // Signature emitter of the Tracewalker: a quiet amber glow that brightens and pulses while Tactical Focus
    // is held. Visual feedback only: it reads TacticalFocus and never drives it.
    [DisallowMultipleComponent]
    public sealed class TraceModule : MonoBehaviour
    {
        [SerializeField] private Renderer emitter;
        [SerializeField] private TacticalFocus focus;
        [SerializeField] private Color emissionColor = new Color(0.95f, 0.7f, 0.16f);
        [SerializeField, Min(0f)] private float idleIntensity = 0.8f;
        [SerializeField, Min(0f)] private float focusIntensity = 2.6f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.3f;
        [SerializeField, Min(0f)] private float pulseFrequency = 2.5f;
        [SerializeField, Min(0.01f)] private float responseTime = 0.12f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock properties;
        public float CurrentIntensity { get; private set; }
        public float IdleIntensity => idleIntensity;
        public bool IsFocused => focus != null && focus.IsActive;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            CurrentIntensity = idleIntensity;
            Apply();
        }

        private void Update()
        {
            float target = idleIntensity;
            if (IsFocused)
                target = focusIntensity * (1f + pulseAmount * Mathf.Sin(Time.unscaledTime * pulseFrequency * Mathf.PI * 2f));
            // Real time: Tactical Focus slows the game clock and the glow must answer at once.
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / responseTime);
            CurrentIntensity = Mathf.Lerp(CurrentIntensity, target, blend);
            Apply();
        }

        private void Apply()
        {
            if (emitter == null) return;
            emitter.GetPropertyBlock(properties);
            properties.SetColor(EmissionColor, emissionColor * CurrentIntensity);
            emitter.SetPropertyBlock(properties);
        }
    }
}
