using System;
using System.Collections.Generic;
using TRACE.AI;
using TRACE.Combat;
using TRACE.Encounter;
using TRACE.Narrative;
using TRACE.Skills;
using UnityEngine;

namespace TRACE.UI
{
    public enum HudSound
    {
        Confirm, Interact, ObjectiveUpdate, CharacterSwitch, SkillReady, ComboReady,
        TargetLock, TargetUnlock, TargetChange, ThreatWarning
    }

    // Functional UI sound: one 2D source next to the HUD. It watches the same state HudRoot already derives and plays
    // a one-shot on each transition (switch, cooldown to ready, combo appearing, lock / unlock / change, interaction,
    // new objective, new off-screen threat, dialogue advanced); Tactical Focus has its own presentation controller.
    // Nothing plays on the first frame, nothing repeats while a state holds, and repeated cues are rate-limited.
    // No gameplay logic, no singleton.
    [DefaultExecutionOrder(45)]
    [DisallowMultipleComponent]
    public sealed class HudAudio : MonoBehaviour
    {
        [Serializable]
        public sealed class Cue
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.5f;
            [Range(0f, 0.1f), Tooltip("Random pitch offset, e.g. 0.03 = ±3 %.")] public float pitchJitter;
            [Min(0f), Tooltip("Seconds before the same cue may play again.")] public float minInterval = 0.05f;
        }

        [SerializeField] private HudRoot hud;
        [SerializeField] private MissionPanel mission;
        [SerializeField] private InteractionController interaction;
        [SerializeField] private ThreatIndicator threat;
        [SerializeField] private DialogueRunner dialogue;
        [SerializeField] private AudioSource source;
        [SerializeField, Range(0f, 1f), Tooltip("UI bus volume, independent from ambience.")] private float uiVolume = 0.8f;
        [SerializeField, Min(0f), Tooltip("An enemy warns again only after leaving the threat hints this long.")] private float threatMemory = 6f;
        [SerializeField] private Cue[] cues = new Cue[Enum.GetValues(typeof(HudSound)).Length];

        private readonly Dictionary<CharacterSkill, bool> skillReady = new Dictionary<CharacterSkill, bool>();
        private readonly Dictionary<ComboOpportunity, ComboOpportunityType> combos = new Dictionary<ComboOpportunity, ComboOpportunityType>();
        private readonly Dictionary<EnemyBrain, float> threatSeen = new Dictionary<EnemyBrain, float>();
        private readonly List<ComboOpportunity> comboSources = new List<ComboOpportunity>();
        private float[] lastPlayed;
        private int[] counts;
        private bool initialised;
        private object lastActive;
        private bool wasLocked;
        private Health lastTarget;
        private int lastInteractions, lastAdvances;
        private string lastObjective;

        public float UiVolume { get => uiVolume; set => uiVolume = Mathf.Clamp01(value); }
        public int PlayCount { get; private set; }
        public HudSound? LastSound { get; private set; }
        public int Count(HudSound sound) => counts != null ? counts[(int)sound] : 0;
        public Cue CueFor(HudSound sound) => (int)sound < cues.Length ? cues[(int)sound] : null;

        private void Awake()
        {
            int length = Enum.GetValues(typeof(HudSound)).Length;
            lastPlayed = new float[length];
            for (int i = 0; i < length; i++) lastPlayed[i] = float.NegativeInfinity;
            counts = new int[length];
            if (source != null) { source.spatialBlend = 0f; source.playOnAwake = false; }
        }

