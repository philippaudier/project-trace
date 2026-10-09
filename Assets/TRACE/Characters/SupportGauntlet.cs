using TRACE.Combat;
using TRACE.Skills;
using UnityEngine;

namespace TRACE.Characters
{
    // Support's signature equipment: the forearm barrier emitter. It idles low; when Pulse Shield fires it opens
    // its plates, flares, projects a short hexagonal barrier past the hand and sends a brief link to every shielded
    // member, so the halos read as coming from her gauntlet. Game time, so Tactical Focus slows it like everything
    // else. Visual only: PulseShield and Shield own the mechanics.
    [DisallowMultipleComponent]
    public sealed class SupportGauntlet : MonoBehaviour
    {
        [SerializeField] private CharacterSkill skill;
        [SerializeField] private Shield[] shields = new Shield[0];
        [SerializeField] private Renderer[] emitters = new Renderer[0];
        [SerializeField] private Transform emitterPoint;
        [SerializeField] private Transform[] plates = new Transform[0];
        [SerializeField, Min(0f), Tooltip("Metres each plate slides outward when deployed.")] private float plateTravel = 0.025f;
        [SerializeField] private Transform projection;
        [SerializeField] private LineRenderer[] tethers = new LineRenderer[0];
        [SerializeField] private Color emissionColor = new Color(0.36f, 0.9f, 0.74f);
        [SerializeField, Min(0f)] private float idleIntensity = 0.6f;
        [SerializeField, Min(0f)] private float deployedIntensity = 1.6f;
        [SerializeField, Min(0f)] private float flareIntensity = 3f;
        [SerializeField, Min(0.05f)] private float projectionTime = 0.6f;
        [SerializeField, Min(0.05f)] private float tetherTime = 0.45f;
        [SerializeField, Min(0.01f)] private float responseTime = 0.15f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock properties;
        private Vector3[] plateRest;
        private LineRenderer[] projectionLines;
        private float[] projectionWidths;
        private float tetherWidth;
        private float sinceActivation = float.PositiveInfinity;

        public float CurrentIntensity { get; private set; }
        public float IdleIntensity => idleIntensity;
        public float DeployAmount { get; private set; }
        public bool IsProjecting => projection != null && projection.gameObject.activeSelf;
        public bool IsLinking => tethers.Length > 0 && tethers[0] != null && tethers[0].enabled;
        public int TetherCount => tethers.Length;
        public bool IsShieldActive
        {
            get
            {
                foreach (var shield in shields) if (shield != null && shield.CurrentAmount > 0f) return true;
                return false;
            }
        }

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            plateRest = new Vector3[plates.Length];
            for (int i = 0; i < plates.Length; i++) plateRest[i] = plates[i] != null ? plates[i].localPosition : Vector3.zero;
            projectionLines = projection != null ? projection.GetComponentsInChildren<LineRenderer>(true) : new LineRenderer[0];
            projectionWidths = new float[projectionLines.Length];
            for (int i = 0; i < projectionLines.Length; i++) projectionWidths[i] = projectionLines[i].widthMultiplier;
            tetherWidth = tethers.Length > 0 && tethers[0] != null ? tethers[0].widthMultiplier : 0.02f;
            CurrentIntensity = idleIntensity;
            Hide();
            ApplyEmission();
        }

        private void OnEnable() { if (skill != null) skill.Activated += OnActivated; }
        private void OnDisable() { if (skill != null) skill.Activated -= OnActivated; Hide(); }

        private void OnActivated() => sinceActivation = 0f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            sinceActivation += dt;
            bool active = IsShieldActive;
            float blend = 1f - Mathf.Exp(-dt / responseTime);
            DeployAmount = Mathf.Lerp(DeployAmount, active || sinceActivation < projectionTime ? 1f : 0f, blend);
            float flare = Mathf.Clamp01(1f - sinceActivation / projectionTime);
            float target = Mathf.Lerp(active ? deployedIntensity : idleIntensity, flareIntensity, flare);
            CurrentIntensity = Mathf.Lerp(CurrentIntensity, target, blend);
            ApplyEmission();
            for (int i = 0; i < plates.Length; i++)
            {
                if (plates[i] == null) continue;
                Vector3 outward = new Vector3(plateRest[i].x, 0f, plateRest[i].z);
                plates[i].localPosition = plateRest[i] + (outward.sqrMagnitude > 0f ? outward.normalized : Vector3.zero) * plateTravel * DeployAmount;
            }
            UpdateProjection();
            UpdateTethers();
        }

        // The barrier opens past the hand, holds, then thins out.
        private void UpdateProjection()
        {
            if (projection == null) return;
            bool show = sinceActivation < projectionTime;
            if (projection.gameObject.activeSelf != show) projection.gameObject.SetActive(show);
            if (!show) return;
            float t = sinceActivation / projectionTime;
            projection.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(t / 0.35f));
            float fade = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
            for (int i = 0; i < projectionLines.Length; i++) projectionLines[i].widthMultiplier = projectionWidths[i] * fade;
        }

        // A short emitter-to-member link per shield, so each halo visibly comes from her.
        private void UpdateTethers()
        {
            bool show = sinceActivation < tetherTime && emitterPoint != null;
            float fade = 1f - Mathf.Clamp01(sinceActivation / tetherTime);
            for (int i = 0; i < tethers.Length; i++)
            {
                var line = tethers[i];
                if (line == null) continue;
                var target = i < shields.Length ? shields[i] : null;
                bool visible = show && target != null && target.CurrentAmount > 0f && !transform.IsChildOf(target.transform);
                line.enabled = visible;
                if (!visible) continue;
                Vector3 from = emitterPoint.position;
                Vector3 to = target.transform.position + Vector3.up * 1.0f;
                line.SetPosition(0, from);
                line.SetPosition(1, Vector3.Lerp(from, to, Mathf.Clamp01(sinceActivation / (tetherTime * 0.4f))));
                line.widthMultiplier = tetherWidth * fade;
            }
        }

        private void Hide()
        {
            if (projection != null) projection.gameObject.SetActive(false);
            foreach (var line in tethers) if (line != null) line.enabled = false;
        }

        private void ApplyEmission()
        {
            foreach (var emitter in emitters)
            {
                if (emitter == null) continue;
                emitter.GetPropertyBlock(properties);
                properties.SetColor(EmissionColor, emissionColor * CurrentIntensity);
                emitter.SetPropertyBlock(properties);
            }
        }
    }
}
