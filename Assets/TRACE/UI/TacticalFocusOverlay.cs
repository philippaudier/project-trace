using TRACE.Combat;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Analytical layer for Tactical Focus: corner brackets, an ANALYSIS checklist, and a global post-process
    // volume (desaturation, slight cyan lift) whose weight follows the focus state in real time. The existing
    // TacticalOverlay keeps its enemy cards; this adds the frame, the readout and the grade around them.
    public sealed class TacticalFocusOverlay : HudPanel
    {
        [SerializeField] private Volume gradeVolume;
        [SerializeField, Range(0f, 1f)] private float gradeWeight = 0.85f;
        [SerializeField, Min(0.1f)] private float gradeSpeed = 6f;
        [SerializeField] private RectTransform[] brackets = new RectTransform[0];
        [SerializeField, Min(0f)] private float bracketBreath = 4f;
        [SerializeField] private Text analysisText;
        private Vector2[] bracketRest;
        public float GradeWeight => gradeVolume != null ? gradeVolume.weight : 0f;
        public string Analysis => analysisText != null ? analysisText.text : "";

        protected override Vector2 SlideDirection => Vector2.zero;
        protected override float TargetAlpha(HudRoot hud) => hud.State == HudState.Focus ? 1f : 0f;

        protected override void OnBind(HudRoot hud)
        {
            bracketRest = new Vector2[brackets.Length];
            for (int i = 0; i < brackets.Length; i++) bracketRest[i] = brackets[i] != null ? brackets[i].anchoredPosition : Vector2.zero;
            if (gradeVolume != null) gradeVolume.weight = 0f;
        }

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            bool focused = hud.State == HudState.Focus;
            if (gradeVolume != null)
                gradeVolume.weight = Mathf.MoveTowards(gradeVolume.weight, focused ? gradeWeight : 0f, Time.unscaledDeltaTime * gradeSpeed);
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
            string line(bool on, string text) => (on ? $"<color=#{Hex(Cyan)}>■</color>  " : $"<color=#{Hex(Muted)}>□</color>  ") + text;
            bool counter = false;
            if (hud.Squad != null)
                foreach (var member in hud.Squad.Members)
                {
                    var combo = member.GetComponent<ComboOpportunity>();
                    if (combo != null && combo.Type == ComboOpportunityType.Protected) counter = true;
                }
            analysisText.text = $"<b>ANALYSIS</b>\n" +
                line(hostiles > 0, $"{hostiles} HOSTILE{(hostiles > 1 ? "S" : "")} DETECTE{(hostiles > 1 ? "S" : "")}") + "\n" +
                line(preparing > 0, preparing > 0 ? $"{preparing} ATTAQUE{(preparing > 1 ? "S" : "")} EN PREPARATION" : "AUCUNE ATTAQUE EN PREPARATION") + "\n" +
                line(combos > 0 || counter, combos > 0 ? "COMBO : ENNEMIS GROUPES  1 + E" : counter ? "COMBO : CONTRE DISPONIBLE" : "AUCUNE OPPORTUNITE DE COMBO") + "\n" +
                line(hud.IsLocked, hud.IsLocked ? "CIBLE VERROUILLEE" : "PAS DE VERROUILLAGE");
        }
    }
}
