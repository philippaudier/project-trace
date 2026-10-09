using System.Collections.Generic;
using TRACE.AI;
using TRACE.Combat;
using UnityEngine;

namespace TRACE.Tactical
{
    // Tactical Focus reading aids on the existing enemies, shown only while Focus is presented and after the marker
    // reveal. A thin ground ring per nearby enemy, told apart by width, size and motion as well as colour:
    // standard (thin, dim, still), locked (thick, bright, still), threat (pulsing fast), combo (slow breathing).
    // Intention ghost: during a melee wind-up, a faint copy of the enemy's silhouette leans toward its strike, the
    // action already perceptible before it lands. Outline: each enemy renderer carries one Tactical rendering layer
    // (standard / locked / threat / combo) that TacticalOutlineFeature draws; this only flips the layer bit when the
    // tier changes and drives a few shader globals (fade, clock, scan reveal, distance range). No material instances,
    // no property blocks, no per-frame allocation. No gameplay reads, no physics.
    [DefaultExecutionOrder(35)]
    [DisallowMultipleComponent]
    public sealed class TacticalReadability : MonoBehaviour
    {
        public enum Marker { None, Standard, Locked, Threat, Combo }
        private static readonly int FadeId = Shader.PropertyToID("_TacticalOutlineFade");
        private static readonly int TimeId = Shader.PropertyToID("_TacticalOutlineTime");
        private static readonly int RevealId = Shader.PropertyToID("_TacticalOutlineReveal");
        private static readonly int RangeId = Shader.PropertyToID("_TacticalOutlineRange");

        [SerializeField] private TacticalFocusPresentationController presentation;
        [SerializeField] private SquadController squad;
        [SerializeField] private TargetingSystem targeting;
        [SerializeField] private EnemyBrain[] enemies = new EnemyBrain[0];
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material ghostMaterial;
        [SerializeField, Min(1f)] private float range = 18f;
        [SerializeField, Min(8)] private int ringSegments = 40;

        [Header("Markers")]
        [SerializeField] private Color standardColor = new Color(0.86f, 0.9f, 0.95f, 0.22f);
        [SerializeField] private Color lockedColor = new Color(0.37f, 0.84f, 1f, 0.95f);
        [SerializeField] private Color threatColor = new Color(1f, 0.46f, 0.3f, 0.95f);
        [SerializeField] private Color comboColor = new Color(1f, 0.78f, 0.16f, 0.9f);
        [SerializeField] private Vector4 widths = new Vector4(0.025f, 0.06f, 0.07f, 0.05f);
        [SerializeField] private Vector4 radii = new Vector4(0.7f, 0.85f, 0.9f, 0.8f);
        [SerializeField, Min(0f)] private float threatPulseHz = 5f;
        [SerializeField, Min(0f)] private float comboPulseHz = 1.6f;

        [Header("Outline")]
        [SerializeField, Tooltip("Rendering layers drawn by TacticalOutlineFeature: standard, locked, threat, combo.")]
        private int[] outlineLayers = { 8, 9, 10, 11 };
        [SerializeField, Min(0.01f)] private float outlineFadeIn = 0.14f;
        [SerializeField, Min(0.01f)] private float outlineFadeOut = 0.11f;
        [SerializeField, Tooltip("Camera distance: full outline until x, gone at y.")] private Vector2 outlineRange = new Vector2(15f, 25f);
        [SerializeField, Min(0f), Tooltip("Longest wait for the scan front before every outline is revealed.")] private float outlineRevealCap = 0.18f;
        [SerializeField, Tooltip("Development only: outlines on every enemy, Focus or not.")] private bool showTacticalOutlines;

        [Header("Intention ghost")]
        [SerializeField, Range(0f, 1f), Tooltip("Kept below the outline and the telegraph.")] private float ghostOpacity = 0.14f;
        [SerializeField] private Color ghostColor = new Color(0.55f, 0.9f, 1f, 1f);
        [SerializeField, Min(0f), Tooltip("How far the ghost leans toward the strike at the end of the wind-up.")] private float ghostLean = 0.7f;

