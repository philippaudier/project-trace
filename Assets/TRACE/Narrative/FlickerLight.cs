using UnityEngine;

namespace TRACE.Narrative
{
    // Cheap intensity noise for a tired lamp; real-time so it keeps breathing under Tactical Focus.
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float depth = 0.35f;
        [SerializeField, Min(0.1f)] private float speed = 9f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance per second of a short blackout.")] private float dropoutRate = 0.15f;
        private Light source;
        private float baseIntensity;
        private float seed;
        private float dropoutUntil;

        private void Awake()
        {
            source = GetComponent<Light>();
            baseIntensity = source.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            if (t < dropoutUntil) { source.intensity = baseIntensity * 0.05f; return; }
            if (Random.value < dropoutRate * Time.unscaledDeltaTime) dropoutUntil = t + Random.Range(0.04f, 0.14f);
            float noise = Mathf.PerlinNoise(seed, t * speed) * 2f - 1f;
            source.intensity = baseIntensity * (1f + noise * depth);
        }
    }
}
