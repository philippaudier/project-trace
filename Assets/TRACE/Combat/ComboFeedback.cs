using UnityEngine;

namespace TRACE.Combat
{
    [RequireComponent(typeof(ComboOpportunity))]
    public sealed class ComboFeedback : MonoBehaviour
    {
        [SerializeField] private GameObject readyMarker;
        [SerializeField] private TextMesh label;
        [SerializeField] private Transform impactRing;
        [SerializeField] private Renderer impactRenderer;
        [SerializeField, Min(0.05f)] private float impactDuration = 0.35f;
        [SerializeField] private string protectedInstruction = "Clic gauche";
        private ComboOpportunity opportunity;
        private UnityEngine.Camera view;
        private MaterialPropertyBlock properties;
        private Renderer readyRenderer;
        public ComboOpportunity Opportunity => opportunity;
        public bool IsShowingImpact => Time.time - opportunity.ConsumedAt < impactDuration;
        public void SetDetailVisible(bool visible) => label.gameObject.SetActive(visible);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            opportunity = GetComponent<ComboOpportunity>();
            view = UnityEngine.Camera.main;
            properties = new MaterialPropertyBlock();
            readyRenderer = readyMarker.GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            bool ready = opportunity.Type != ComboOpportunityType.None;
            bool impact = IsShowingImpact;
            readyMarker.SetActive(ready);
            if (ready)
            {
                readyRenderer.GetPropertyBlock(properties);
                properties.SetColor(BaseColor, opportunity.Type == ComboOpportunityType.Grouped ? new Color(1f, 0.85f, 0.15f) : Color.cyan);
                readyRenderer.SetPropertyBlock(properties);
            }
            label.gameObject.SetActive(ready || impact);
            if (ready || impact)
            {
                bool grouped = (impact ? opportunity.LastConsumedType : opportunity.Type) == ComboOpportunityType.Grouped;
                label.text = impact ? (grouped ? "COMBO !" : "CONTRE !") :
                    grouped ? $"COMBO  {opportunity.RemainingDuration:0.0}s\n1 puis E : DASH" :
                    $"CONTRE  {opportunity.RemainingDuration:0.0}s\n{protectedInstruction}";
                label.color = grouped ? new Color(1f, 0.85f, 0.15f) : Color.cyan;
                if (view != null) label.transform.rotation = view.transform.rotation;
            }
            impactRing.gameObject.SetActive(impact);
            if (!impact) return;
            float progress = Mathf.Clamp01((Time.time - opportunity.ConsumedAt) / impactDuration);
            float radius = opportunity.LastConsumedType == ComboOpportunityType.Grouped ? 2f : 1.2f;
            impactRing.localScale = Vector3.one * Mathf.Lerp(0.35f, radius, progress);
            impactRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, Color.Lerp(Color.white,
                opportunity.LastConsumedType == ComboOpportunityType.Grouped ? new Color(1f, 0.4f, 0f) : Color.cyan, progress));
            impactRenderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            if (readyMarker != null) readyMarker.SetActive(false);
            if (label != null) label.gameObject.SetActive(false);
            if (impactRing != null) impactRing.gameObject.SetActive(false);
        }
    }
}