        private LineRenderer[] rings;
        private Marker[] markers;
        private Health[] healths;
        private ComboOpportunity[] combos;
        private BasicMeleeEnemy[] melee;
        private GameObject[] ghosts;
        private MeshRenderer[] ghostRenderers;
        private float[] windupStart;
        private Transform[] visuals;
        private Renderer[] bodies;
        private Renderer[][] outlined;
        private uint[][] baseMasks;
        private Marker[] tiers;
        private float outlineFade;
        private float outlineClock;
        private MaterialPropertyBlock block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public Marker MarkerOf(EnemyBrain enemy)
        {
            int i = System.Array.IndexOf(enemies, enemy);
            return i >= 0 && markers != null ? markers[i] : Marker.None;
        }
        public Marker OutlineOf(EnemyBrain enemy)
        {
            int i = System.Array.IndexOf(enemies, enemy);
            return i >= 0 && tiers != null && outlineFade > 0f ? tiers[i] : Marker.None;
        }
        public float OutlineFade => outlineFade;
        public uint OutlineLayerBit(Marker tier) => tier == Marker.None ? 0u : 1u << outlineLayers[(int)tier - 1];
        public IReadOnlyList<Renderer> OutlinedRenderers(EnemyBrain enemy)
        {
            int i = System.Array.IndexOf(enemies, enemy);
            return i >= 0 && outlined != null ? outlined[i] : System.Array.Empty<Renderer>();
        }
        public bool ShowTacticalOutlines { get => showTacticalOutlines; set => showTacticalOutlines = value; }
        public bool GhostVisible(EnemyBrain enemy)
        {
            int i = System.Array.IndexOf(enemies, enemy);
            return i >= 0 && ghosts != null && ghosts[i] != null && ghosts[i].activeSelf;
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            int count = enemies.Length;
            rings = new LineRenderer[count];
            markers = new Marker[count];
            healths = new Health[count];
            combos = new ComboOpportunity[count];
            melee = new BasicMeleeEnemy[count];
            ghosts = new GameObject[count];
            ghostRenderers = new MeshRenderer[count];
            windupStart = new float[count];
            visuals = new Transform[count];
            bodies = new Renderer[count];
            outlined = new Renderer[count][];
            baseMasks = new uint[count][];
            tiers = new Marker[count];
            for (int i = 0; i < count; i++)
            {
                if (enemies[i] == null) continue;
                healths[i] = enemies[i].GetComponent<Health>();
                combos[i] = enemies[i].GetComponent<ComboOpportunity>();
                melee[i] = enemies[i] as BasicMeleeEnemy;
                rings[i] = CreateRing(enemies[i].name);
                CollectOutlined(i);
                var named = enemies[i].transform.Find("Enemy Visual");
                bodies[i] = named != null ? named.GetComponent<Renderer>() : enemies[i].GetComponentInChildren<MeshRenderer>();
                if (melee[i] != null) CreateGhost(i);
            }
        }

        private LineRenderer CreateRing(string owner)
        {
            var go = new GameObject("Focus Marker " + owner);
            go.layer = 2;
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = ringSegments;
            line.sharedMaterial = lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        // A silhouette copy of the enemy's visual mesh with a faint transparent material.
        private void CreateGhost(int i)
        {
            var named = enemies[i].transform.Find("Enemy Visual");
            var source = named != null ? named.GetComponent<MeshFilter>() : null;
            if (source == null) source = enemies[i].GetComponentInChildren<MeshFilter>();
            if (source == null || source.sharedMesh == null) return;
            visuals[i] = source.transform;
            var go = new GameObject("Intention Ghost " + enemies[i].name);
            go.layer = 2;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ghostMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.localScale = source.transform.lossyScale;
            go.SetActive(false);
            ghosts[i] = go;
            ghostRenderers[i] = renderer;
        }

        private void LateUpdate()
        {
            bool reveal = presentation != null && presentation.Revealed(TacticalFocusPresentationController.Stage.Markers);
            bool combosShown = presentation != null && presentation.Revealed(TacticalFocusPresentationController.Stage.Combos);
            var active = squad != null ? squad.ActiveMember : null;
            var locked = targeting != null && targeting.IsLocked ? targeting.LockedTarget : null;
            float time = Time.unscaledTime;
            UpdateOutlineGlobals();
            bool outlining = outlineFade > 0f;
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                bool relevant = reveal && enemy != null && enemy.isActiveAndEnabled && healths[i] != null && !healths[i].IsDead &&
                    active != null && Vector3.Distance(enemy.transform.position, active.transform.position) <= range;
                markers[i] = !relevant ? Marker.None :
                    enemy.IsPreparingAttack ? Marker.Threat :
                    locked == healths[i] ? Marker.Locked :
                    combosShown && combos[i] != null && combos[i].Type != ComboOpportunityType.None ? Marker.Combo : Marker.Standard;
                DrawRing(i, time);
                UpdateGhost(i, relevant);
                // Outline tier: same priority as the markers, but for every live enemy the camera could see
                // within the outline range; the shader fades distance and respects occlusion.
                bool alive = outlining && enemy != null && enemy.gameObject.activeInHierarchy && healths[i] != null && !healths[i].IsDead && active != null &&
                    Vector3.Distance(enemy.transform.position, active.transform.position) <= outlineRange.y + 5f;
                SetTier(i, !alive ? Marker.None :
                    enemy.IsPreparingAttack ? Marker.Threat :
                    locked == healths[i] ? Marker.Locked :
                    (combosShown || showTacticalOutlines) && combos[i] != null && combos[i].Type != ComboOpportunityType.None ? Marker.Combo : Marker.Standard);
            }
        }

