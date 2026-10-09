using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Narrative;
using TRACE.Skills;
using TRACE.Tactical;
using TRACE.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class AudioTests
    {
        private SquadController squad;
        private HudAudio audio;
        private SquadMember[] members;
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

        [UnityTest] public IEnumerator EveryCueHasAShortMonoClipOnA2DSourceAndLoadingIsSilent()
        {
            Assert.That(audio.PlayCount, Is.EqualTo(0), "loading a scene plays nothing");
            var source = Get<AudioSource>(audio, "source");
            Assert.That(source.spatialBlend, Is.EqualTo(0f));
            Assert.That(source.transform.parent, Is.Null, "not attached to a 3D actor");
            foreach (HudSound sound in System.Enum.GetValues(typeof(HudSound)))
            {
                var cue = audio.CueFor(sound);
                Assert.That(cue.clip, Is.Not.Null, sound.ToString());
                Assert.That(cue.clip.channels, Is.EqualTo(1), sound.ToString());
                Assert.That(cue.clip.length, Is.LessThan(0.6f), sound.ToString());
                Assert.That(cue.pitchJitter, Is.LessThanOrEqualTo(0.04f), sound.ToString());
            }
            // Mix: danger above opportunities above targeting above secondary UI.
            Assert.That(audio.CueFor(HudSound.ThreatWarning).volume, Is.GreaterThan(audio.CueFor(HudSound.ComboReady).volume));
            Assert.That(audio.CueFor(HudSound.ComboReady).volume, Is.GreaterThanOrEqualTo(audio.CueFor(HudSound.TargetLock).volume));
            Assert.That(audio.CueFor(HudSound.TargetLock).volume, Is.GreaterThan(audio.CueFor(HudSound.Confirm).volume));
            Assert.That(audio.CueFor(HudSound.ComboReady).clip, Is.Not.EqualTo(audio.CueFor(HudSound.SkillReady).clip));
            yield return null;
        }

        [UnityTest] public IEnumerator CharacterSwitchSoundsOncePerSwitchEvenWhenRapid()
        {
            foreach (var key in new[] { Key.Digit2, Key.Digit3, Key.Digit1 })
            {
                Keys(key); yield return Wait(0.05f); Keys(); yield return Wait(0.25f);
            }
            Assert.That(audio.Count(HudSound.CharacterSwitch), Is.EqualTo(3));
            yield return Wait(0.3f);
            Assert.That(audio.Count(HudSound.CharacterSwitch), Is.EqualTo(3), "nothing while the member holds");
        }

        [UnityTest] public IEnumerator SkillReadySoundsOnlyOnTheCooldownToReadyEdge()
        {
            Keys(Key.Digit3); yield return Wait(0.1f); Keys(); yield return Wait(0.2f);
            var skill = members[2].GetComponent<PulseShield>();
            Assert.That(skill.Activate(), Is.True);
            yield return Wait(0.2f);
            Assert.That(audio.Count(HudSound.SkillReady), Is.EqualTo(0), "not while cooling down");
            skill.ResetCooldown();
            yield return Wait(0.2f);
            Assert.That(audio.Count(HudSound.SkillReady), Is.EqualTo(1));
            yield return Wait(0.5f);
            Assert.That(audio.Count(HudSound.SkillReady), Is.EqualTo(1), "not repeated while ready");
        }

        [UnityTest] public IEnumerator ComboReadySoundsOnceWhenAnOpportunityAppears()
        {
            var combo = enemies[0].GetComponent<ComboOpportunity>();
            enemies[0].gameObject.SetActive(true);
            yield return Wait(0.1f);
            int before = audio.Count(HudSound.ComboReady);
            Assert.That(combo.Offer(ComboOpportunityType.Grouped, 2f, squad), Is.True);
            yield return Wait(0.2f);
            Assert.That(audio.Count(HudSound.ComboReady), Is.EqualTo(before + 1));
            yield return Wait(0.5f);
            Assert.That(audio.Count(HudSound.ComboReady), Is.EqualTo(before + 1), "once per appearance");
            Assert.That(audio.Count(HudSound.SkillReady), Is.EqualTo(0), "distinct from skill ready");
        }

        [UnityTest] public IEnumerator TargetLockUnlockAndChangeEachHaveTheirCue()
        {
            var leader = members[0].transform;
            for (int i = 0; i < 2; i++)
            {
                Place(enemies[i].transform, leader.position + leader.forward * (5f + i * 1.5f) + leader.right * (i == 0 ? -1.2f : 1.6f));
                enemies[i].gameObject.SetActive(true);
            }
            yield return Wait(0.4f);
            yield return Middle();
            Assert.That(audio.Count(HudSound.TargetLock), Is.EqualTo(1));
            var first = squad.GetComponent<TargetingSystem>().LockedTarget;
            yield return Wheel(1f);
            yield return Wait(0.1f);
            if (squad.GetComponent<TargetingSystem>().LockedTarget != first)
                Assert.That(audio.Count(HudSound.TargetChange), Is.EqualTo(1));
            yield return Middle();
            Assert.That(audio.Count(HudSound.TargetUnlock), Is.EqualTo(1));
            Assert.That(audio.Count(HudSound.TargetLock), Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ThreatWarningIsRateLimited()
        {
            Assert.That(audio.Play(HudSound.ThreatWarning), Is.True);
            Assert.That(audio.Play(HudSound.ThreatWarning), Is.False, "anti-spam");
            yield return Wait(0.3f);
            Assert.That(audio.Play(HudSound.ThreatWarning), Is.False, "still within the interval");
            Assert.That(audio.CueFor(HudSound.ThreatWarning).minInterval, Is.GreaterThanOrEqualTo(2f));
        }

        // Silence pass: ambience off, the slice's key moments still each have their cue.
        [UnityTest] public IEnumerator FirstTraceInteractionObjectiveAndDialogueSoundWithoutAmbience()
        {
            yield return Load("FirstTrace");
            var ambience = Object.FindFirstObjectByType<AmbienceController>();
            if (ambience != null) ambience.gameObject.SetActive(false);
            var dialogue = Object.FindFirstObjectByType<DialogueRunner>();
            var interaction = squad.GetComponent<InteractionController>();
            float deadline = Time.unscaledTime + 16f;
            while ((!dialogue.IsPlaying || dialogue.QueuedCount > 0) && Time.unscaledTime < deadline) yield return null;
            while (dialogue.IsPlaying && Time.unscaledTime < deadline) yield return null;
            var terminal = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.Prompt.Contains("terminal"));
            var body = members[0].GetComponent<CharacterController>(); body.enabled = false;
            members[0].transform.SetPositionAndRotation(terminal.transform.position + Vector3.left * 1.3f + Vector3.up * 0.08f, Quaternion.Euler(0f, 90f, 0f));
            body.enabled = true; Physics.SyncTransforms();
            yield return Wait(0.3f);
            Assert.That(interaction.Current, Is.EqualTo(terminal));
            yield return PressF();
            Assert.That(audio.Count(HudSound.Interact), Is.EqualTo(1));
            yield return Wait(0.3f);
            Assert.That(audio.Count(HudSound.ObjectiveUpdate), Is.GreaterThanOrEqualTo(1), "the clue phase brings a new objective");
            Assert.That(dialogue.IsPlaying, Is.True);
            yield return PressF();
            Assert.That(audio.Count(HudSound.Confirm), Is.EqualTo(1), "advancing a line confirms");
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            squad = Object.FindFirstObjectByType<SquadController>();
            audio = Object.FindFirstObjectByType<HudAudio>();
            Assert.That(audio, Is.Not.Null, scene + " has no HUD Audio");
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            members = squad.Members.ToArray();
            enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            if (scene == "Prototype") foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            foreach (var other in members) if (!other.IsPlayerControlled) other.Companion.enabled = false;
            yield return Wait(0.3f);
        }

        private IEnumerator Middle()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle)); yield return Wait(0.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Wait(0.2f);
        }

        private IEnumerator Wheel(float direction)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, direction) }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; yield return null;
        }

        private IEnumerator PressF()
        {
            Keys(Key.F); yield return null; yield return null;
            Keys(); yield return null; yield return null;
        }

        private static void Place(Transform actor, Vector3 position)
        {
            actor.position = position + Vector3.up * 0.08f;
            Physics.SyncTransforms();
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
