using TRACE.Encounter;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Screen-edge warnings for off-screen attacks aimed at the controlled member. Renders the hints that
    // ThreatIndicator computes; the indicator keeps the detection logic.
    public sealed class ThreatIndicatorView : HudPanel
    {
        [SerializeField] private ThreatIndicator source;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform[] markers = new RectTransform[0];
        [SerializeField] private Text[] labels = new Text[0];
        public int VisibleCount { get; private set; }

        protected override Vector2 SlideDirection => Vector2.zero;
        protected override float TargetAlpha(HudRoot hud) => source != null && source.Hints.Count > 0 ? 1f : 0f;

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            VisibleCount = 0;
            if (source == null || canvasRect == null) return;
            Vector2 size = canvasRect.rect.size;
            float width = Mathf.Max(1f, Screen.width), height = Mathf.Max(1f, Screen.height);
            for (int i = 0; i < markers.Length; i++)
            {
                bool active = i < source.Hints.Count;
                if (markers[i].gameObject.activeSelf != active) markers[i].gameObject.SetActive(active);
                if (!active) continue;
                var hint = source.Hints[i];
                markers[i].anchoredPosition = new Vector2(hint.ScreenPosition.x / width * size.x, hint.ScreenPosition.y / height * size.y);
                if (i < labels.Length && labels[i] != null) labels[i].text = hint.Text;
                VisibleCount++;
            }
        }
    }
}
