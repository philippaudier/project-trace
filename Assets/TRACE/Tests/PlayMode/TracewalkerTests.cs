using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Input;
using TRACE.Tactical;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class TracewalkerTests
    {
        private SquadController squad;
        private TacticalFocus focus;
        private SquadMember[] members;
        private Keyboard keyboard;
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
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load("Prototype");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f; Time.fixedDeltaTime = oldFixed; Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator MemberOneIsTheTracewalkerWithProfileModuleMarkAndBlade()
        {
            AssertTracewalker(members[0]);
            for (int i = 1; i < members.Length; i++)
            {
                var other = members[i].GetComponent<CharacterProfile>();
                Assert.That(other == null || !other.IsTracewalker, Is.True, members[i].name + " is not a Tracewalker");
                Assert.That(members[i].GetComponent<TraceModule>(), Is.Null, members[i].name);
                Assert.That(members[i].transform.Find("Tracewalker Visual"), Is.Null, members[i].name);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator TraceModuleBrightensWhileFocusIsHeldAndSettlesAfterRelease()
        {
            var module = members[0].GetComponent<TraceModule>();
            float idle = module.IdleIntensity;
            yield return Wait(0.3f);
            Assert.That(module.IsFocused, Is.False);
            Assert.That(module.CurrentIntensity, Is.EqualTo(idle).Within(0.05f));
            Keys(Key.Tab); yield return Wait(0.5f);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(module.IsFocused, Is.True);
            Assert.That(module.CurrentIntensity, Is.GreaterThan(idle * 1.8f));
            Assert.That(Time.timeScale, Is.EqualTo(0.15f).Within(0.001f), "focus itself is unchanged");
            Keys(); yield return Wait(0.8f);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(module.CurrentIntensity, Is.EqualTo(idle).Within(0.1f));
        }

        [UnityTest] public IEnumerator HudLabelsNameTheTracewalkerWithTheAmberAccent()
        {
            var overlay = squad.GetComponent<TacticalOverlay>();
            var profile = members[0].GetComponent<CharacterProfile>();
            Keys(Key.Tab); yield return Wait(0.25f);
            Assert.That(overlay.IsVisible, Is.True);
            StringAssert.Contains("TRACEWALKER", overlay.MemberInfo(0));
            StringAssert.Contains(profile.AccentHex, overlay.MemberInfo(0));
            StringAssert.DoesNotContain("ASSAULT", overlay.MemberInfo(0));
            StringAssert.Contains("CONTROL", overlay.MemberInfo(1));
            StringAssert.Contains("SUPPORT", overlay.MemberInfo(2));
            Keys(); yield return Wait(0.25f);
        }

        [UnityTest] public IEnumerator FirstTraceAndEncounterScenesCarryTheSameTracewalker()
        {
            yield return Load("PrototypeEncounter");
            AssertTracewalker(members[0]);
            yield return Load("FirstTrace");
            AssertTracewalker(members[0]);
            Assert.That(members[0].IsPlayerControlled, Is.True);
        }

        private static void AssertTracewalker(SquadMember member)
        {
            Assert.That(member.name, Is.EqualTo("Member 1 - Tracewalker"));
            var profile = member.GetComponent<CharacterProfile>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.CharacterId, Is.EqualTo("tracewalker_player"));
            Assert.That(profile.DisplayName, Is.EqualTo("Tracewalker"));
            Assert.That(profile.Designation, Is.EqualTo("Tracewalker"));
            Assert.That(profile.Archetype, Is.EqualTo("Assault / Mobile Vanguard"));
            Assert.That(profile.AccentColor.r, Is.GreaterThan(profile.AccentColor.b), "amber accent");
            var module = member.GetComponent<TraceModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(Get<Renderer>(module, "emitter"), Is.Not.Null);
            Assert.That(Get<TacticalFocus>(module, "focus"), Is.Not.Null);
            var visual = member.transform.Find("Tracewalker Visual");
            Assert.That(visual, Is.Not.Null);
            var names = visual.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (string part in new[] { "Torso", "Origin Mark", "Stroke", "Trace Module", "Module Lens", "Blade", "Edge", "Coat Back" })
                Assert.That(names, Does.Contain(part));
            Assert.That(member.transform.Find("Capsule Visual"), Is.Null);
            var materials = visual.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial.name).Distinct().ToArray();
            Assert.That(materials, Does.Contain("M_Tracewalker_Amber"));
            Assert.That(materials, Does.Contain("M_Tracewalker_Module"));
            Assert.That(materials.All(m => m.StartsWith("M_Tracewalker_")), Is.True);
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            squad = Object.FindFirstObjectByType<SquadController>();
            focus = squad.GetComponent<TacticalFocus>();
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            members = squad.Members.ToArray();
            foreach (var enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
            yield return Wait(0.05f);
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