        private void LateUpdate()
        {
            if (hud == null) return;
            if (!initialised) { Snapshot(); initialised = true; return; }
            float now = Time.unscaledTime;

            var active = hud.ActiveMember;
            if (active != null && !ReferenceEquals(active, lastActive)) Play(HudSound.CharacterSwitch);
            lastActive = active;

            // Targeting: lock, unlock, change. A lock lost because the target died stays silent (the kill speaks).
            var targeting = hud.Targeting;
            bool locked = targeting != null && targeting.IsLocked;
            var target = locked ? targeting.LockedTarget : null;
            if (locked && !wasLocked) Play(HudSound.TargetLock);
            else if (!locked && wasLocked && (lastTarget == null || !lastTarget.IsDead)) Play(HudSound.TargetUnlock);
            else if (locked && target != lastTarget) Play(HudSound.TargetChange);
            wasLocked = locked;
            lastTarget = target;

            // Skill ready: once, on the cooldown-to-ready edge, for any member.
            if (hud.Squad != null)
                foreach (var member in hud.Squad.Members)
                {
                    var skill = member.GetComponent<CharacterSkill>();
                    if (skill == null) continue;
                    bool ready = skill.IsReady;
                    if (skillReady.TryGetValue(skill, out bool was) && ready && !was && !member.Health.IsDead) Play(HudSound.SkillReady);
                    skillReady[skill] = ready;
                }

            // Combo: once when an opportunity appears on an enemy or a member.
            CollectCombos();
            foreach (var combo in comboSources)
            {
                var type = combo.Type;
                if (combos.TryGetValue(combo, out var was) && was == ComboOpportunityType.None && type != ComboOpportunityType.None) Play(HudSound.ComboReady);
                combos[combo] = type;
            }

            if (interaction != null && interaction.InteractionCount != lastInteractions)
            {
                lastInteractions = interaction.InteractionCount;
                Play(HudSound.Interact);
            }
            if (dialogue != null && dialogue.ManualAdvanceCount != lastAdvances)
            {
                lastAdvances = dialogue.ManualAdvanceCount;
                Play(HudSound.Confirm);
            }
            if (mission != null && mission.Objective != lastObjective)
            {
                bool fresh = !string.IsNullOrEmpty(mission.Objective) && !string.IsNullOrEmpty(lastObjective);
                lastObjective = mission.Objective;
                if (fresh) Play(HudSound.ObjectiveUpdate);
            }

            // Threats: a newly relevant off-screen threat warns once; the cue's interval stops spam across enemies.
            if (threat != null)
            {
                bool fresh = false;
                foreach (var hint in threat.Hints)
                {
                    if (hint.Enemy == null) continue;
                    if (!threatSeen.TryGetValue(hint.Enemy, out float seen) || now - seen > threatMemory) fresh = true;
                    threatSeen[hint.Enemy] = now;
                }
                if (fresh) Play(HudSound.ThreatWarning);
            }
        }

        public bool Play(HudSound sound)
        {
            int index = (int)sound;
            var cue = index < cues.Length ? cues[index] : null;
            float now = Time.unscaledTime;
            if (cue == null || now - lastPlayed[index] < cue.minInterval) return false;
            lastPlayed[index] = now;
            counts[index]++;
            PlayCount++;
            LastSound = sound;
            if (source == null || cue.clip == null || !source.isActiveAndEnabled) return true;
            source.pitch = 1f + UnityEngine.Random.Range(-cue.pitchJitter, cue.pitchJitter);
            source.PlayOneShot(cue.clip, cue.volume * uiVolume);
            return true;
        }

        // Records the current state without sounding it: loading a scene is silent.
        private void Snapshot()
        {
            lastActive = hud.ActiveMember;
            wasLocked = hud.Targeting != null && hud.Targeting.IsLocked;
            lastTarget = wasLocked ? hud.Targeting.LockedTarget : null;
            if (hud.Squad != null)
                foreach (var member in hud.Squad.Members)
                {
                    var skill = member.GetComponent<CharacterSkill>();
                    if (skill != null) skillReady[skill] = skill.IsReady;
                }
            CollectCombos();
            foreach (var combo in comboSources) combos[combo] = combo.Type;
            lastInteractions = interaction != null ? interaction.InteractionCount : 0;
            lastAdvances = dialogue != null ? dialogue.ManualAdvanceCount : 0;
            lastObjective = mission != null ? mission.Objective : null;
            if (threat != null)
                foreach (var hint in threat.Hints)
                    if (hint.Enemy != null) threatSeen[hint.Enemy] = Time.unscaledTime;
        }

        private void CollectCombos()
        {
            comboSources.Clear();
            if (hud.Squad != null)
                foreach (var member in hud.Squad.Members)
                {
                    var combo = member.GetComponent<ComboOpportunity>();
                    if (combo != null) comboSources.Add(combo);
                }
            foreach (var enemy in hud.Enemies)
            {
                if (enemy == null) continue;
                var combo = enemy.GetComponent<ComboOpportunity>();
                if (combo != null) comboSources.Add(combo);
            }
        }
    }
}
