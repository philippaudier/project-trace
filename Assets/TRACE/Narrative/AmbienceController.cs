using UnityEngine;
using UnityEngine.Audio;

namespace TRACE.Narrative
{
    // Environment soundscape in four layers. Base: a continuous, very low room tone. Environment: the zone's character
    // on a different band (air, ventilation). Detail: rare spatial one-shots (creaks, relays, distant impacts) at
    // plausible points, with long random gaps and some skipped slots, so silence stays part of the design. Anomaly: a
    // faint layer only on Trace moments. Story hooks: SetTension, SetCombat, PlayCreak, SetAnomaly, SetConclusion.
    // Every change fades on unscaled time. Missing clips fall back to the code-generated placeholders, so a scene with
    // no assigned clips still sounds like before.
    [DisallowMultipleComponent]
    public sealed class AmbienceController : MonoBehaviour
    {
        [Header("Base and environment")]
        [SerializeField] private AudioClip baseLoop;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 0.3f;
        [SerializeField] private AudioClip environmentLoop;
        [SerializeField, Range(0f, 1f)] private float environmentVolume = 0.22f;
        [SerializeField, Min(0.1f), Tooltip("Fade of base / environment changes (tension, conclusion).")] private float layerFade = 1.5f;
        [SerializeField, Min(1f), Tooltip("Environment gain in the tension corridor.")] private float tensionBoost = 1.35f;
        [SerializeField, Range(0f, 1f), Tooltip("Layers kept at the conclusion, to leave room for the reveal.")] private float conclusionLevel = 0.45f;

        [Header("Anomaly (Trace moments only)")]
        [SerializeField] private AudioClip anomalyLoop;
        [SerializeField, Range(0f, 1f)] private float anomalyVolume = 0.18f;
        [SerializeField, Min(0.1f)] private float anomalyFade = 2.5f;

        [Header("Details")]
        [SerializeField, Tooltip("Empty: no detail events at all (test scenes).")] private AudioClip[] detailClips = new AudioClip[0];
        [SerializeField, Tooltip("Plausible spots (pipes, panels, far walls). Empty: details play around the listener.")] private Transform[] detailPoints = new Transform[0];
        [SerializeField] private Vector2 detailInterval = new Vector2(8f, 30f);
        [SerializeField, Range(0f, 1f), Tooltip("Share of detail slots that stay silent.")] private float silenceChance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float detailVolume = 0.45f;
        [SerializeField, Range(0f, 0.2f)] private float detailPitchJitter = 0.06f;
        [SerializeField, Min(0.1f)] private float detailMinDistance = 3f;
        [SerializeField, Min(1f)] private float detailMaxDistance = 28f;

        [Header("Story and combat")]
        [SerializeField, Tooltip("Hall event creak; empty uses the generated placeholder.")] private AudioClip creakClip;
        [SerializeField, Range(0f, 1f)] private float creakVolume = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("Placeholder combat drone (Music group).")] private float combatVolume = 0.3f;
        [SerializeField, Tooltip("Mixer group for every ambience layer.")] private AudioMixerGroup ambienceGroup;
        [SerializeField, Tooltip("Mixer group for the combat drone (the slice's only musical layer).")] private AudioMixerGroup musicGroup;

        private const int SampleRate = 22050;
        private AudioSource baseSource;
        private AudioSource environmentSource;
        private AudioSource anomalySource;
        private AudioSource combat;
        private AudioSource oneShot;
        private AudioSource[] detailSources;
        private AudioClip creak;
        private float untilDetail;
        private int lastDetail = -1;
        private int nextDetailSource;
        private float targetCombat;
        private float targetAnomaly;
        public bool CombatActive { get; private set; }
        public bool TensionActive { get; private set; }
        public bool AnomalyActive { get; private set; }
        public bool ConclusionActive { get; private set; }
        public int CreakCount { get; private set; }
        public int DetailCount { get; private set; }
        public Vector3 LastDetailPosition { get; private set; }
        public bool AudioAvailable { get; private set; }
        public float BaseLevel => baseSource != null ? baseSource.volume : 0f;
        public float EnvironmentLevel => environmentSource != null ? environmentSource.volume : 0f;
        public float AnomalyLevel => anomalySource != null ? anomalySource.volume : 0f;
        public bool UsesAuthoredClips => baseLoop != null;

