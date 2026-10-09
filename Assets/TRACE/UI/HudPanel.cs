using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Base of every HUD panel: a CanvasGroup faded and slid in real time toward the visibility the panel
    // decides for the current HUD state. Text refreshes are throttled so cards stay cheap.
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class HudPanel : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float fadeSpeed = 7f;
        [SerializeField, Min(0f)] private float slideDistance = 10f;
        [SerializeField, Min(0.01f)] private float textRefreshInterval = 0.05f;
        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 restPosition;
        private float targetAlpha;
        private float nextRefresh;
        private bool initialised;

        public float Alpha => group != null ? group.alpha : 0f;
        public bool IsShown => targetAlpha > 0.01f;
        // Concept palette: charcoal UI, neutral grey panels, off-white text, ORIGIN amber, analysis cyan, warning.
        public static readonly Color Charcoal = new Color(0.07f, 0.075f, 0.085f, 0.82f);
        public static readonly Color PanelGrey = new Color(0.16f, 0.17f, 0.185f, 0.7f);
        public static readonly Color OffWhite = new Color(0.92f, 0.92f, 0.9f);
        public static readonly Color Muted = new Color(0.62f, 0.65f, 0.69f);
        public static readonly Color Amber = new Color(1f, 0.776f, 0.161f);
        public static readonly Color Cyan = new Color(0.369f, 0.839f, 1f);
        public static readonly Color Danger = new Color(0.96f, 0.36f, 0.26f);

        protected virtual void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = GetComponent<RectTransform>();
            restPosition = rect.anchoredPosition;
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
        }

        // Called by HudRoot every frame. Panels decide their target visibility and refresh content.
        public void Tick(HudRoot hud)
        {
            if (!initialised) { initialised = true; OnBind(hud); }
            targetAlpha = Mathf.Clamp01(TargetAlpha(hud));
            bool refresh = Time.unscaledTime >= nextRefresh;
            if (refresh) nextRefresh = Time.unscaledTime + textRefreshInterval;
            OnTick(hud, refresh);
            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, dt * fadeSpeed);
            // A hidden panel rests slightly off its place and slides in as it fades.
            float hidden = 1f - Mathf.Clamp01(group.alpha / Mathf.Max(0.01f, targetAlpha));
            rect.anchoredPosition = restPosition + SlideDirection * (IsShown ? hidden * slideDistance : slideDistance * (1f - group.alpha));
        }

        protected virtual Vector2 SlideDirection => Vector2.down;
        protected virtual void OnBind(HudRoot hud) { }
        protected abstract float TargetAlpha(HudRoot hud);
        protected abstract void OnTick(HudRoot hud, bool refresh);

        protected static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        // Identity colour on portrait frames and accent bars; slightly lifted under Tactical Focus so it survives
        // the desaturated grade.
        public static Color AccentFor(Color accent, bool focused, float alpha = 1f)
        {
            Color color = focused ? Color.Lerp(accent, Color.white, 0.25f) : accent;
            color.a = alpha;
            return color;
        }

        // Shows the given portrait, or the neutral fallback; the image is never left empty or broken.
        public static void ShowPortrait(Image image, Sprite sprite, Sprite fallback)
        {
            if (image == null) return;
            image.sprite = sprite != null ? sprite : fallback;
            image.enabled = image.sprite != null;
        }
    }
}
