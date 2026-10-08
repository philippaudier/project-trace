using System;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Bottom right: the two members not under player control. Compact cards; richer during combat and Focus.
    public sealed class SquadStatusPanel : HudPanel
    {
        [Serializable]
        public sealed class Card
        {
            public RectTransform root;
            public Image portrait;
            public Image accent;
            public Text nameText;
            public Image hpFill;
            public Text skillText;
            public Text statusText;
            public Image comboTag;
            [NonSerialized] public SquadMember member;
        }

        [SerializeField] private Card[] cards = Array.Empty<Card>();
        [SerializeField, Range(0f, 1f)] private float explorationAlpha = 0.6f;
        public string CardLabel(int index) => index < cards.Length && cards[index].nameText != null ? cards[index].nameText.text : "";
        public string CardSkill(int index) => index < cards.Length && cards[index].skillText != null ? cards[index].skillText.text : "";
        public string CardStatus(int index) => index < cards.Length && cards[index].statusText != null ? cards[index].statusText.text : "";
        public SquadMember CardMember(int index) => index < cards.Length ? cards[index].member : null;
        public Sprite CardPortrait(int index) => index < cards.Length && cards[index].portrait != null ? cards[index].portrait.sprite : null;

        protected override float TargetAlpha(HudRoot hud) => hud.Squad == null ? 0f : hud.State == HudState.Exploration ? explorationAlpha : 1f;

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            if (hud.Squad == null) return;
            int slot = 0;
            for (int i = 0; i < hud.Squad.Members.Count && slot < cards.Length; i++)
            {
                var member = hud.Squad.Members[i];
                if (member == hud.ActiveMember) continue;
                var card = cards[slot++];
                if (card.member != member)
                {
                    card.member = member;
                    var profile = member.GetComponent<CharacterProfile>();
                    Color accent = profile != null ? profile.AccentColor : OffWhite;
                    card.nameText.text = $"<color=#{Hex(Muted)}>0{i + 1}</color>  {(profile != null ? profile.DisplayName.ToUpperInvariant() : member.name.ToUpperInvariant())}";
                    if (card.accent != null) card.accent.color = accent;
                    if (card.portrait != null)
                    {
                        card.portrait.sprite = profile != null ? profile.Portrait : null;
                        card.portrait.enabled = card.portrait.sprite != null;
                    }
                }
                if (!refresh) continue;
                var health = member.Health;
                float ratio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;
                card.hpFill.fillAmount = ratio;
                card.hpFill.color = health.IsDead ? Danger : ratio > 0.35f ? OffWhite : Danger;
                var skill = member.GetComponent<CharacterSkill>();
                bool detailed = hud.State != HudState.Exploration;
                card.skillText.text = skill == null || !detailed ? "" :
                    skill.IsReady ? $"{skill.SkillName.ToUpperInvariant()}  <color=#{Hex(Cyan)}>READY</color>" :
                    $"{skill.SkillName.ToUpperInvariant()}  <color=#{Hex(Muted)}>{skill.CooldownRemaining:0.0}s</color>";
                var combo = member.GetComponent<ComboOpportunity>();
                bool counter = combo != null && combo.Type == ComboOpportunityType.Protected;
                card.statusText.text = health.IsDead ? $"<color=#{Hex(Danger)}>DOWN</color>" :
                    counter ? $"<color=#{Hex(Amber)}>COMBO  CONTRE {combo.RemainingDuration:0.0}s</color>" :
                    $"<color=#{Hex(Muted)}>{health.CurrentHealth:0}</color>";
                if (card.comboTag != null) card.comboTag.enabled = counter;
            }
            for (; slot < cards.Length; slot++) cards[slot].member = null;
        }
    }
}
