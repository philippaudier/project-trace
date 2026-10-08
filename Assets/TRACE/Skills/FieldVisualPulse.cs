using UnityEngine;

namespace TRACE.Skills
{
    // Measured, technological motion for the Gravity Field rings: counter-rotation and a slow width pulse.
    // Game time, so Tactical Focus slows it like everything else. Visual only; GravityField owns the mechanics.
    public sealed class FieldVisualPulse : MonoBehaviour
    {
        [SerializeField] private LineRenderer[] rings;
        [SerializeField] private float[] spinDegreesPerSecond;
        [SerializeField, Min(0f)] private float baseWidth = 0.055f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.15f;
        [SerializeField, Min(0f)] private float pulseFrequency = 1.5f;
        private float elapsed;
        public float Elapsed => elapsed;

        private void OnEnable() => elapsed = 0f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || rings == null) return;
            elapsed += dt;
            float width = baseWidth * (1f + pulseAmount * Mathf.Sin(elapsed * pulseFrequency * Mathf.PI * 2f));
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null) continue;
                float spin = spinDegreesPerSecond != null && i < spinDegreesPerSecond.Length ? spinDegreesPerSecond[i] : 0f;
                rings[i].transform.localRotation = Quaternion.Euler(0f, elapsed * spin, 0f);
                rings[i].widthMultiplier = width;
            }
        }
    }
}
