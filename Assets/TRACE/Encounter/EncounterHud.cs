using TRACE.Input;
using TRACE.Tactical;
using UnityEngine;

namespace TRACE.Encounter
{
    // Prototype IMGUI: wave banner, remaining enemies, end-of-encounter result with debug statistics.
    public sealed class EncounterHud : MonoBehaviour
    {
        [SerializeField] private EncounterController encounter;
        [SerializeField] private TacticalFocus focus;
        [SerializeField, Tooltip("Show the ENCOUNTER COMPLETE panel; off when a story director owns the ending.")]
        private bool showCompletion = true;
        private TracePlayerInput input;
        private bool menuEntered;
        private GUIStyle title;
        private GUIStyle body;
        private GUIStyle small;

        private void Awake() => input = focus != null ? focus.GetComponent<TracePlayerInput>() : FindFirstObjectByType<TracePlayerInput>();

        private void Update()
        {
            bool result = encounter != null && (encounter.State == EncounterController.EncounterState.Defeated ||
                (encounter.State == EncounterController.EncounterState.Complete && showCompletion));
            if (result && !menuEntered && input != null) { menuEntered = true; input.EnterMenu(); }
        }

        private void OnGUI()
        {
            if (encounter == null) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                body = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleRight };
            }
            float w = Screen.width, h = Screen.height;
            var wave = encounter.CurrentWave;
            string waveLabel = wave != null ? $"WAVE {encounter.WaveIndex + 1} / {encounter.WaveCount}  -  {wave.name}" : "";
            switch (encounter.State)
            {
                case EncounterController.EncounterState.Pending:
                    if (encounter.HasBegun) GUI.Label(new Rect(0, h * 0.18f, w, 40), $"RENCONTRE DANS {encounter.StateRemaining:0.0} s", body);
                    break;
                case EncounterController.EncounterState.Spawning:
                    GUI.Label(new Rect(0, h * 0.14f, w, 60), waveLabel, title);
                    GUI.Label(new Rect(0, h * 0.14f + 60, w, 30), "Les ennemis arrivent sur les marqueurs", body);
                    break;
                case EncounterController.EncounterState.Fighting:
                    if (focus != null && focus.IsActive) break;
                    GUI.Box(new Rect(w - 292, 12, 280, 56), GUIContent.none);
                    GUI.Label(new Rect(w - 284, 16, 264, 24), waveLabel, small);
                    GUI.Label(new Rect(w - 284, 40, 264, 24), $"ENNEMIS RESTANTS : {encounter.EnemiesAlive}", small);
                    break;
                case EncounterController.EncounterState.Cleared:
                    GUI.Label(new Rect(0, h * 0.14f, w, 60), "VAGUE ELIMINEE", title);
                    GUI.Label(new Rect(0, h * 0.14f + 60, w, 30),
                        $"Prochaine vague dans {encounter.StateRemaining:0.0} s   -   +{encounter.InterWaveHeal:0} HP", body);
                    break;
                case EncounterController.EncounterState.Complete:
                case EncounterController.EncounterState.Defeated:
                    bool won = encounter.State == EncounterController.EncounterState.Complete;
                    if (won && !showCompletion) break;
                    GUI.Box(new Rect(w * 0.5f - 260, h * 0.22f, 520, 300), GUIContent.none);
                    GUI.Label(new Rect(0, h * 0.22f + 10, w, 60), won ? "ENCOUNTER COMPLETE" : "SQUAD DEFEATED", title);
                    GUI.Label(new Rect(0, h * 0.22f + 80, w, 170), Statistics(), body);
                    if (GUI.Button(new Rect(w * 0.5f - 110, h * 0.22f + 250, 220, 36), "Recommencer  (Retour arriere)")) encounter.Restart();
                    break;
            }
        }

        private string Statistics() =>
            $"Duree : {encounter.Duration:0.0} s\n" +
            $"Switches : {encounter.SwitchCount}    Tactical Focus : {encounter.FocusCount}\n" +
            $"Combos : {encounter.ComboCount}    Degats subis : {encounter.DamageTaken:0}\n" +
            $"Membres tombes : {encounter.MembersFallen}";
    }
}
