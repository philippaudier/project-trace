using TRACE.Encounter;
using TRACE.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Top right: "// MAIN OBJECTIVE", the objective, one detail line, the sector. Static strings by default;
    // a story director or an encounter controller, when present, provide the objective and the detail line.
    public sealed class MissionPanel : HudPanel
    {
        [SerializeField] private Text sectorText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text detailText;
        [SerializeField] private string sector = "SECTEUR";
        [SerializeField] private string objective = "Objectif";
        [SerializeField] private string detail = "";
        [SerializeField] private EncounterController encounter;
        [SerializeField] private StoryDirector story;
        [SerializeField, Tooltip("One objective per StoryDirector phase (Arrival … End).")] private string[] storyObjectives = new string[0];
        [SerializeField, Tooltip("One sector label per StoryDirector phase.")] private string[] storySectors = new string[0];
        [SerializeField, Range(0f, 1f)] private float focusAlpha = 0.35f;
        public string Objective => objectiveText != null ? objectiveText.text : "";
        public string Detail => detailText != null ? detailText.text : "";
        public string Sector => sectorText != null ? sectorText.text : "";

        protected override Vector2 SlideDirection => Vector2.up;
        protected override float TargetAlpha(HudRoot hud) => hud.State == HudState.Focus ? focusAlpha : 1f;

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            if (!refresh) return;
            string currentSector = sector, currentObjective = objective, currentDetail = detail;
            if (story != null)
            {
                int phase = (int)story.Current;
                if (phase < storyObjectives.Length && !string.IsNullOrEmpty(storyObjectives[phase])) currentObjective = storyObjectives[phase];
                if (phase < storySectors.Length && !string.IsNullOrEmpty(storySectors[phase])) currentSector = storySectors[phase];
            }
            if (encounter != null)
            {
                switch (encounter.State)
                {
                    case EncounterController.EncounterState.Pending:
                        if (encounter.HasBegun) currentDetail = $"CONTACT DANS {encounter.StateRemaining:0} s";
                        break;
                    case EncounterController.EncounterState.Spawning:
                    case EncounterController.EncounterState.Fighting:
                        currentDetail = $"VAGUE {encounter.WaveIndex + 1} / {encounter.WaveCount}   ·   HOSTILES {encounter.EnemiesAlive}";
                        break;
                    case EncounterController.EncounterState.Cleared:
                        currentDetail = $"VAGUE {encounter.WaveIndex + 1} / {encounter.WaveCount} NEUTRALISEE   ·   {encounter.StateRemaining:0} s";
                        break;
                    case EncounterController.EncounterState.Complete:
                        currentDetail = "ZONE SECURISEE";
                        break;
                    case EncounterController.EncounterState.Defeated:
                        currentDetail = "ESCOUADE HORS DE COMBAT";
                        break;
                }
            }
            sectorText.text = currentSector.ToUpperInvariant();
            objectiveText.text = currentObjective;
            detailText.text = currentDetail;
            detailText.color = encounter != null && encounter.State == EncounterController.EncounterState.Defeated ? Danger : Cyan;
        }
    }
}
