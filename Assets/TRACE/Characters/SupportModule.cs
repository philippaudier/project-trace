using TRACE.Combat;
using UnityEngine;

namespace TRACE.Characters
{
    // Support's ORIGIN field stabilization technology: a mint emitter that idles low and brightens while any
    // squad member carries a Pulse Shield. Reads the shields only; no reaction to Tactical Focus.
    [DisallowMultipleComponent]
    public sealed class SupportModule : MonoBehaviour
    {
        [SerializeField] private Renderer emitter;
        [SerializeField] private Shield[] shields = new Shield[0];
        [SerializeField] private Color emissionColor = new Color(0.45f, 0.9f, 0.78f);
        [SerializeField, Min(0f)] private float idleIntensity = 0.5f;
        [SerializeField, Min(0f)] private float deployedIntensity = 1.7f;
        [SerializeField, Min(0.01f)] private float responseTime = 0.25f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock properties;
        public float CurrentIntensity { get; private set; }
        public float IdleIntensity => idleIntensity;
        public int ShieldCount => shields != null ? shields.Length : 0;
        public bool IsDeployed
        {
            get
            {
                if (shields == null) return false;
                foreach (var shield in shields) if (shield != null && shield.CurrentAmount > 0f) return true;
                return false;
            }
        }

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
