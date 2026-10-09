using System;
using TRACE.Combat;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Top centre: the relevant enemy. Shown for a hard lock, or for the soft target while in combat.
    // A locked target gets the cyan frame and the LOCK tag; a soft target stays muted. Enemy HP reads warning red.
    // A regular enemy gets a thin compact strip; an elite or boss gets the wider, heavier layout.
    public sealed class TargetPanel : HudPanel
    {
        [Serializable]
        public struct Layout
        {
            public Vector2 size;
            public float nameX;
            public int nameSize;
            public int labelSize;
            public float barY;
            public float barHeight;
            public float infoY;
        }

        [SerializeField] private Image frame;
        [SerializeField] private Text nameText;
        [SerializeField] private Text tagText;
        [SerializeField] private Text hpText;
        [SerializeField] private Image hpBack;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text statusText;
        [SerializeField] private Text comboText;
        [SerializeField] private Layout compact = new Layout { size = new Vector2(280f, 40f), nameX = 46f, nameSize = 13, labelSize = 9, barY = -21f, barHeight = 4f, infoY = -28f };
        [SerializeField] private Layout elite = new Layout { size = new Vector2(420f, 64f), nameX = 60f, nameSize = 17, labelSize = 11, barY = -28f, barHeight = 8f, infoY = -41f };
        private bool? eliteShown;
        public string NameLabel => nameText != null ? nameText.text : "";
        public string TagLabel => tagText != null ? tagText.text : "";
        public string ComboLabel => comboText != null ? comboText.text : "";
        public string StatusLabel => statusText != null ? statusText.text : "";
        public bool IsEliteLayout => eliteShown == true;
        public Vector2 Size => ((RectTransform)transform).sizeDelta;

        protected override Vector2 SlideDirection => Vector2.up;
        protected override float TargetAlpha(HudRoot hud) => hud.TargetHealth == null ? 0f : hud.IsLocked || hud.State != HudState.Exploration ? 1f : 0f;

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            var health = hud.TargetHealth;
            if (health == null || !refresh) return;
            var brain = hud.TargetBrain;
            bool isElite = brain != null && brain.IsElite;
            if (eliteShown != isElite) Apply(isElite ? elite : compact, isElite);
            nameText.text = brain != null ? brain.Archetype : health.name.ToUpperInvariant();
            tagText.text = hud.IsLocked ? $"<color=#{Hex(Cyan)}>LOCK</color>" : $"<color=#{Hex(Muted)}>TARGET</color>";
            Color frameColor = hud.IsLocked ? Cyan : Muted;
            frameColor.a = hud.IsLocked ? 0.9f : 0.35f;
            if (frame != null) frame.color = frameColor;
            float ratio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;
            hpFill.fillAmount = ratio;
            hpFill.color = Danger;
            hpText.text = $"{health.CurrentHealth:0} / {health.MaxHealth:0}";
            string status = brain != null ? brain.StatusLabel : "";
            var guard = health.GetComponent<FrontalGuard>();
            if (guard != null) status += (status.Length > 0 ? "   ·   " : "") + "GUARD";
            var gravity = health.GetComponent<EnemyGravityResponse>();
            if (gravity != null && gravity.IsSlowed) status += (status.Length > 0 ? "   ·   " : "") + $"<color=#{Hex(Cyan)}>SLOWED</color>";
            statusText.text = status;
            var combo = health.GetComponent<ComboOpportunity>();
            comboText.text = combo != null && combo.Type == ComboOpportunityType.Grouped ?
                $"<color=#{Hex(Amber)}>COMBO 1+E  {combo.RemainingDuration:0.0}s</color>" : "";
        }

        // Resizes the panel and moves its rows; bars stretch with the panel width.
        private void Apply(Layout layout, bool isElite)
        {
            eliteShown = isElite;
            ((RectTransform)transform).sizeDelta = layout.size;
            Place(tagText, new Vector2(12f, -(layout.nameSize - layout.labelSize) * 0.5f - 5f), layout.labelSize);
            Place(nameText, new Vector2(layout.nameX, -4f), layout.nameSize);
            Place(hpText, new Vector2(-12f, -(layout.nameSize - layout.labelSize) * 0.5f - 5f), layout.labelSize + 1);
            Place(statusText, new Vector2(12f, layout.infoY), layout.labelSize);
            Place(comboText, new Vector2(-12f, layout.infoY), layout.labelSize);
            Stretch(hpBack, layout.barY, layout.barHeight);
            Stretch(hpFill, layout.barY, layout.barHeight);
        }

        private static void Place(Text text, Vector2 position, int size)
        {
            if (text == null) return;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, size + 4f);
            text.fontSize = size;
        }

        private static void Stretch(Image bar, float y, float height)
        {
            if (bar == null) return;
            var rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(12f, y - height);
            rect.offsetMax = new Vector2(-12f, y);
        }
    }
}
