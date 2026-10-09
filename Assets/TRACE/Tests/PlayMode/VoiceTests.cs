using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Skills;
using TRACE.Tactical;
using TRACE.UI;
using TRACE.Voice;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class VoiceTests
    {
        private SquadController squad;
        private SquadVoiceDirector director;
        private SquadMember[] members;
        private CharacterVoice[] voices;
        private EnemyBrain[] enemies;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldCapture, oldFixed;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime; oldFixed = Time.fixedDeltaTime;
            Time.captureDeltaTime = 0f; Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            yield return Load("Prototype");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var focus = squad != null ? squad.GetComponent<TacticalFocus>() : null;
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f; Time.fixedDeltaTime = oldFixed; Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator EveryMemberHasAVoiceSetWithTheMinimalCategoriesOnAVoiceSource()
        {
            string[] ids = { "tracewalker_player", "origin_control_01", "origin_support_01" };
            for (int i = 0; i < 3; i++)
            {
                var voice = voices[i];
                Assert.That(voice, Is.Not.Null, members[i].name);
                Assert.That(voice.VoiceSet, Is.Not.Null, members[i].name);
                Assert.That(voice.VoiceSet.CharacterId, Is.EqualTo(ids[i]));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.AttackLight), Is.GreaterThanOrEqualTo(3));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.Dodge), Is.GreaterThanOrEqualTo(2));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.HurtLight), Is.GreaterThanOrEqualTo(3));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.HurtHeavy), Is.GreaterThanOrEqualTo(2));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.SkillPrimary), Is.GreaterThanOrEqualTo(2));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.SwitchIn), Is.GreaterThanOrEqualTo(2));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.LowHealth), Is.GreaterThanOrEqualTo(1));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.AllyDown), Is.GreaterThanOrEqualTo(1));
                Assert.That(voice.VoiceSet.ClipCount(VoiceCategory.CombatStart), Is.GreaterThanOrEqualTo(1));
                Assert.That(voice.VoiceSet.PitchJitter, Is.LessThanOrEqualTo(0.01f), "human voice: ±1 % at most");
                var source = voice.Source;
                Assert.That(source.name, Is.EqualTo("VoiceAudioSource"));
                Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo("Voice"));
                Assert.That(source.spatialBlend, Is.InRange(0.3f, 0.8f), "light 3D");
                Assert.That(source.maxDistance, Is.LessThanOrEqualTo(30f));
                foreach (var line in voice.VoiceSet.Lines)
                    foreach (var clip in line.clips.Where(c => c != null))
                    {
                        Assert.That(clip.channels, Is.EqualTo(1), clip.name + " mono");
                        Assert.That(clip.loadType, Is.Not.EqualTo(AudioClipLoadType.Streaming), clip.name + " not streamed");
                        Assert.That(clip.length, Is.LessThan(2.5f), clip.name + " short");
                    }
            }
            Assert.That(director, Is.Not.Null);
            Assert.That(squad.GetComponent<VoiceDebugPanel>(), Is.Not.Null);
            Assert.That(squad.GetComponent<VoiceDebugPanel>().Visible, Is.False, "debug panel hidden by default");
            yield return null;
        }

        [UnityTest] public IEnumerator ImportedLinesAreMappedPerCharacterWithTheirVariants()
        {
            string[] folders = { "Tracewalker", "Control", "Support" };
            var spoken = new[] { VoiceCategory.SwitchIn, VoiceCategory.CombatStart, VoiceCategory.SkillPrimary, VoiceCategory.LowHealth,
                VoiceCategory.AllyDown, VoiceCategory.ComboReady, VoiceCategory.TacticalFocusEnter };
            for (int i = 0; i < 3; i++)
            {
                var set = voices[i].VoiceSet;
                foreach (var category in spoken)
                {
                    var line = set.Get(category);
                    Assert.That(line.ClipCount, Is.EqualTo(2), folders[i] + " " + category + " keeps both variants");
                    foreach (var clip in line.clips)
                    {
                        StringAssert.StartsWith("VO_" + folders[i] + "_", clip.name, "real recording, right character");
                        Assert.That(clip.name, Does.Not.Contain("TMP_"));
                    }
                }
                foreach (var line in set.Lines)
                    foreach (var clip in line.clips.Where(c => c != null))
                        Assert.That(clip.name.Contains(folders[i]) || clip.name.Contains("_" + set.FilePrefix + "_"), Is.True, clip.name + " belongs to " + folders[i]);
                // Grunts have no recording yet: placeholders stay in place.
                StringAssert.StartsWith("TMP_", set.Get(VoiceCategory.AttackLight).clips[0].name);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ComboWindowsCallOutOnceAndTheProtectedMemberSpeaksForThemselves()
        {
            foreach (var voice in voices) voice.ForceProbability = true;
            var enemy = enemies[0];
            enemy.gameObject.SetActive(true); yield return null;
            var grouped = enemy.GetComponent<ComboOpportunity>();
            Assert.That(grouped.Offer(ComboOpportunityType.Grouped, 3f, squad), Is.True);
            yield return Wait(0.1f);
            Assert.That(voices[0].Count(VoiceCategory.ComboReady), Is.EqualTo(1), "the active member calls the window");
            yield return Wait(0.2f);
            Assert.That(voices[0].Count(VoiceCategory.ComboReady), Is.EqualTo(1), "not repeated while the window holds");
            var protectedMember = members[2].GetComponent<ComboOpportunity>();
            Assert.That(protectedMember.Offer(ComboOpportunityType.Protected, 3f, squad), Is.True);
            yield return Wait(0.1f);
            Assert.That(voices[2].Count(VoiceCategory.ComboReady), Is.EqualTo(1), "the protected member speaks");
            Assert.That(director.ComboLines, Is.EqualTo(2));
            enemy.gameObject.SetActive(false);
        }

        [UnityTest] public IEnumerator AttacksSpeakWithoutRepeatingTheSameVariantAndRespectTheCooldown()
        {
            var voice = voices[0]; voice.ForceProbability = true;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return Wait(0.05f);
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Wait(0.05f);
            Assert.That(voice.Count(VoiceCategory.AttackLight), Is.EqualTo(1));
            var first = voice.LastClip;
            Assert.That(voice.IsSpeaking, Is.True);
            // Within the cooldown: silence; after it: another variant.
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return Wait(0.05f);
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Wait(0.05f);
            Assert.That(voice.Count(VoiceCategory.AttackLight), Is.EqualTo(1), "attack lines are rate limited");
            yield return Wait(0.6f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return Wait(0.05f);
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Wait(0.05f);
            Assert.That(voice.Count(VoiceCategory.AttackLight), Is.EqualTo(2));
            Assert.That(voice.LastClip, Is.Not.EqualTo(first), "no immediate repeat of the same clip");
        }

        [UnityTest] public IEnumerator HurtLightAndHeavyAreExclusiveRateLimitedAndHeavyInterruptsLowLines()
        {
            var voice = voices[0]; voice.ForceProbability = true;
            var receiver = members[0].GetComponent<DamageReceiver>();
            Assert.That(voice.Speak(VoiceCategory.AttackLight), Is.True);
            yield return null;
            Assert.That(receiver.TryTakeDamage(40f), Is.True);
            yield return null;
            Assert.That(voice.Count(VoiceCategory.HurtHeavy), Is.EqualTo(1), "a heavy hit speaks heavy");
            Assert.That(voice.Count(VoiceCategory.HurtLight), Is.EqualTo(0), "never both for one hit");
            Assert.That(voice.LastCategory, Is.EqualTo(VoiceCategory.HurtHeavy), "critical interrupts the attack grunt");
            Assert.That(voice.Speak(VoiceCategory.AttackLight), Is.False, "a low line cannot cut a critical one");
            yield return Wait(1.3f);
            Assert.That(receiver.TryTakeDamage(5f), Is.True); yield return null;
            Assert.That(receiver.TryTakeDamage(5f), Is.True); yield return null;
            Assert.That(voice.Count(VoiceCategory.HurtLight), Is.EqualTo(1), "two light hits in 0.6 s: one line");
        }

        [UnityTest] public IEnumerator LowHealthSpeaksOnceAtTheThresholdAndReArmsOnlyAfterRecovering()
        {
            var voice = voices[0]; voice.ForceProbability = true;
            Set(voice, "heavyHitFraction", 2f); // keep every hit light so only LowHealth is under test
            var health = members[0].Health; var receiver = members[0].GetComponent<DamageReceiver>();
            Assert.That(receiver.TryTakeDamage(60f), Is.True); yield return null;
            Assert.That(voice.Count(VoiceCategory.LowHealth), Is.EqualTo(0), "40 % is above the threshold");
            yield return Wait(0.7f);
            Assert.That(receiver.TryTakeDamage(15f), Is.True); yield return null;
            Assert.That(voice.Count(VoiceCategory.LowHealth), Is.EqualTo(1), "25 %: once");
            Assert.That(voice.LastCategory, Is.EqualTo(VoiceCategory.LowHealth), "high priority wins over the light hurt");
            yield return Wait(0.7f);
            Assert.That(receiver.TryTakeDamage(5f), Is.True); yield return null;
            Assert.That(voice.Count(VoiceCategory.LowHealth), Is.EqualTo(1), "not repeated while still low");
            health.Heal(40f); yield return null;
            Assert.That(voice.LowHealthArmed, Is.True, "re-armed above threshold + hysteresis");
            var line = voice.VoiceSet.Get(VoiceCategory.LowHealth); float cooldown = line.cooldown; line.cooldown = 0.1f;
            yield return Wait(0.7f);
            Assert.That(receiver.TryTakeDamage(45f), Is.True); yield return null;
            line.cooldown = cooldown;
            Assert.That(voice.Count(VoiceCategory.LowHealth), Is.EqualTo(2), "speaks again after dropping below once more");
        }

        [UnityTest] public IEnumerator SwitchSpeaksTheArrivingMemberButNotInABurst()
        {
            foreach (var voice in voices) voice.ForceProbability = true;
            Keys(Key.Digit2); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(voices[1].Count(VoiceCategory.SwitchIn), Is.EqualTo(1));
            Keys(Key.Digit3); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(voices[2].Count(VoiceCategory.SwitchIn), Is.EqualTo(0), "second switch inside the burst window stays quiet");
            yield return Wait(1.6f);
            Keys(Key.Digit1); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(voices[0].Count(VoiceCategory.SwitchIn), Is.EqualTo(1));
            Assert.That(director.SwitchLines, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator SkillCallsOutAndAnAllyDownGetsExactlyOneReaction()
        {
            foreach (var voice in voices) voice.ForceProbability = true;
            Assert.That(members[0].GetComponent<CharacterSkill>().Activate(), Is.True);
            yield return null;
            Assert.That(voices[0].Count(VoiceCategory.SkillPrimary), Is.EqualTo(1));
            members[1].Health.TakeDamage(1000f);
            yield return null;
            Assert.That(members[1].Health.IsDead, Is.True);
            int reactions = voices.Sum(v => v.Count(VoiceCategory.AllyDown));
            Assert.That(reactions, Is.EqualTo(1), "one reaction per fallen member");
            Assert.That(voices[0].Count(VoiceCategory.AllyDown), Is.EqualTo(1), "the active member reacts");
            Assert.That(voices[1].IsSpeaking, Is.False, "the fallen member is silent");
            Assert.That(director.AllyDownLines, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CombatStartSpeaksOncePerEngagementAndFocusStaysDiscreet()
        {
            foreach (var voice in voices) voice.ForceProbability = true;
            var hud = Object.FindFirstObjectByType<HudRoot>();
            var enemy = enemies[0];
            enemy.transform.position = members[0].transform.position + members[0].transform.forward * 4f;
            enemy.gameObject.SetActive(true); Physics.SyncTransforms();
            yield return Wait(0.6f);
            Assert.That(hud.State, Is.EqualTo(HudState.Combat));
            Assert.That(voices[0].Count(VoiceCategory.CombatStart), Is.EqualTo(1));
            Assert.That(director.CombatArmed, Is.False, "one line per engagement");
            var focus = squad.GetComponent<TacticalFocus>();
            Keys(Key.Tab); yield return Wait(0.3f);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(voices[0].Count(VoiceCategory.TacticalFocusEnter), Is.LessThanOrEqualTo(1));
            Assert.That(VoicePolicy.DefaultProbability(VoiceCategory.TacticalFocusEnter), Is.LessThanOrEqualTo(0.3f), "not every Focus");
            Keys(); yield return Wait(0.4f);
            enemy.gameObject.SetActive(false);
            yield return Wait(3.2f);
            Assert.That(hud.State, Is.EqualTo(HudState.Exploration));
            Assert.That(director.CombatArmed, Is.True, "re-armed after calm");
        }

        [UnityTest] public IEnumerator MissingClipsAreSilenceNeverErrors()
        {
            var voice = voices[2]; voice.ForceProbability = true;
            var runtimeSet = Object.Instantiate(voice.VoiceSet);
            foreach (var line in runtimeSet.Lines) line.clips = new AudioClip[0];
            Set(voice, "voiceSet", runtimeSet);
            foreach (VoiceCategory category in Enum.GetValues(typeof(VoiceCategory)))
                Assert.That(voice.Speak(category, ignoreCooldown: true), Is.False, category + " is silent when empty");
            Set(voice, "voiceSet", null);
            Assert.That(voice.Speak(VoiceCategory.HurtLight), Is.False, "no set, no error");
            members[2].GetComponent<DamageReceiver>().TryTakeDamage(50f);
            yield return null;
            Assert.That(voice.PlayCount, Is.EqualTo(0));
            Object.Destroy(runtimeSet);
        }

        [UnityTest] public IEnumerator MixerHasAVoiceGroupThatTheFocusBarelyFilters()
        {
            var presentation = Object.FindFirstObjectByType<TacticalFocusPresentationController>();
            var mixer = presentation.Mixer;
            var group = mixer.FindMatchingGroups("Master/Voice").FirstOrDefault(g => g.name == "Voice");
            Assert.That(group, Is.Not.Null);
            Assert.That(voices.All(v => v.Source.outputAudioMixerGroup == group), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator FirstTraceAndFieldTestCarryTheVoices()
        {
            foreach (string scene in new[] { "FirstTrace", "FieldTest" })
            {
                yield return Load(scene);
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(voices[i], Is.Not.Null, scene + " " + members[i].name);
                    Assert.That(voices[i].VoiceSet, Is.Not.Null, scene);
                    Assert.That(voices[i].Source.outputAudioMixerGroup.name, Is.EqualTo("Voice"), scene);
                }
                Assert.That(director, Is.Not.Null, scene);
            }
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            squad = Object.FindFirstObjectByType<SquadController>();
            director = squad.GetComponent<SquadVoiceDirector>();
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            members = squad.Members.ToArray();
            voices = members.Select(m => m.GetComponent<CharacterVoice>()).ToArray();
            enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            if (scene == "Prototype") foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            foreach (var other in members) if (!other.IsPlayerControlled) other.Companion.enabled = false;
            yield return Wait(0.3f);
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
