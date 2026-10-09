using System.Collections.Generic;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Tactical;
using TRACE.UI;
using UnityEngine;

namespace TRACE.Voice
{
    // Squad-level voice triggers: switch in / out, combat start (once per engagement), one ally reaction when a
    // member falls, a discreet breath on Tactical Focus. It only asks a member's CharacterVoice to speak; the
    // member's policy decides. No gameplay logic.
    [DefaultExecutionOrder(49)]
    [DisallowMultipleComponent]
    public sealed class SquadVoiceDirector : MonoBehaviour
    {
        [SerializeField] private SquadController squad;
        [SerializeField] private HudRoot hud;
        [SerializeField] private TacticalFocus focus;
        [SerializeField, Min(0f), Tooltip("Rapid switches inside this window keep quiet after the first line.")] private float switchBurstWindow = 1.5f;
        private readonly Dictionary<SquadMember, CharacterVoice> voices = new Dictionary<SquadMember, CharacterVoice>();
        private readonly List<SquadMember> subscribed = new List<SquadMember>();
        private SquadMember lastActive;
        private bool initialised, combatArmed = true, wasFocused;
        private float lastSwitchAt = float.NegativeInfinity;
        private readonly Dictionary<ComboOpportunity, ComboOpportunityType> combos = new Dictionary<ComboOpportunity, ComboOpportunityType>();
        private readonly List<ComboOpportunity> comboSources = new List<ComboOpportunity>();
        public int SwitchLines { get; private set; }
        public int CombatStartLines { get; private set; }
        public int AllyDownLines { get; private set; }
        public int ComboLines { get; private set; }
        public bool CombatArmed => combatArmed;

        private void Start()
        {
            if (squad == null) return;
            foreach (var member in squad.Members)
            {
                voices[member] = member.GetComponent<CharacterVoice>();
                member.Health.OnDeath += () => MemberDown(member);
                subscribed.Add(member);
            }
            if (hud != null)
            {
                hud.StateChanged += StateChanged;
                foreach (var enemy in hud.Enemies)
                    if (enemy != null && enemy.GetComponent<ComboOpportunity>() is ComboOpportunity enemyCombo) comboSources.Add(enemyCombo);
            }
            foreach (var member in squad.Members)
                if (member.GetComponent<ComboOpportunity>() is ComboOpportunity memberCombo) comboSources.Add(memberCombo);
        }

        private void OnDestroy()
        {
            if (hud != null) hud.StateChanged -= StateChanged;
        }

        public CharacterVoice VoiceOf(SquadMember member) => member != null && voices.TryGetValue(member, out var voice) ? voice : null;

        private void LateUpdate()
        {
            if (squad == null) return;
            var active = squad.ActiveMember;
            if (!initialised) { initialised = true; lastActive = active; wasFocused = focus != null && focus.IsActive; return; }
            if (active != lastActive)
            {
                float now = Time.unscaledTime;
                if (now - lastSwitchAt >= switchBurstWindow)
                {
                    if (VoiceOf(lastActive) is CharacterVoice leaving) leaving.Speak(VoiceCategory.SwitchOut);
                    if (VoiceOf(active) is CharacterVoice arriving && arriving.Speak(VoiceCategory.SwitchIn)) SwitchLines++;
                }
                lastSwitchAt = now;
                lastActive = active;
            }
            // A combo window opening: enemies grouped -> the active member calls it; a protected member calls their own.
            foreach (var combo in comboSources)
            {
                if (combo == null) continue;
                var type = combo.Type;
                combos.TryGetValue(combo, out var previous);
                combos[combo] = type;
                if (type == previous || type == ComboOpportunityType.None) continue;
                var speaker = type == ComboOpportunityType.Protected ? combo.GetComponent<SquadMember>() : active;
                if (speaker == null) speaker = active;
                if (VoiceOf(speaker) is CharacterVoice caller && caller.Speak(VoiceCategory.ComboReady)) ComboLines++;
            }
            bool focused = focus != null && focus.IsActive;
            if (focused != wasFocused)
            {
                // Not every Focus: a breath at most, and the Focus's own sound keeps priority.
                if (VoiceOf(active) is CharacterVoice voice)
                    voice.Speak(focused ? VoiceCategory.TacticalFocusEnter : VoiceCategory.TacticalFocusExit);
                wasFocused = focused;
            }
        }

        private void StateChanged(HudState state)
        {
            if (state == HudState.Exploration) { combatArmed = true; return; }
            if (state != HudState.Combat || !combatArmed) return;
            combatArmed = false;
            if (VoiceOf(squad.ActiveMember) is CharacterVoice voice && voice.Speak(VoiceCategory.CombatStart)) CombatStartLines++;
        }

        // One reaction per fallen member: the active member if alive, otherwise the first other survivor.
        private void MemberDown(SquadMember fallen)
        {
            var speaker = squad.ActiveMember;
            if (speaker == null || speaker == fallen || speaker.Health.IsDead)
            {
                speaker = null;
                foreach (var member in squad.Members)
                    if (member != fallen && !member.Health.IsDead) { speaker = member; break; }
            }
            if (VoiceOf(speaker) is CharacterVoice voice && voice.Speak(VoiceCategory.AllyDown)) AllyDownLines++;
        }
    }
}
