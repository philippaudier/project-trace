using TRACE.AI;
using TRACE.Characters;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Bottom left: the controlled member. Portrait, name, designation, HP, skill slots (primary, ultimate
    // placeholder, dodge), state. The player's main reference.
    public sealed class ActiveOperatorPanel : HudPanel
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image accentBar;
        [SerializeField] private Image portraitFrame;
        [SerializeField] private Text nameText;
        [SerializeField] private Text roleText;
        [SerializeField] private Text hpText;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text skillText;
        [SerializeField] private Image skillFill;
        [SerializeField] private Text ultimateText;
        [SerializeField] private Text dodgeText;
        [SerializeField] private Text stateText;
        [SerializeField] private Image switchHighlight;
        [SerializeField, Range(0f, 1f)] private float explorationAlpha = 0.8f;
        private SquadMember shown;
        private float longestRemaining;
        public string NameLabel => nameText != null ? nameText.text : "";
        public string RoleLabel => roleText != null ? roleText.text : "";
        public string SkillLabel => skillText != null ? skillText.text : "";
        public string StateLabel => stateText != null ? stateText.text : "";
        public Sprite PortraitSprite => portrait != null ? portrait.sprite : null;

        protected override float TargetAlpha(HudRoot hud) => hud.ActiveMember == null ? 0f : hud.State == HudState.Exploration ? explorationAlpha : 1f;

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            var member = hud.ActiveMember;
            if (member == null) return;
            if (member != shown)
            {
                shown = member;
                longestRemaining = 0f;
                var profile = member.GetComponent<CharacterProfile>();
                Color accent = profile != null ? profile.AccentColor : OffWhite;
                nameText.text = profile != null ? profile.DisplayName.ToUpperInvariant() : member.name.ToUpperInvariant();
                roleText.text = profile != null ? profile.Archetype.ToUpperInvariant() : "";
                if (accentBar != null) accentBar.color = accent;
                if (portraitFrame != null) portraitFrame.color = new Color(accent.r, accent.g, accent.b, 0.9f);
                if (portrait != null)
                {
                    portrait.sprite = profile != null ? profile.Portrait : null;
                    portrait.enabled = portrait.sprite != null;
                }
                if (ultimateText != null) ultimateText.text = $"<color=#{Hex(Muted)}>R  ULTIMATE</color>";
            }
            if (switchHighlight != null)
                switchHighlight.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.35f - (Time.unscaledTime - hud.ActiveChangedAt) * 0.9f));
            if (!refresh) return;
            var health = member.Health;
            float ratio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;
            hpFill.fillAmount = ratio;
            hpFill.color = ratio > 0.35f ? OffWhite : Danger;
            hpText.text = $"{health.CurrentHealth:0} / {health.MaxHealth:0}";
            var skill = member.GetComponent<CharacterSkill>();
            if (skill != null)
            {
                float remaining = skill.CooldownRemaining;
                if (skill.IsReady) longestRemaining = 0f; else longestRemaining = Mathf.Max(longestRemaining, remaining);
                skillText.text = skill.IsReady ? $"E  {skill.SkillName.ToUpperInvariant()}  <color=#{Hex(Cyan)}>READY</color>" :
                    $"E  {skill.SkillName.ToUpperInvariant()}  <color=#{Hex(Muted)}>{remaining:0.0}s</color>";
                skillFill.fillAmount = skill.IsReady ? 1f : Mathf.Clamp01(1f - remaining / Mathf.Max(0.01f, longestRemaining));
                skillFill.color = skill.IsReady ? Cyan : Muted;
            }
            var receiver = member.Receiver;
            bool evading = receiver != null && receiver.IsInvulnerable;
            if (dodgeText != null) dodgeText.text = evading ? $"<color=#{Hex(Cyan)}>RMB  DODGE</color>" : $"<color=#{Hex(Muted)}>RMB  DODGE</color>";
            stateText.text = health.IsDead ? $"<color=#{Hex(Danger)}>DOWN</color>" :
                evading ? $"<color=#{Hex(Cyan)}>EVADE</color>" :
                hud.State == HudState.Focus ? $"<color=#{Hex(Cyan)}>ANALYSIS</color>" :
                hud.State == HudState.Combat ? $"<color=#{Hex(Amber)}>ENGAGED</color>" : $"<color=#{Hex(Muted)}>FIELD</color>";
        }
    }
}
