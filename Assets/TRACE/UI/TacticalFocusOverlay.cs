using TRACE.Combat;
using TRACE.Skills;
using TRACE.Tactical;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Analytical layer for Tactical Focus: corner brackets and an ANALYSIS row of short tags. It follows the reveal
    // timing of TacticalFocusPresentationController (which also owns the grade); TacticalOverlay keeps the enemy cards.
    public sealed class TacticalFocusOverlay : HudPanel
    {
        [SerializeField] private TacticalFocusPresentationController presentation;
        [SerializeField] private RectTransform[] brackets = new RectTransform[0];
        [SerializeField, Min(0f)] private float bracketBreath = 4f;
        [SerializeField] private Text analysisText;
        private Vector2[] bracketRest;
        public float GradeWeight => presentation != null ? presentation.GradeWeight : 0f;
        public string Analysis => analysisText != null ? analysisText.text : "";

        protected override Vector2 SlideDirection => Vector2.zero;
        protected override float TargetAlpha(HudRoot hud) => hud.State == HudState.Focus &&
            (presentation == null || presentation.Revealed(TacticalFocusPresentationController.Stage.Status)) ? 1f : 0f;

        protected override void OnBind(HudRoot hud)
        {
            bracketRest = new Vector2[brackets.Length];
            for (int i = 0; i < brackets.Length; i++) bracketRest[i] = brackets[i] != null ? brackets[i].anchoredPosition : Vector2.zero;
        }

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            bool focused = hud.State == HudState.Focus;
            // Brackets breathe outward very slightly: alive, not flashy.
            float breath = Mathf.Sin(Time.unscaledTime * 1.6f) * bracketBreath;
            for (int i = 0; i < brackets.Length; i++)
            {
                if (brackets[i] == null) continue;
                Vector2 outward = new Vector2(Mathf.Sign(brackets[i].anchorMin.x - 0.5f), Mathf.Sign(brackets[i].anchorMin.y - 0.5f));
                brackets[i].anchoredPosition = bracketRest[i] + outward * breath;
            }
            if (!refresh || !focused || analysisText == null) return;
            int hostiles = hud.CountHostiles(out int preparing, out int combos);
            bool counter = false;
            if (hud.Squad != null)
                foreach (var member in hud.Squad.Members)
                {
                    var combo = member.GetComponent<ComboOpportunity>();
                    if (combo != null && combo.Type == ComboOpportunityType.Protected) counter = true;
                }
            // One row of short tags, read at a glance: lit when actionable, dimmed otherwise.
            string tag(bool on, Color color, string text) => $"<color=#{Hex(on ? color : Muted)}>{text}</color>";
            bool combosShown = presentation == null || presentation.Revealed(TacticalFocusPresentationController.Stage.Combos);
            analysisText.text = $"<color=#{Hex(Muted)}>ANALYSIS</color>\n<b>" +
                tag(hostiles > 0, OffWhite, $"HOSTILES {hostiles}") + "    " +
                tag(preparing > 0, Danger, preparing > 0 ? $"WIND-UP {preparing}" : "WIND-UP") + "    " +
                tag(combosShown && (combos > 0 || counter), Amber, !combosShown ? "COMBO" : combos > 0 ? "GROUPED 1+E" : counter ? "PROTECTED" : "COMBO") + "    " +
                tag(hud.IsLocked, Cyan, hud.IsLocked ? "LOCKED" : "LOCK") + "</b>";
        }
    }
}
