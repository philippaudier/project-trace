using UnityEngine;

namespace TRACE.Narrative
{
    // Placeholder soundscape generated in code (no audio assets yet): wind, electrical hum, drips, creaks,
    // and a combat drone. Deliberately crude; a real pass replaces the clips without touching callers.
    [DisallowMultipleComponent]
    public sealed class AmbienceController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] private float humVolume = 0.06f;
        [SerializeField, Range(0f, 1f)] private float tensionHumVolume = 0.16f;
        [SerializeField, Range(0f, 1f)] private float combatVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float dripVolume = 0.25f;
        [SerializeField, Min(0.5f)] private float fadeTime = 1.5f;
        private const int SampleRate = 22050;
        private AudioSource wind;
        private AudioSource hum;
        private AudioSource combat;
        private AudioSource oneShot;
        private AudioClip drip;
        private AudioClip creak;
        private float nextDripAt;
        private float targetCombat;
        private float targetHum;
        public bool CombatActive { get; private set; }
        public bool TensionActive { get; private set; }
        public int CreakCount { get; private set; }
        public bool AudioAvailable { get; private set; }

        private void Awake()
        {
            try
            {
                wind = Source("Wind", Loop(4f, WindSample), windVolume, true);
                hum = Source("Hum", Loop(2f, HumSample), humVolume, true);
                combat = Source("Combat Drone", Loop(4.8f, CombatSample), 0f, true);
                oneShot = Source("One Shots", null, 1f, false);
                drip = Clip("Drip", 0.12f, DripSample);
                creak = Clip("Creak", 1.1f, CreakSample);
                AudioAvailable = wind != null && wind.clip != null;
            }
            catch (System.Exception)
            {
                AudioAvailable = false;
            }
            targetHum = humVolume;
            nextDripAt = Time.unscaledTime + 3f;
        }

        public void SetCombat(bool active)
        {
            CombatActive = active;
            targetCombat = active ? combatVolume : 0f;
        }

        public void SetTension(bool active)
        {
            TensionActive = active;
            targetHum = active ? tensionHumVolume : humVolume;
        }

        public void PlayCreak()
        {
            CreakCount++;
            if (AudioAvailable && creak != null) oneShot.PlayOneShot(creak, 0.6f);
        }

        private void Update()
        {
            if (!AudioAvailable) return;
            float dt = Time.unscaledDeltaTime / fadeTime;
            combat.volume = Mathf.MoveTowards(combat.volume, targetCombat, dt * combatVolume);
            hum.volume = Mathf.MoveTowards(hum.volume, targetHum, dt * tensionHumVolume);
            if (Time.unscaledTime >= nextDripAt)
            {
                nextDripAt = Time.unscaledTime + Random.Range(3.5f, 9f);
                oneShot.PlayOneShot(drip, dripVolume * Random.Range(0.6f, 1f));
            }
        }

        private AudioSource Source(string name, AudioClip clip, float volume, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            if (clip != null) source.Play();
            return source;
        }

        private static AudioClip Loop(float seconds, System.Func<int, float> sample) => Clip("Loop", seconds, sample);

        private static AudioClip Clip(string name, float seconds, System.Func<int, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            if (clip == null) return null;
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i), -1f, 1f);
            clip.SetData(data, 0);
            return clip;
        }

        private static float lowPass;
        private static float WindSample(int i)
        {
            float t = (float)i / SampleRate;
            float noise = Random.value * 2f - 1f;
            lowPass += (noise - lowPass) * 0.035f;
            float gust = 0.55f + 0.45f * Mathf.Sin(t * 1.3f) * Mathf.Sin(t * 0.47f + 1f);
            return lowPass * 2.2f * gust;
        }

        private static float HumSample(int i)
        {
            float t = (float)i / SampleRate;
            return Mathf.Sin(t * 50f * Mathf.PI * 2f) * 0.5f + Mathf.Sin(t * 100f * Mathf.PI * 2f) * 0.2f + Mathf.Sin(t * 150f * Mathf.PI * 2f) * 0.08f;
        }

        private static float CombatSample(int i)
        {
            float t = (float)i / SampleRate;
            float beat = Mathf.Repeat(t, 0.6f) / 0.6f;
            float envelope = Mathf.Exp(-beat * 5f);
            float bar = Mathf.Repeat(t, 2.4f) < 1.2f ? 55f : 41.2f;
            return (Mathf.Sin(t * bar * Mathf.PI * 2f) * 0.6f + Mathf.Sin(t * bar * 2f * Mathf.PI * 2f) * 0.25f) * envelope +
                Mathf.Sin(t * 220f * Mathf.PI * 2f) * 0.04f * (1f - envelope);
        }

        private static float DripSample(int i)
        {
            float t = (float)i / SampleRate;
            return Mathf.Sin(t * 2600f * Mathf.PI * 2f * (1f - t * 2f)) * Mathf.Exp(-t * 45f);
        }

        private static float CreakSample(int i)
        {
            float t = (float)i / SampleRate;
            float frequency = Mathf.Lerp(190f, 85f, t) * (1f + 0.05f * Mathf.Sin(t * 37f));
            return Mathf.Sin(t * frequency * Mathf.PI * 2f) * 0.5f * Mathf.Exp(-t * 2.2f) * Mathf.Min(1f, t * 20f);
        }
    }
}
