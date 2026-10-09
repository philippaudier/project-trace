using System;
using TRACE.AI;
using TRACE.Encounter;
using TRACE.Input;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TRACE.Narrative
{
    // Linear sequencer of the First Trace slice: zones, one clue, one strange event, tension, encounter, conclusion.
    // Zones are distance checks on the controlled member; no quest or save system.
    [DefaultExecutionOrder(70)]
    [DisallowMultipleComponent]
    public sealed class StoryDirector : MonoBehaviour
    {
        public enum Phase { Arrival, Clue, Hall, Tension, Combat, Conclusion, End }

        [Header("Systems")]
        [SerializeField] private SquadController squad;
        [SerializeField] private EncounterController encounter;
        [SerializeField] private DialogueRunner dialogue;
        [SerializeField] private AmbienceController ambience;
        [Header("Level")]
        [SerializeField] private Interactable clueTerminal;
        [SerializeField] private Interactable badge;
        [SerializeField] private GateDoor clueDoor;
        [SerializeField] private GateDoor eventDoor;
        [SerializeField] private GateDoor[] arenaGates = Array.Empty<GateDoor>();
        [SerializeField] private Light eventLight;
        [SerializeField] private GameObject eventScreen;
        [SerializeField] private Light[] tensionLights = Array.Empty<Light>();
        [SerializeField] private Light conclusionLight;
        [SerializeField] private Renderer conclusionScreen;
        [SerializeField] private Material conclusionScreenOn;
        [SerializeField] private Transform hallZone;
        [SerializeField] private Transform tensionZone;
        [SerializeField] private Transform arenaZone;
        [SerializeField, Min(1f)] private float zoneRadius = 5f;
        [Header("Timing")]
        [SerializeField, Min(0f)] private float arrivalDelay = 4f;
        [SerializeField, Min(0f)] private float conclusionDelay = 1.5f;
        [SerializeField, Min(0f)] private float eventScreenDuration = 3f;
        [SerializeField, Min(0f)] private float tensionLightIntensity = 4f;
        [SerializeField, Min(0f)] private float arenaGateRadius = 7f;
        [Header("Lines")]
        [SerializeField] private DialogueLine[] arrivalLines = Array.Empty<DialogueLine>();
        [SerializeField] private DialogueLine[] clueLines = Array.Empty<DialogueLine>();
        [SerializeField] private DialogueLine[] badgeLines = Array.Empty<DialogueLine>();
        [SerializeField] private DialogueLine[] eventLines = Array.Empty<DialogueLine>();
        [SerializeField] private DialogueLine[] tensionLines = Array.Empty<DialogueLine>();
        [SerializeField] private DialogueLine[] conclusionLines = Array.Empty<DialogueLine>();
        private float phaseEnteredAt;
        private bool arrivalPlayed;
        private bool conclusionPlayed;
        private float eventScreenUntil;
        private int lastGateWave = -1;
        private bool endPanelHidden;
        private bool menuEntered;
        private GUIStyle title;
        private GUIStyle body;
        public Phase Current { get; private set; } = Phase.Arrival;
        public float PhaseElapsed => Time.time - phaseEnteredAt;
        public bool EventTriggered { get; private set; }
        public bool EncounterPrepared { get; private set; }
        public bool ConclusionShown => conclusionPlayed;

        private void Awake()
        {
            phaseEnteredAt = Time.time;
            if (eventLight != null) eventLight.enabled = false;
            if (eventScreen != null) eventScreen.SetActive(false);
            if (conclusionLight != null) conclusionLight.enabled = false;
        }

        private void OnEnable()
        {
            if (clueTerminal != null) clueTerminal.Interacted += OnClue;
            if (badge != null) badge.Interacted += OnBadge;
        }

        private void OnDisable()
        {
            if (clueTerminal != null) clueTerminal.Interacted -= OnClue;
            if (badge != null) badge.Interacted -= OnBadge;
        }

        private void Update()
        {
            if (eventScreen != null && eventScreen.activeSelf && Time.time >= eventScreenUntil) eventScreen.SetActive(false);
            switch (Current)
            {
                case Phase.Arrival:
                    if (!arrivalPlayed && PhaseElapsed >= arrivalDelay) { arrivalPlayed = true; dialogue.Play(arrivalLines); }
                    break;
                case Phase.Clue:
                    if (InZone(hallZone)) TriggerEvent();
                    break;
                case Phase.Hall:
                    if (InZone(tensionZone)) EnterTension();
                    break;
                case Phase.Tension:
                    if (InZone(arenaZone)) PrepareEncounter();
                    break;
                case Phase.Combat:
                    StageGates();
                    if (encounter.State == EncounterController.EncounterState.Complete) Enter(Phase.Conclusion);
                    break;
                case Phase.Conclusion:
                    if (!conclusionPlayed && PhaseElapsed >= conclusionDelay) ShowConclusion();
                    else if (conclusionPlayed && !dialogue.IsPlaying) Enter(Phase.End);
                    break;
                case Phase.End:
                    if (!menuEntered && !endPanelHidden)
                    {
                        menuEntered = true;
                        var input = squad != null ? squad.GetComponent<TracePlayerInput>() : null;
                        if (input != null) input.EnterMenu();
                    }
                    break;
            }
        }

        private bool InZone(Transform zone)
        {
            var member = squad != null ? squad.ActiveMember : null;
            if (zone == null || member == null) return false;
            Vector3 delta = member.transform.position - zone.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= zoneRadius * zoneRadius;
        }

        private void Enter(Phase next)
        {
            Current = next;
            phaseEnteredAt = Time.time;
        }

        private void OnClue()
        {
            if (Current != Phase.Arrival) return;
            dialogue.Play(clueLines);
            if (clueDoor != null) clueDoor.Open();
            Enter(Phase.Clue);
        }

        private void OnBadge() => dialogue.Play(badgeLines);

        // Something opens the way on its own: curiosity and a little unease, no jump scare.
        private void TriggerEvent()
        {
            EventTriggered = true;
            if (eventDoor != null) eventDoor.Open();
            if (eventLight != null) eventLight.enabled = true;
            if (eventScreen != null) { eventScreen.SetActive(true); eventScreenUntil = Time.time + eventScreenDuration; }
            if (ambience != null) { ambience.PlayCreak(); ambience.SetAnomaly(true); }
            dialogue.Play(eventLines);
            Enter(Phase.Hall);
        }

        private void EnterTension()
        {
            foreach (var light in tensionLights) if (light != null) { light.enabled = true; light.intensity = tensionLightIntensity; }
            if (ambience != null) { ambience.SetTension(true); ambience.SetAnomaly(false); }
            dialogue.Play(tensionLines);
            Enter(Phase.Tension);
        }

        // Implicit checkpoint: full squad, skills ready, then the staged encounter begins.
        private void PrepareEncounter()
        {
            EncounterPrepared = true;
            foreach (var member in squad.Members)
            {
                if (member == null || member.Health.IsDead) continue;
                member.Health.Heal(member.Health.MaxHealth);
                var skill = member.GetComponent<CharacterSkill>();
                if (skill != null) skill.ResetCooldown();
            }
            if (ambience != null) ambience.SetCombat(true);
            encounter.Begin();
            Enter(Phase.Combat);
        }

        // Enemies come through the gates nearest to their wave positions instead of popping into view.
        private void StageGates()
        {
            if (encounter.State != EncounterController.EncounterState.Spawning || encounter.WaveIndex == lastGateWave) return;
            lastGateWave = encounter.WaveIndex;
            var wave = encounter.CurrentWave;
            if (wave == null) return;
            foreach (var gate in arenaGates)
            {
                if (gate == null) continue;
                foreach (var enemy in wave.enemies)
                    if (enemy != null && Vector3.Distance(enemy.transform.position, gate.transform.position) <= arenaGateRadius) { gate.Open(); break; }
            }
        }

        private void ShowConclusion()
        {
            conclusionPlayed = true;
            if (ambience != null) { ambience.SetCombat(false); ambience.SetConclusion(true); ambience.SetAnomaly(true); }
            if (conclusionLight != null) conclusionLight.enabled = true;
            if (conclusionScreen != null && conclusionScreenOn != null) conclusionScreen.sharedMaterial = conclusionScreenOn;
            dialogue.Play(conclusionLines);
        }

        public void RestartSlice() => encounter.Restart();
        public void ReturnToPrototype() => SceneManager.LoadScene("PrototypeEncounter");

        private void OnGUI()
        {
            if (Current != Phase.End || endPanelHidden) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                body = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            }
            float w = Screen.width, h = Screen.height;
            GUI.Box(new Rect(w * 0.5f - 240f, h * 0.3f, 480f, 190f), GUIContent.none);
            GUI.Label(new Rect(0f, h * 0.3f + 14f, w, 50f), "END OF FIRST TRACE", title);
            GUI.Label(new Rect(0f, h * 0.3f + 66f, w, 30f), $"Rencontre : {encounter.Duration:0} s   -   Switches {encounter.SwitchCount}   Focus {encounter.FocusCount}   Combos {encounter.ComboCount}", body);
            if (GUI.Button(new Rect(w * 0.5f - 220f, h * 0.3f + 120f, 140f, 36f), "Recommencer")) RestartSlice();
            if (GUI.Button(new Rect(w * 0.5f - 70f, h * 0.3f + 120f, 140f, 36f), "Scene Prototype")) ReturnToPrototype();
            if (GUI.Button(new Rect(w * 0.5f + 80f, h * 0.3f + 120f, 140f, 36f), "Rester ici"))
            {
                endPanelHidden = true;
                var input = squad != null ? squad.GetComponent<TracePlayerInput>() : null;
                if (input != null) input.ExitMenu();
            }
        }
    }
}