        // Every mesh renderer of the enemy (body, guard plate, barrel…); telegraphs and aim lines are line renderers
        // and stay out. Found once, so future enemies are included without per-prefab wiring. Base masks are kept.
        private void CollectOutlined(int i)
        {
            var found = new List<Renderer>();
            foreach (var renderer in enemies[i].GetComponentsInChildren<Renderer>(true))
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) found.Add(renderer);
            outlined[i] = found.ToArray();
            baseMasks[i] = new uint[outlined[i].Length];
            for (int r = 0; r < outlined[i].Length; r++) baseMasks[i][r] = outlined[i][r].renderingLayerMask;
        }

        private void SetTier(int i, Marker tier)
        {
            if (tiers[i] == tier || outlined[i] == null) return;
            tiers[i] = tier;
            uint bit = OutlineLayerBit(tier);
            for (int r = 0; r < outlined[i].Length; r++)
                if (outlined[i][r] != null) outlined[i][r].renderingLayerMask = baseMasks[i][r] | bit;
        }

        // Fade on unscaled time; reveal follows the scan front, capped so reading never waits long.
        private void UpdateOutlineGlobals()
        {
            float dt = Time.unscaledDeltaTime;
            bool presenting = presentation != null && presentation.IsPresenting;
            bool show = presenting || showTacticalOutlines;
            outlineFade = Mathf.MoveTowards(outlineFade, show ? 1f : 0f, dt / (show ? outlineFadeIn : outlineFadeOut));
            outlineClock += dt;
            float radius = presenting && presentation.SinceEnter < outlineRevealCap ? Mathf.Max(0f, presentation.ScanRadiusNow - 0.5f) : 10000f;
            Vector3 origin = presentation != null ? presentation.ScanOrigin : Vector3.zero;
            Shader.SetGlobalFloat(FadeId, outlineFade);
            Shader.SetGlobalFloat(TimeId, outlineClock);
            Shader.SetGlobalVector(RevealId, new Vector4(origin.x, origin.y, origin.z, radius));
            Shader.SetGlobalVector(RangeId, new Vector4(outlineRange.x, outlineRange.y, 0f, 0f));
        }

        private void DrawRing(int i, float time)
        {
            var ring = rings[i];
            if (ring == null) return;
            var marker = markers[i];
            ring.enabled = marker != Marker.None;
            if (!ring.enabled) return;
            int k = (int)marker - 1;
            float radius = radii[k];
            Color color = marker == Marker.Standard ? standardColor : marker == Marker.Locked ? lockedColor : marker == Marker.Threat ? threatColor : comboColor;
            if (marker == Marker.Threat) radius *= 1f + 0.12f * Mathf.Sin(time * threatPulseHz * Mathf.PI * 2f);
            if (marker == Marker.Combo) color.a *= 0.7f + 0.3f * Mathf.Sin(time * comboPulseHz * Mathf.PI * 2f);
            ring.startColor = ring.endColor = color;
            ring.widthMultiplier = widths[k];
            // On the ground under the body: enemy pivots are not always at the feet.
            Vector3 centre = enemies[i].transform.position;
            if (bodies[i] != null) { var b = bodies[i].bounds; centre = new Vector3(b.center.x, b.min.y, b.center.z); }
            centre += Vector3.up * 0.05f;
            for (int s = 0; s < ring.positionCount; s++)
            {
                float angle = s * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(s, centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        private void UpdateGhost(int i, bool relevant)
        {
            var ghost = ghosts[i];
            if (ghost == null) return;
            var enemy = melee[i];
            bool winding = relevant && enemy.Phase == BasicMeleeEnemy.AttackPhase.Windup;
            if (!winding) { windupStart[i] = -1f; if (ghost.activeSelf) ghost.SetActive(false); return; }
            if (windupStart[i] < 0f) windupStart[i] = enemy.WindupRemaining;
            float progress = windupStart[i] > 0f ? 1f - enemy.WindupRemaining / windupStart[i] : 1f;
            if (!ghost.activeSelf) ghost.SetActive(true);
            var visual = visuals[i];
            Vector3 strike = enemy.StrikeDirection;
            strike.y = 0f;
            if (strike.sqrMagnitude < 0.0001f) strike = enemy.transform.forward;
            strike.Normalize();
            ghost.transform.SetPositionAndRotation(visual.position + strike * ghostLean * Mathf.SmoothStep(0.2f, 1f, progress),
                Quaternion.LookRotation(strike, Vector3.up) * Quaternion.Inverse(enemy.transform.rotation) * visual.rotation);
            Color color = ghostColor;
            color.a = ghostOpacity * Mathf.Lerp(0.4f, 1f, progress);
            ghostRenderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            ghostRenderers[i].SetPropertyBlock(block);
        }

        private void OnDisable()
        {
            outlineFade = 0f;
            Shader.SetGlobalFloat(FadeId, 0f);
            if (rings == null) return;
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] != null) rings[i].enabled = false;
                if (ghosts[i] != null) ghosts[i].SetActive(false);
                markers[i] = Marker.None;
                SetTier(i, Marker.None);
            }
        }
    }
}
