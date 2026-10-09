using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Narrative;
using TRACE.Tactical;
using TRACE.UI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class TacticalFocusPresentationTests
    {
        private SquadController squad;
        private TacticalFocus focus;
        private TacticalFocusPresentationController presentation;
        private TacticalReadability readability;
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
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f; Time.fixedDeltaTime = oldFixed; Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator MixerRoutesTheWorldBehindAMembraneAndKeepsTheTacticalLayerClear()
        {
            var mixer = presentation.Mixer;
            Assert.That(mixer, Is.Not.Null);
            Assert.That(mixer.updateMode, Is.EqualTo(AudioMixerUpdateMode.UnscaledTime), "Focus slows time, not the mix");
            foreach (string name in new[] { "Music", "Ambience", "World", "Combat", "UI", "Tactical" })
                Assert.That(mixer.FindMatchingGroups("Master/" + name).Any(g => g.name == name), Is.True, name);
            Assert.That(mixer.FindSnapshot("Normal"), Is.Not.Null);
            Assert.That(mixer.FindSnapshot("TacticalFocus"), Is.Not.Null);
            Assert.That(presentation.CurrentSnapshot, Is.EqualTo(mixer.FindSnapshot("Normal")));
            var hudAudio = Object.FindFirstObjectByType<HudAudio>();
            Assert.That(hudAudio.GetComponent<AudioSource>().outputAudioMixerGroup.name, Is.EqualTo("UI"));
            foreach (var source in presentation.GetComponentsInChildren<AudioSource>())
            {
                Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo("Tactical"), source.name);
                Assert.That(source.spatialBlend, Is.EqualTo(0f), source.name);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EnteringSwitchesTheSnapshotGradesBrieflyAccentsAndStartsTheBed()
        {
            float peak = 0f;
            Keys(Key.Tab);
            float until = Time.unscaledTime + 0.35f;
            while (Time.unscaledTime < until) { yield return null; peak = Mathf.Max(peak, presentation.AberrationIntensity); }
            Assert.That(presentation.IsPresenting, Is.True);
            Assert.That(presentation.EnterCount, Is.EqualTo(1));
            Assert.That(presentation.CurrentSnapshot, Is.EqualTo(presentation.Mixer.FindSnapshot("TacticalFocus")));
            Assert.That(presentation.GradeWeight, Is.EqualTo(1f).Within(0.01f), "grade in within ~0.15 s");
            Assert.That(peak, Is.InRange(0.08f, 0.15f), "a short aberration spike on entering");
            yield return Wait(0.3f);
            Assert.That(presentation.AberrationIntensity, Is.InRange(0.005f, 0.03f), "near nothing while active");
            Assert.That(presentation.BedLevel, Is.GreaterThan(0f).And.LessThan(0.2f), "bed present but faint");
            Assert.That(presentation.EnterCount, Is.EqualTo(1), "not replayed while held");
        }

        [UnityTest] public IEnumerator LeavingRestoresTheWorldQuickly()
        {
            Keys(Key.Tab); yield return Wait(0.5f);
            Keys(); yield return Wait(0.45f);
            Assert.That(presentation.IsPresenting, Is.False);
            Assert.That(presentation.ExitCount, Is.EqualTo(1));
            Assert.That(presentation.CurrentSnapshot, Is.EqualTo(presentation.Mixer.FindSnapshot("Normal")));
            Assert.That(presentation.GradeWeight, Is.EqualTo(0f));
            Assert.That(presentation.AberrationIntensity, Is.EqualTo(0f).Within(0.001f));
            Assert.That(presentation.BedLevel, Is.EqualTo(0f));
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.01f), "gameplay time untouched by the presentation");
        }

        [UnityTest] public IEnumerator TheScanPulseTravelsOutwardOnceAndFades()
        {
            Keys(Key.Tab);
            yield return null; yield return null;
            Assert.That(presentation.ScanVisible, Is.True);
            float first = presentation.ScanRadiusNow;
            yield return Wait(0.15f);
            Assert.That(presentation.ScanRadiusNow, Is.GreaterThan(first));
            Assert.That(presentation.ScanRadiusNow, Is.LessThanOrEqualTo(presentation.ScanRadius));
            yield return Wait(0.5f);
            Assert.That(presentation.ScanVisible, Is.False, "0.3-0.6 s, then gone while Focus holds");
            Assert.That(presentation.IsPresenting, Is.True);
        }

        [UnityTest] public IEnumerator TheHudRevealsInAShortCascade()
        {
            var overlay = Object.FindFirstObjectByType<HudRoot>().GetComponentInChildren<TacticalFocusOverlay>(true);
            Keys(Key.Tab);
            yield return null; yield return null;
            Assert.That(presentation.IsPresenting, Is.True);
            if (presentation.SinceEnter < 0.08f)
            {
                Assert.That(presentation.Revealed(TacticalFocusPresentationController.Stage.Markers), Is.False);
                Assert.That(overlay.IsShown, Is.False, "the status layer waits ~180 ms");
            }
            yield return Wait(0.3f);
            foreach (TacticalFocusPresentationController.Stage stage in System.Enum.GetValues(typeof(TacticalFocusPresentationController.Stage)))
                Assert.That(presentation.Revealed(stage), Is.True, stage.ToString());
            Assert.That(overlay.IsShown, Is.True, "everything is up within ~250 ms");
        }

        [UnityTest] public IEnumerator EnemyMarkersTellStandardLockedAndComboApart()
        {
            var a = Place(enemies[0], 4f, -1.2f);
            var b = Place(enemies[1], 5.5f, 1.6f);
            yield return Wait(0.3f);
            Assert.That(readability.MarkerOf(a), Is.EqualTo(TacticalReadability.Marker.None), "nothing outside Focus");
            Keys(Key.Tab); yield return Wait(0.4f);
            Assert.That(readability.MarkerOf(a), Is.Not.EqualTo(TacticalReadability.Marker.None));
            Assert.That(squad.GetComponent<TargetingSystem>().LockTarget(b.GetComponent<Health>()), Is.True);
            yield return Wait(0.1f);
            Assert.That(readability.MarkerOf(b), Is.EqualTo(TacticalReadability.Marker.Locked));
            Assert.That(a.GetComponent<ComboOpportunity>().Offer(ComboOpportunityType.Grouped, 3f, squad), Is.True);
            yield return Wait(0.1f);
            if (!a.IsPreparingAttack) Assert.That(readability.MarkerOf(a), Is.EqualTo(TacticalReadability.Marker.Combo));
        }

        [UnityTest] public IEnumerator AWindUpShowsAThreatMarkerAndAnIntentionGhostOnlyInFocus()
        {
            var pursuer = enemies.OfType<BasicMeleeEnemy>().First();
            var leader = members[0].transform;
            pursuer.transform.position = leader.position + leader.forward * 1.3f + Vector3.up * 0.08f;
            pursuer.gameObject.SetActive(true);
            Physics.SyncTransforms();
            float deadline = Time.unscaledTime + 5f;
            while (!pursuer.IsPreparingAttack && Time.unscaledTime < deadline) yield return null;
            Assume.That(pursuer.IsPreparingAttack, "the pursuer winds up next to the leader");
            Assert.That(readability.GhostVisible(pursuer), Is.False, "no ghost outside Focus");
            Keys(Key.Tab); yield return Wait(0.2f);
            Assert.That(pursuer.IsPreparingAttack, Is.True, "Focus stretches the wind-up");
            Assert.That(readability.MarkerOf(pursuer), Is.EqualTo(TacticalReadability.Marker.Threat));
            Assert.That(readability.GhostVisible(pursuer), Is.True);
            Keys(); yield return Wait(0.3f);
            Assert.That(readability.GhostVisible(pursuer), Is.False);
        }

        [UnityTest] public IEnumerator FirstTraceKeepsAmbienceInItsGroupsAndFocusStableThroughASwitch()
        {
            yield return Load("FirstTrace");
            yield return Wait(0.3f);
            var ambience = Object.FindFirstObjectByType<AmbienceController>();
            if (ambience != null && ambience.AudioAvailable)
            {
                var groups = ambience.GetComponentsInChildren<AudioSource>().Select(s => s.outputAudioMixerGroup != null ? s.outputAudioMixerGroup.name : "none").ToArray();
                Assert.That(groups, Does.Contain("Ambience"));
                Assert.That(groups, Does.Contain("Music"));
                Assert.That(groups, Does.Not.Contain("none"));
            }
            Keys(Key.Tab); yield return Wait(0.4f);
            Assert.That(presentation.IsPresenting, Is.True);
            Keys(Key.Tab, Key.Digit2); yield return Wait(0.2f); Keys(Key.Tab); yield return Wait(0.3f);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[1]), "switching is allowed in Focus");
            Assert.That(presentation.IsPresenting, Is.True);
            Assert.That(presentation.EnterCount, Is.EqualTo(1), "a switch does not replay the entry");
            Keys(); yield return Wait(0.4f);
            Assert.That(presentation.IsPresenting, Is.False);
        }

        private EnemyBrain Place(EnemyBrain enemy, float ahead, float side)
        {
            var leader = members[0].transform;
            enemy.transform.position = leader.position + leader.forward * ahead + leader.right * side + Vector3.up * 0.08f;
            enemy.gameObject.SetActive(true);
            Physics.SyncTransforms();
            return enemy;
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            squad = Object.FindFirstObjectByType<SquadController>();
            focus = squad.GetComponent<TacticalFocus>();
            presentation = Object.FindFirstObjectByType<TacticalFocusPresentationController>();
            Assert.That(presentation, Is.Not.Null, scene + " has no Tactical Focus presentation");
            readability = presentation.GetComponent<TacticalReadability>();
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            members = squad.Members.ToArray();
            enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            if (scene == "Prototype") foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            foreach (var other in members) if (!other.IsPlayerControlled) other.Companion.enabled = false;
            yield return Wait(0.2f);
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
