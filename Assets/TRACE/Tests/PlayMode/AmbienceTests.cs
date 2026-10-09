using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Input;
using TRACE.Narrative;
using TRACE.Tactical;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class AmbienceTests
    {
        private SquadController squad;
        private AmbienceController ambience;
        private Keyboard keyboard;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime; Time.captureDeltaTime = 0f; Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load("FirstTrace");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var focus = squad != null ? squad.GetComponent<TacticalFocus>() : null;
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f; Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator FirstTraceLayersFadeInQuietlyOnAuthoredClips()
        {
            Assert.That(ambience.UsesAuthoredClips, Is.True);
            Assume.That(ambience.AudioAvailable, "audio device available");
            Assert.That(ambience.BaseLevel, Is.LessThan(0.3f), "fades in, never starts at full level");
            yield return Wait(2f);
            Assert.That(ambience.BaseLevel, Is.EqualTo(0.3f).Within(0.01f));
            Assert.That(ambience.EnvironmentLevel, Is.EqualTo(0.22f).Within(0.01f));
            Assert.That(ambience.AnomalyLevel, Is.EqualTo(0f), "no anomaly outside Trace moments");
            var layers = ambience.GetComponentsInChildren<AudioSource>();
            var baseSource = layers.First(s => s.name == "Base");
            var environment = layers.First(s => s.name == "Environment");
            Assert.That(baseSource.clip.name, Is.EqualTo("AMB_FirstTrace_Base_LOOP"));
            Assert.That(environment.clip.name, Is.EqualTo("AMB_FirstTrace_Environment_LOOP"));
            Assert.That(baseSource.clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming), "long beds stream");
            Assert.That(baseSource.clip.channels, Is.EqualTo(2));
            Assert.That(baseSource.loop && environment.loop, Is.True);
            Assert.That(baseSource.outputAudioMixerGroup.name, Is.EqualTo("Ambience"));
        }

        [UnityTest] public IEnumerator PlacedEmittersAreFewSpatialLoopsInTheAmbienceGroup()
        {
            yield return Wait(2f);
            var emitters = Object.FindObjectsByType<AmbienceEmitter>(FindObjectsSortMode.None);
            Assert.That(emitters.Length, Is.InRange(4, 8));
            foreach (var emitter in emitters)
            {
                var source = emitter.GetComponent<AudioSource>();
                Assert.That(source.spatialBlend, Is.EqualTo(1f), emitter.name);
                Assert.That(source.loop, Is.True, emitter.name);
                Assert.That(source.maxDistance, Is.LessThanOrEqualTo(15f), emitter.name);
                Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo("Ambience"), emitter.name);
                if (ambience.AudioAvailable) Assert.That(emitter.Volume, Is.EqualTo(emitter.Target).Within(0.01f), emitter.name);
            }
            Assert.That(emitters.Select(e => e.GetComponent<AudioSource>().clip.name).Distinct().Count(), Is.LessThanOrEqualTo(3), "a few clips reused");
        }

        [UnityTest] public IEnumerator DetailsAreRareSpatialAndComeFromPlausibleSpots()
        {
            Assume.That(ambience.AudioAvailable);
            var interval = Get<Vector2>(ambience, "detailInterval");
            Assert.That(interval.x, Is.GreaterThanOrEqualTo(8f));
            Assert.That(interval.y, Is.LessThanOrEqualTo(30f));
            Assert.That(Get<float>(ambience, "silenceChance"), Is.GreaterThan(0f), "some slots stay silent");
            var points = Get<Transform[]>(ambience, "detailPoints");
            Assert.That(points.Length, Is.GreaterThanOrEqualTo(4));
            int before = ambience.DetailCount;
            ambience.PlayDetailNow();
            yield return null;
            Assert.That(ambience.DetailCount, Is.EqualTo(before + 1));
            Assert.That(points.Min(p => Vector3.Distance(p.position, ambience.LastDetailPosition)), Is.LessThan(2f));
            var detailSource = ambience.GetComponentsInChildren<AudioSource>().First(s => s.name.StartsWith("Detail"));
            Assert.That(detailSource.spatialBlend, Is.EqualTo(1f));
        }

        [UnityTest] public IEnumerator TheTraceLayerFadesInOnTheHallEventAndOutInTheTensionCorridor()
        {
            Assume.That(ambience.AudioAvailable);
            var story = Object.FindFirstObjectByType<StoryDirector>();
            story.GetType().GetMethod("TriggerEvent", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(story, null);
            Assert.That(ambience.AnomalyActive, Is.True);
            yield return Wait(0.5f);
            Assert.That(ambience.AnomalyLevel, Is.GreaterThan(0f).And.LessThan(0.3f), "1-3 s fade, not a cut");
            yield return Wait(2.5f);
            Assert.That(ambience.AnomalyLevel, Is.EqualTo(0.3f).Within(0.01f));
            story.GetType().GetMethod("EnterTension", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(story, null);
            Assert.That(ambience.AnomalyActive, Is.False);
            Assert.That(ambience.TensionActive, Is.True);
            yield return Wait(3f);
            Assert.That(ambience.AnomalyLevel, Is.EqualTo(0f));
            Assert.That(ambience.EnvironmentLevel, Is.GreaterThan(0.22f), "tension lifts the environment a little");
        }

        [UnityTest] public IEnumerator TheConclusionLeavesRoomForSilence()
        {
            Assume.That(ambience.AudioAvailable);
            yield return Wait(2f);
            ambience.SetConclusion(true);
            yield return Wait(2f);
            Assert.That(ambience.BaseLevel, Is.EqualTo(0.3f * 0.45f).Within(0.01f));
            Assert.That(ambience.EnvironmentLevel, Is.EqualTo(0.22f * 0.45f).Within(0.01f));
        }

        [UnityTest] public IEnumerator TacticalFocusFloatsAStereoBedAboveTheFilteredWorld()
        {
            var presentation = Object.FindFirstObjectByType<TacticalFocusPresentationController>();
            var bed = presentation.GetComponentsInChildren<AudioSource>().First(s => s.name == "Bed Source");
            Assert.That(bed.clip.name, Is.EqualTo("AMB_TacticalFocus_FLOAT_LOOP"));
            Assert.That(bed.clip.channels, Is.EqualTo(2), "stereo width preserved");
            Assert.That(bed.spatialBlend, Is.EqualTo(0f), "2D global");
            Assert.That(bed.outputAudioMixerGroup.name, Is.EqualTo("Tactical"), "outside the world low-pass");
            Keys(Key.Tab); yield return null; yield return null;
            Assert.That(presentation.BedLevel, Is.EqualTo(0f), "the bed follows the enter cue");
            yield return Wait(0.6f);
            Assert.That(presentation.BedLevel, Is.GreaterThan(0f).And.LessThan(0.2f), "felt, not a new music");
            Assert.That(bed.pitch, Is.InRange(0.98f, 1.02f));
            Assert.That(ambience.BaseLevel, Is.GreaterThan(0f), "the place is still there under Focus");
            Keys(); yield return Wait(0.5f);
            Assert.That(presentation.BedLevel, Is.EqualTo(0f));
        }

        [UnityTest] public IEnumerator PrototypeHasOnlyAMinimalAmbience()
        {
            yield return Load("Prototype");
            Assert.That(ambience, Is.Not.Null);
            Assert.That(ambience.UsesAuthoredClips, Is.True);
            Assert.That(Get<float>(ambience, "environmentVolume"), Is.EqualTo(0f));
            Assert.That(Get<AudioClip>(ambience, "anomalyLoop"), Is.Null);
            Assert.That(Get<AudioClip[]>(ambience, "detailClips"), Is.Empty);
            Assert.That(Object.FindObjectsByType<AmbienceEmitter>(FindObjectsSortMode.None).Length, Is.LessThanOrEqualTo(1));
            yield return Wait(2f);
            if (ambience.AudioAvailable)
            {
                Assert.That(ambience.BaseLevel, Is.GreaterThan(0f));
                Assert.That(ambience.EnvironmentLevel, Is.EqualTo(0f));
                Assert.That(ambience.DetailCount, Is.EqualTo(0));
            }
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            squad = Object.FindFirstObjectByType<SquadController>();
            ambience = Object.FindFirstObjectByType<AmbienceController>();
            Assert.That(ambience, Is.Not.Null, scene + " has no ambience");
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            yield return null;
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
