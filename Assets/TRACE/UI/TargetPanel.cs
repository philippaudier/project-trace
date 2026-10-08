using TRACE.Combat;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Top centre: the relevant enemy. Shown for a hard lock, or for the soft target while in combat.
    // A locked target gets the cyan frame and the LOCK tag; a soft target stays muted. Enemy HP reads warning red.
    public sealed class TargetPanel : HudPanel
    {
        [SerializeField] private Image frame;
        [SerializeField] private Text nameText;
        [SerializeField] private Text tagText;
        [SerializeField] private Text hpText;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text statusText;
        [SerializeField] private Text comboText;
        public string NameLabel => nameText != null ? nameText.text : "";
        public string TagLabel => tagText != null ? tagText.text : "";
        public string ComboLabel => comboText != null ? comboText.text : "";
        public string StatusLabel => statusText != null ? statusText.text : "";

        protected override Vector2 SlideDirection => Vector2.up;
        protected override float TargetAlpha(HudRoot hud) => hud.TargetHealth == null ? 0f : hud.IsLocked || hud.State != HudState.Exploration ? 1f : 0f;

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            var health = hud.TargetHealth;
            if (health == null || !refresh) return;
            var brain = hud.TargetBrain;
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
            if (guard != null) status += (status.Length > 0 ? "   ·   " : "") + "GARDE FRONTALE";
            var gravity = health.GetComponent<EnemyGravityResponse>();
            if (gravity != null && gravity.IsSlowed) status += (status.Length > 0 ? "   ·   " : "") + $"<color=#{Hex(Cyan)}>SLOWED</color>";
            statusText.text = status;
            var combo = health.GetComponent<ComboOpportunity>();
            comboText.text = combo != null && combo.Type == ComboOpportunityType.Grouped ?
                $"<color=#{Hex(Amber)}>COMBO  GROUPED {combo.RemainingDuration:0.0}s   ·   1 + E</color>" : "";
        }
    }
}
