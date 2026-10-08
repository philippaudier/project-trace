using TRACE.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Centre bottom: "[F]  INTERACT / prompt" while an interactable is in range. Mirrors InteractionController.Current.
    public sealed class InteractionPrompt : HudPanel
    {
        [SerializeField] private InteractionController interaction;
        [SerializeField] private Text keyText;
        [SerializeField] private Text promptText;
        public string Label => promptText != null ? promptText.text : "";

        protected override float TargetAlpha(HudRoot hud) => interaction != null && interaction.Current != null ? 1f : 0f;

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            if (!refresh || interaction == null || interaction.Current == null) return;
            keyText.text = "F";
            promptText.text = $"INTERACT\n<color=#{Hex(Muted)}>{interaction.Current.Prompt}</color>";
        }
    }
}