        private void Awake()
        {
            try
            {
                baseSource = Source("Base", baseLoop != null ? baseLoop : Loop(2f, HumSample), 0f, true);
                environmentSource = Source("Environment", environmentLoop != null ? environmentLoop : Loop(4f, WindSample), 0f, true);
                anomalySource = Source("Anomaly", anomalyLoop, 0f, true);
                combat = Source("Combat Drone", Loop(4.8f, CombatSample), 0f, true, musicGroup);
                oneShot = Source("One Shots", null, 1f, false);
                detailSources = new AudioSource[2];
                for (int i = 0; i < detailSources.Length; i++)
                {
                    detailSources[i] = Source("Detail " + (i + 1), null, 1f, false);
                    detailSources[i].spatialBlend = 1f;
                    detailSources[i].rolloffMode = AudioRolloffMode.Linear;
                    detailSources[i].minDistance = detailMinDistance;
                    detailSources[i].maxDistance = detailMaxDistance;
                    detailSources[i].dopplerLevel = 0f;
                }
                creak = creakClip != null ? creakClip : Clip("Creak", 1.1f, CreakSample);
                // Long loops start at a random point so two visits never sound identical.
                foreach (var loop in new[] { baseSource, environmentSource })
                    if (loop.clip != null && loop.clip.length > 1f) loop.time = Random.Range(0f, loop.clip.length * 0.9f);
                AudioAvailable = baseSource != null && baseSource.clip != null;
            }
            catch (System.Exception)
            {
                AudioAvailable = false;
            }
            untilDetail = Random.Range(detailInterval.x * 0.5f, detailInterval.y * 0.5f);
        }

        public void SetCombat(bool active)
        {
            CombatActive = active;
            targetCombat = active ? combatVolume : 0f;
        }

        public void SetTension(bool active) => TensionActive = active;

        // Trace moments: something has changed, not a new piece of music.
        public void SetAnomaly(bool active)
        {
            AnomalyActive = active && anomalySource != null && anomalySource.clip != null;
            targetAnomaly = AnomalyActive ? anomalyVolume : 0f;
            if (AnomalyActive && !anomalySource.isPlaying) anomalySource.Play();
        }

        // The ending leaves room: the place recedes, the silence and the reveal take over.
        public void SetConclusion(bool active) => ConclusionActive = active;

        public void PlayCreak()
        {
            CreakCount++;
            if (AudioAvailable && creak != null) oneShot.PlayOneShot(creak, creakVolume);
        }

        private void Update()
        {
            if (!AudioAvailable) return;
            float dt = Time.unscaledDeltaTime;
            float keep = ConclusionActive ? conclusionLevel : 1f;
            float baseTarget = baseVolume * keep;
            float environmentTarget = environmentVolume * keep * (TensionActive ? tensionBoost : 1f);
            baseSource.volume = Mathf.MoveTowards(baseSource.volume, baseTarget, dt * baseVolume / layerFade);
            environmentSource.volume = Mathf.MoveTowards(environmentSource.volume, environmentTarget, dt * environmentVolume * tensionBoost / layerFade);
            combat.volume = Mathf.MoveTowards(combat.volume, targetCombat, dt * combatVolume / layerFade);
            if (anomalySource.clip != null)
            {
                anomalySource.volume = Mathf.MoveTowards(anomalySource.volume, targetAnomaly, dt * anomalyVolume / anomalyFade);
                if (!AnomalyActive && anomalySource.isPlaying && anomalySource.volume <= 0f) anomalySource.Stop();
            }
            untilDetail -= dt;
            if (untilDetail <= 0f)
            {
                untilDetail = Random.Range(detailInterval.x, detailInterval.y);
                if (Random.value >= silenceChance) PlayDetail();
            }
        }

        // One rare spatial detail: never the same clip twice in a row, slight pitch variation, a plausible spot.
        private void PlayDetail()
        {
            if (detailClips.Length == 0) return;
            int pick = Random.Range(0, detailClips.Length);
            if (detailClips.Length > 1 && pick == lastDetail) pick = (pick + 1) % detailClips.Length;
            lastDetail = pick;
            var source = detailSources[nextDetailSource];
            nextDetailSource = (nextDetailSource + 1) % detailSources.Length;
            var listener = Object.FindFirstObjectByType<AudioListener>();
            Vector3 position = detailPoints.Length > 0 && detailPoints[0] != null
                ? detailPoints[Random.Range(0, detailPoints.Length)].position + Random.insideUnitSphere * 1.5f
                : (listener != null ? listener.transform.position : transform.position) + Random.onUnitSphere * Random.Range(8f, 18f);
            source.transform.position = position;
            source.pitch = 1f + Random.Range(-detailPitchJitter, detailPitchJitter);
            source.PlayOneShot(detailClips[pick], detailVolume);
            LastDetailPosition = position;
            DetailCount++;
        }

        // Test and debugging hook: plays the next detail now.
        public void PlayDetailNow() { if (AudioAvailable) PlayDetail(); }

        private AudioSource Source(string name, AudioClip clip, float volume, bool loop, AudioMixerGroup group = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = group != null ? group : ambienceGroup;
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

        private static float CreakSample(int i)
        {
            float t = (float)i / SampleRate;
            float frequency = Mathf.Lerp(190f, 85f, t) * (1f + 0.05f * Mathf.Sin(t * 37f));
            return Mathf.Sin(t * frequency * Mathf.PI * 2f) * 0.5f * Mathf.Exp(-t * 2.2f) * Mathf.Min(1f, t * 20f);
        }
    }
}
