using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Encounter;
using TRACE.Input;
using TRACE.Narrative;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class FirstTraceTests
    {
        private SquadController squad;
        private StoryDirector story;
        private EncounterController encounter;
        private DialogueRunner dialogue;
        private InteractionController interaction;
        private AmbienceController ambience;
        private SquadMember[] members;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("FirstTrace");
            Bind();
            yield return Frames(5);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private void Bind()
        {
            squad = Object.FindFirstObjectByType<SquadController>();
            story = Object.FindFirstObjectByType<StoryDirector>();
            encounter = Object.FindFirstObjectByType<EncounterController>();
            dialogue = Object.FindFirstObjectByType<DialogueRunner>();
            ambience = Object.FindFirstObjectByType<AmbienceController>();
            interaction = squad.GetComponent<InteractionController>();
            members = squad.Members.ToArray();
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
        }

        [UnityTest]
        public IEnumerator SliceIsTheFirstBuildSceneAndOpensCalmly()
        {
            Assert.That(SceneUtility.GetScenePathByBuildIndex(0), Does.Contain("FirstTrace"));
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Arrival));
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Pending));
            Assert.That(encounter.HasBegun, Is.False);
            Assert.That(Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length, Is.Zero);
            Assert.That(members.All(m => !m.Health.IsDead), Is.True);
            Assert.That(Object.FindObjectsByType<GateDoor>(FindObjectsSortMode.None).All(d => !d.IsOpen), Is.True);
            Assert.That(Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(NavMesh.SamplePosition(members[0].transform.position, out _, 1f, NavMesh.AllAreas), Is.True);
            Assert.That(dialogue.IsPlaying, Is.False);
            yield return Wait(4.5f);
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Pending), "no auto start");
            Assert.That(dialogue.IsPlaying, Is.True, "arrival exchange starts on its own");
            Assert.That(dialogue.CurrentLine.speaker, Is.EqualTo("CONTROL"));
        }

        [UnityTest]
        public IEnumerator TerminalInteractionPlaysTheArchiveAndOpensTheDoor()
        {
            var terminal = Terminal();
            var door = Door("Door Clue");
            PlaceActive(terminal.transform.position + Vector3.left * 1.3f, Quaternion.Euler(0f, 90f, 0f));
            yield return Frames(3);
            Assert.That(interaction.Current, Is.EqualTo(terminal));
            Assert.That(terminal.IsFocused, Is.True);
            yield return PressInteract();
            Assert.That(terminal.UseCount, Is.EqualTo(1));
            Assert.That(dialogue.IsPlaying, Is.True);
            StringAssert.Contains("ARCHIVE", dialogue.CurrentLine.text);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Clue));
            Assert.That(door.IsOpening || door.IsOpen, Is.True);
            yield return Until(() => door.IsOpen, 3f);
            Assert.That(interaction.Current, Is.Null, "no prompt while the exchange plays");
            int shown = dialogue.ShownCount;
            yield return PressInteract();
            Assert.That(dialogue.ShownCount, Is.EqualTo(shown + 1), "Interact advances the line");
            Assert.That(terminal.UseCount, Is.EqualTo(1), "one-shot terminal is not used again");
        }

        [UnityTest]
        public IEnumerator DialogueAdvancesOnInteractAndAutoAdvancesOnTime()
        {
            story.enabled = false;
            Flush();
            dialogue.Play(new[] { new DialogueLine("A", "un", 1f), new DialogueLine("B", "deux", 1f), new DialogueLine("C", "trois", 1f) });
            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(dialogue.CurrentLine.text, Is.EqualTo("un"));
            yield return PressInteract();
            Assert.That(dialogue.CurrentLine.text, Is.EqualTo("deux"));
            yield return RealTime(1.2f);
            Assert.That(dialogue.CurrentLine.text, Is.EqualTo("trois"));
            bool finished = false;
            dialogue.Finished += () => finished = true;
            yield return RealTime(1.2f);
            Assert.That(dialogue.IsPlaying, Is.False);
            Assert.That(finished, Is.True);
            Assert.That(dialogue.ShownCount, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator BadgeIsOptionalAndOnlyAddsALine()
        {
            var badge = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.Prompt.Contains("badge"));
            PlaceActive(badge.transform.position + Vector3.forward * 1.2f, Quaternion.identity);
            yield return Frames(3);
            Assert.That(interaction.Current, Is.EqualTo(badge));
            yield return PressInteract();
            Assert.That(dialogue.IsPlaying, Is.True);
            StringAssert.Contains("OKAFOR", dialogue.CurrentLine.text);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Arrival), "the badge does not advance the story");
        }

        [UnityTest]
        public IEnumerator HallEventOpensTheFarDoorByItself()
        {
            Terminal().Interact();
            yield return Frames(2);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Clue));
            Assert.That(dialogue.QueuedCount, Is.GreaterThan(0), "the archive exchange is queued line by line");
            Flush();
            var door = Door("Door Event");
            var light = Get<Light>(story, "eventLight");
            var screen = Get<GameObject>(story, "eventScreen");
            Assert.That(door.IsOpen, Is.False);
            Assert.That(light.enabled, Is.False);
            PlaceActive(Get<Transform>(story, "hallZone").position, Quaternion.identity);
            yield return Frames(3);
            Assert.That(story.EventTriggered, Is.True);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Hall));
            Assert.That(door.IsOpening || door.IsOpen, Is.True);
            Assert.That(light.enabled, Is.True);
            Assert.That(screen.activeSelf, Is.True);
            Assert.That(ambience.CreakCount, Is.EqualTo(1));
            StringAssert.Contains("porte", dialogue.CurrentLine.text);
            yield return Wait(3.5f);
            Assert.That(screen.activeSelf, Is.False, "the screen only wakes up briefly");
        }

        [UnityTest]
        public IEnumerator TensionThenArenaPrepareTheSquadAndStageTheEncounter()
        {
            yield return ReachCombat();
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Combat));
            Assert.That(story.EncounterPrepared, Is.True);
            Assert.That(ambience.TensionActive, Is.True);
            Assert.That(Get<Light[]>(story, "tensionLights").All(l => l.enabled && l.intensity >= 3f), Is.True);
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(members[0].Health.MaxHealth), "checkpoint heals");
            Assert.That(members[0].GetComponent<CharacterSkill>().IsReady, Is.True, "checkpoint resets cooldowns");
            Assert.That(encounter.HasBegun, Is.True);
            Assert.That(ambience.CombatActive, Is.True);
            yield return Until(() => encounter.State == EncounterController.EncounterState.Spawning, 4f);
            var gateA = Door("Gate A");
            yield return Frames(2);
            Assert.That(gateA.IsOpening || gateA.IsOpen, Is.True, "wave one comes through gate A");
            Assert.That(Door("Gate B").IsOpening || Door("Gate B").IsOpen, Is.False);
            yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting, 4f);
            Assert.That(encounter.Waves[0].enemies.All(e => e.gameObject.activeSelf && e.GetComponent<NavMeshAgent>().isOnNavMesh), Is.True);
            Assert.That(Object.FindObjectsByType<GateDoor>(FindObjectsSortMode.None).Count(d => d.OpenCount > 0), Is.EqualTo(3), "clue door, event door, gate A");
        }

        [UnityTest]
        public IEnumerator ClearingTheEncounterPlaysTheConclusionAndEndsTheSlice()
        {
            yield return ReachCombat();
            for (int w = 0; w < encounter.WaveCount; w++)
            {
                yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting && encounter.WaveIndex == w, 10f);
                Flush();
                foreach (var enemy in encounter.Waves[w].enemies) enemy.GetComponent<Health>().TakeDamage(9999f);
                yield return Frames(2);
            }
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Complete));
            Assert.That(Door("Gate B").OpenCount, Is.EqualTo(1));
            Assert.That(Door("Gate C").OpenCount, Is.EqualTo(1));
            yield return Frames(2);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Conclusion));
            var light = Get<Light>(story, "conclusionLight");
            var screen = Get<Renderer>(story, "conclusionScreen");
            Assert.That(light.enabled, Is.False, "a short calm before the terminal wakes up");
            yield return Until(() => story.ConclusionShown, 3f);
            Assert.That(light.enabled, Is.True);
            Assert.That(screen.sharedMaterial.name, Does.Contain("ScreenOn"));
            Assert.That(ambience.CombatActive, Is.False);
            StringAssert.Contains("IDENTITY MATCH", dialogue.CurrentLine.text);
            yield return PressInteract();
            StringAssert.Contains("DECEASED", dialogue.CurrentLine.text);
            yield return PressInteract();
            StringAssert.Contains("12 MINUTES", dialogue.CurrentLine.text);
            while (dialogue.IsPlaying) yield return PressInteract();
            yield return Frames(2);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.End));
            Assert.That(squad.GetComponent<TracePlayerInput>().InMenu, Is.True, "end panel frees the cursor for its buttons");
        }

        [UnityTest]
        public IEnumerator RestartReloadsTheSliceFromTheArrival()
        {
            Terminal().Interact();
            yield return Frames(2);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Clue));
            var previous = story;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Backspace));
            yield return Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => Object.FindFirstObjectByType<StoryDirector>() != previous, 3f);
            yield return Frames(3);
            Bind();
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Arrival));
            Assert.That(Terminal().UseCount, Is.Zero);
            Assert.That(Object.FindObjectsByType<GateDoor>(FindObjectsSortMode.None).All(d => !d.IsOpen), Is.True);
            Assert.That(encounter.HasBegun, Is.False);
        }

        // ---------------------------------------------------------------- helpers

        private IEnumerator ReachCombat()
        {
            Terminal().Interact();
            yield return Frames(2);
            Flush();
            PlaceActive(Get<Transform>(story, "hallZone").position, Quaternion.identity);
            yield return Frames(3);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Hall));
            Flush();
            PlaceActive(Get<Transform>(story, "tensionZone").position, Quaternion.identity);
            yield return Frames(3);
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Tension));
            Flush();
            members[0].Health.TakeDamage(40f);
            Assert.That(members[0].GetComponent<CharacterSkill>().Activate(), Is.True);
            yield return Wait(0.3f);
            PlaceActive(Get<Transform>(story, "arenaZone").position, Quaternion.identity);
            yield return Frames(3);
        }

        // Beats queue behind the current exchange; tests fast-forward it instead of waiting real seconds per line.
        private void Flush() { int guard = 0; while (dialogue.IsPlaying && guard++ < 64) dialogue.Skip(); }
        private static Interactable Terminal() => Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.Prompt.Contains("terminal"));
        private static GateDoor Door(string name) => Object.FindObjectsByType<GateDoor>(FindObjectsSortMode.None).First(d => d.name == name);

        private IEnumerator PressInteract()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames(2);
        }

        private void PlaceActive(Vector3 position, Quaternion rotation)
        {
            var member = squad.ActiveMember;
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false; body.enabled = false;
            member.transform.SetPositionAndRotation(position + Vector3.up * 0.08f, rotation);
            body.enabled = true; motor.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out after " + timeoutSeconds + " s of game time.");
        }
        private static IEnumerator RealTime(float seconds) { yield return new WaitForSecondsRealtime(seconds); yield return null; }
        private static IEnumerator Wait(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return null; }
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
