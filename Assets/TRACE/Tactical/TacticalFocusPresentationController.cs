using TRACE.AI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TRACE.Tactical
{
    // Sensory presentation of Tactical Focus: the Tracewalker filters perception to extract what matters. TacticalFocus
    // decides the state; this only represents it. Mixer snapshot (world behind a membrane, tactical layer unfiltered),
    // enter / exit cues and a faint bed, a restrained grade with a brief chromatic accent, a scan ring from the active
    // member, and the reveal timing the tactical HUD and markers follow. Every transition runs on unscaled time.
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    public sealed class TacticalFocusPresentationController : MonoBehaviour
    {
        public enum Stage { Markers, Cards, Status, Combos }

        [Header("Source")]
        [SerializeField] private TacticalFocus focus;
        [SerializeField] private SquadController squad;

        [Header("Audio")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerSnapshot normalSnapshot;
        [SerializeField] private AudioMixerSnapshot focusSnapshot;
        [SerializeField, Min(0f)] private float snapshotEnterTime = 0.16f;
        [SerializeField, Min(0f)] private float snapshotExitTime = 0.2f;
        [SerializeField] private AudioSource cueSource;
        [SerializeField] private AudioClip enterClip;
        [SerializeField] private AudioClip exitClip;
        [SerializeField, Range(0f, 1f)] private float enterVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float exitVolume = 0.5f;
        [SerializeField] private AudioSource bedSource;
        [SerializeField, Range(0f, 1f), Tooltip("Felt more than heard: if it sounds like music starting, it is too loud.")] private float bedVolume = 0.32f;
        [SerializeField, Min(0f), Tooltip("The bed follows the enter cue and the world filter.")] private float bedDelay = 0.08f;
        [SerializeField, Min(0.01f)] private float bedFadeIn = 0.3f;
        [SerializeField, Min(0.01f)] private float bedFadeOut = 0.2f;
        [SerializeField, Range(0f, 0.03f), Tooltip("Very slow pitch drift of the bed (±).")] private float bedPitchDrift = 0.015f;
        [SerializeField, Min(1f)] private float bedDriftPeriod = 17f;
        [SerializeField, Min(0f), Tooltip("On leaving, the bed fades and the world returns before the exit cue.")] private float exitCueDelay = 0.1f;
        [SerializeField, Tooltip("Rare spatial artefact over the bed; empty to disable.")] private AudioClip artefactClip;
        [SerializeField, Range(0f, 1f)] private float artefactVolume = 0.05f;
        [SerializeField] private Vector2 artefactInterval = new Vector2(2.5f, 6f);

        [Header("Post-process")]
        [SerializeField] private Volume gradeVolume;
        [SerializeField, Min(0.01f)] private float gradeEnterTime = 0.15f;
        [SerializeField, Min(0.01f)] private float gradeExitTime = 0.2f;
        [SerializeField, Range(-100f, 0f)] private float saturation = -20f;
        [SerializeField, Range(0f, 50f)] private float contrast = 8f;
        [SerializeField, Range(-1f, 0f)] private float postExposure = -0.1f;
        [SerializeField, Tooltip("Very slight cold lift, not a blue tint.")] private Color colorFilter = new Color(0.94f, 0.985f, 1f);
        [SerializeField, Range(0f, 0.5f), Tooltip("Light: peripheral vision stays useful.")] private float vignette = 0.18f;
        [SerializeField] private Volume aberrationVolume;
        [SerializeField, Range(0f, 1f)] private float aberrationPeak = 0.12f;
        [SerializeField, Range(0f, 0.1f)] private float aberrationActive = 0.02f;
        [SerializeField, Range(0f, 1f)] private float aberrationExitPeak = 0.06f;
        [SerializeField, Min(0.01f)] private float aberrationRise = 0.04f;
        [SerializeField, Min(0.01f)] private float aberrationSettle = 0.22f;

        [Header("Scan pulse")]
        [SerializeField] private LineRenderer scanRing;
        [SerializeField, Min(0.05f)] private float scanDuration = 0.45f;
        [SerializeField, Min(1f)] private float scanRadius = 12f;
        [SerializeField, Min(0.001f)] private float scanWidth = 0.09f;
        [SerializeField] private Color scanColor = new Color(0.72f, 0.94f, 1f, 0.55f);

        [Header("HUD reveal (seconds after entering)")]
        [SerializeField, Min(0f)] private float markersAt = 0.08f;
        [SerializeField, Min(0f)] private float cardsAt = 0.14f;
        [SerializeField, Min(0f)] private float statusAt = 0.18f;
        [SerializeField, Min(0f)] private float combosAt = 0.22f;

        private ChromaticAberration aberration;
        private bool presenting;
        private float sinceChange = float.PositiveInfinity;
        private float untilArtefact;
        private float driftClock;
        private bool exitCuePending;
        private Vector3 scanOrigin;
        private int scanSegments;

        public bool IsPresenting => presenting;
        public float SinceEnter => presenting ? sinceChange : 0f;
        public float GradeWeight => gradeVolume != null ? gradeVolume.weight : 0f;
        public float AberrationIntensity => aberration != null ? aberration.intensity.value : 0f;
        public float BedLevel => bedSource != null && bedSource.isPlaying ? bedSource.volume : 0f;
        public bool ScanVisible => scanRing != null && scanRing.enabled;
        public float ScanRadiusNow { get; private set; }
        public float ScanRadius => scanRadius;
        public Vector3 ScanOrigin => scanOrigin;
        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }
        public AudioMixerSnapshot CurrentSnapshot { get; private set; }
        public AudioMixer Mixer => mixer;

        public bool Revealed(Stage stage)
        {
            if (!presenting) return false;
            float at = stage == Stage.Markers ? markersAt : stage == Stage.Cards ? cardsAt : stage == Stage.Status ? statusAt : combosAt;
            return SinceEnter >= at;
        }

        private void Awake()
        {
            if (mixer != null) mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            CurrentSnapshot = normalSnapshot;
            // Runtime copies of the profiles: animating them never writes to the assets.
            if (gradeVolume != null)
            {
                gradeVolume.weight = 0f;
                var profile = gradeVolume.profile;
                if (profile.TryGet(out ColorAdjustments color))
                {
                    color.saturation.Override(saturation);
                    color.contrast.Override(contrast);
                    color.postExposure.Override(postExposure);
                    color.colorFilter.Override(colorFilter);
                }
                if (profile.TryGet(out Vignette vignetteEffect)) vignetteEffect.intensity.Override(vignette);
            }
            if (aberrationVolume != null && aberrationVolume.profile.TryGet(out aberration))
            {
                aberrationVolume.weight = 1f;
                aberration.intensity.Override(0f);
            }
            if (bedSource != null) { bedSource.loop = true; bedSource.volume = 0f; bedSource.playOnAwake = false; }
            if (scanRing != null)
            {
                scanSegments = scanRing.positionCount;
                scanRing.useWorldSpace = true;
                scanRing.enabled = false;
            }
        }

        private void LateUpdate()
        {
            bool active = focus != null && focus.isActiveAndEnabled && focus.IsActive;
            float dt = Time.unscaledDeltaTime;
            if (active != presenting) Switch(active);
            else sinceChange += dt;
            float since = sinceChange;

            if (gradeVolume != null)
                gradeVolume.weight = Mathf.MoveTowards(gradeVolume.weight, presenting ? 1f : 0f, dt / (presenting ? gradeEnterTime : gradeExitTime));
            if (aberration != null) aberration.intensity.value = AberrationAt(since);

            if (bedSource != null)
            {
                float target = presenting && since >= bedDelay ? bedVolume : 0f;
                bedSource.pitch = 1f + bedPitchDrift * Mathf.Sin(driftClock * Mathf.PI * 2f / bedDriftPeriod);
                driftClock += dt;
                bedSource.volume = Mathf.MoveTowards(bedSource.volume, target, dt * bedVolume / (presenting ? bedFadeIn : bedFadeOut));
                if (!presenting && bedSource.isPlaying && bedSource.volume <= 0f) bedSource.Stop();
            }
            untilArtefact -= dt;
            if (presenting && artefactClip != null && cueSource != null && untilArtefact <= 0f)
            {
                untilArtefact = Random.Range(artefactInterval.x, artefactInterval.y);
                cueSource.panStereo = Random.Range(-0.6f, 0.6f);
                cueSource.PlayOneShot(artefactClip, artefactVolume);
            }
            if (exitCuePending && !presenting && since >= exitCueDelay)
            {
                exitCuePending = false;
                if (cueSource != null && exitClip != null) { cueSource.panStereo = 0f; cueSource.PlayOneShot(exitClip, exitVolume); }
            }
            UpdateScan(since);
        }

        private void Switch(bool active)
        {
            presenting = active;
            sinceChange = 0f;
            CurrentSnapshot = active ? focusSnapshot : normalSnapshot;
            if (CurrentSnapshot != null) CurrentSnapshot.TransitionTo(active ? snapshotEnterTime : snapshotExitTime);
            exitCuePending = !active;
            if (active && cueSource != null && enterClip != null)
            {
                cueSource.panStereo = 0f;
                cueSource.PlayOneShot(enterClip, enterVolume);
            }
            if (active)
            {
                EnterCount++;
                untilArtefact = artefactInterval.x;
                if (bedSource != null && !bedSource.isPlaying) bedSource.Play();
                var member = squad != null ? squad.ActiveMember : null;
                scanOrigin = member != null ? member.transform.position + Vector3.up * 0.06f : transform.position;
            }
            else ExitCount++;
        }

        // A short spike on entering that settles to a near-invisible value; a smaller spike on leaving, then nothing.
        private float AberrationAt(float since)
        {
            if (presenting)
            {
                if (since < aberrationRise) return Mathf.Lerp(0f, aberrationPeak, since / aberrationRise);
                return Mathf.Lerp(aberrationPeak, aberrationActive, Mathf.SmoothStep(0f, 1f, (since - aberrationRise) / aberrationSettle));
            }
            if (since < aberrationRise) return Mathf.Lerp(aberrationActive, aberrationExitPeak, since / aberrationRise);
            return Mathf.Lerp(aberrationExitPeak, 0f, Mathf.SmoothStep(0f, 1f, (since - aberrationRise) / aberrationSettle));
        }

        // One thin ring travels outward from the active member's feet and fades: the reading has started.
        private void UpdateScan(float since)
        {
            if (scanRing == null) return;
            bool show = presenting && since < scanDuration;
            scanRing.enabled = show;
            if (!show) { ScanRadiusNow = 0f; return; }
            float t = since / scanDuration;
            ScanRadiusNow = scanRadius * (1f - (1f - t) * (1f - t));
            Color color = scanColor;
            color.a *= 1f - t;
            scanRing.startColor = scanRing.endColor = color;
            scanRing.widthMultiplier = scanWidth * (1f - 0.5f * t);
            for (int i = 0; i < scanSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / scanSegments;
                scanRing.SetPosition(i, scanOrigin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ScanRadiusNow);
            }
        }

        private void OnDisable()
        {
            if (presenting && normalSnapshot != null) normalSnapshot.TransitionTo(0f);
            presenting = false;
            if (gradeVolume != null) gradeVolume.weight = 0f;
            if (aberration != null) aberration.intensity.value = 0f;
            if (bedSource != null) { bedSource.Stop(); bedSource.volume = 0f; }
            if (scanRing != null) scanRing.enabled = false;
        }
    }
}
