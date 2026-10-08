using TRACE.Skills;
using UnityEngine;

namespace TRACE.Characters
{
    // Control's ORIGIN field technology: a cyan emitter that idles low and brightens while her Gravity Field
    // is deployed. Reads the skill only; no reaction to Tactical Focus (she is not a Tracewalker).
    [DisallowMultipleComponent]
    public sealed class FieldControlModule : MonoBehaviour
    {
        [SerializeField] private Renderer emitter;
        [SerializeField] private GravityFieldSkill skill;
        [SerializeField] private Color emissionColor = new Color(0.5f, 0.85f, 1f);
        [SerializeField, Min(0f)] private float idleIntensity = 0.5f;
        [SerializeField, Min(0f)] private float deployedIntensity = 1.8f;
        [SerializeField, Min(0.01f)] private float responseTime = 0.2f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock properties;
        public float CurrentIntensity { get; private set; }
        public float IdleIntensity => idleIntensity;
        public bool IsDeployed => skill != null && skill.Field != null && skill.Field.RemainingDuration > 0f;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            CurrentIntensity = idleIntensity;
            Apply();
        }

        private void Update()
        {
            float target = IsDeployed ? deployedIntensity : idleIntensity;
            float blend = 1f - Mathf.Exp(-Time.deltaTime / responseTime);
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
