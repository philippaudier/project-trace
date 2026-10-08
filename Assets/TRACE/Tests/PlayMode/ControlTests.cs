using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Input;
using TRACE.Skills;
using TRACE.Tactical;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class ControlTests
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

        [UnityTest] public IEnumerator MemberTwoIsControlWithProfileModuleStaffAndMark()
        {
            AssertControl(members[1]);
            // Still a plain capsule for Support, and no Control parts on the Tracewalker.
            Assert.That(members[2].transform.Find("Body"), Is.Not.Null);
            Assert.That(members[0].GetComponent<FieldControlModule>(), Is.Null);
            yield return null;
        }

        [UnityTest] public IEnumerator ControlIsNeverATracewalkerAndDiffersInSilhouetteAndMovement()
        {
            var control = members[1].GetComponent<CharacterProfile>();
            var tracewalker = members[0].GetComponent<CharacterProfile>();
            Assert.That(control.IsTracewalker, Is.False);
            Assert.That(tracewalker.IsTracewalker, Is.True);
            Assert.That(control.Affiliation, Is.EqualTo("ORIGIN"));
            Assert.That(members[1].GetComponent<TraceModule>(), Is.Null, "no Trace perception");
            // Palette: cyan dominant on Control, amber dominant on the Tracewalker.
            Assert.That(control.AccentColor.b, Is.GreaterThan(control.AccentColor.r));
            Assert.That(tracewalker.AccentColor.r, Is.GreaterThan(tracewalker.AccentColor.b));
            var controlMaterials = members[1].transform.Find("Control Visual").GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial.name).ToArray();
            Assert.That(controlMaterials.Count(m => m == "M_Control_Cyan" || m == "M_Control_FieldModule"), Is.GreaterThan(controlMaterials.Count(m => m == "M_Control_OriginAmber")));
            // Silhouette: narrower stance and a long weapon versus the short blade.
            float controlStance = Mathf.Abs(members[1].transform.Find("Control Visual/Hip R").localPosition.x);
            float tracewalkerStance = Mathf.Abs(members[0].transform.Find("Tracewalker Visual/Hip R").localPosition.x);
            Assert.That(controlStance, Is.LessThan(tracewalkerStance));
            float staffLength = members[1].transform.Find("Control Visual/Upper Body/Shoulder R/Gravity Staff/Shaft").localScale.y;
            float bladeLength = members[0].transform.Find("Tracewalker Visual/Upper Body/Shoulder R/Blade/Edge").localScale.z;
            Assert.That(staffLength, Is.GreaterThan(bladeLength * 2f));
            // Movement: calmer puppet, no Tactical Focus gesture.
            var controlPuppet = members[1].GetComponent<CharacterPuppet>();
            var tracewalkerPuppet = members[0].GetComponent<CharacterPuppet>();
            Assert.That(controlPuppet.LegSwingAmplitude, Is.LessThan(tracewalkerPuppet.LegSwingAmplitude));
            Assert.That(controlPuppet.PoseResponse, Is.LessThan(tracewalkerPuppet.PoseResponse));
            Assert.That(controlPuppet.FocusGesture, Is.False);
            Assert.That(tracewalkerPuppet.FocusGesture, Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator GravityFieldIsCyanPulsesAndLightsTheFieldControlModule()
        {
            var module = members[1].GetComponent<FieldControlModule>();
            float idle = module.IdleIntensity;
            yield return Wait(0.3f);
            Assert.That(module.IsDeployed, Is.False);
            Assert.That(module.CurrentIntensity, Is.EqualTo(idle).Within(0.05f));
            Keys(Key.Digit2); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[1]));
            var skill = members[1].GetComponent<GravityFieldSkill>();
            Assert.That(skill.Activate(), Is.True);
            yield return Wait(0.6f);
            Assert.That(skill.Field.RemainingDuration, Is.GreaterThan(0f));
            Assert.That(module.IsDeployed, Is.True);
            Assert.That(module.CurrentIntensity, Is.GreaterThan(idle * 2f));
            var visual = Get<Transform>(skill.Field, "visual");
            var pulse = visual.GetComponent<FieldVisualPulse>();
            Assert.That(pulse, Is.Not.Null);
            Assert.That(pulse.Elapsed, Is.GreaterThan(0.3f));
            var rings = visual.GetComponentsInChildren<LineRenderer>();
            Assert.That(rings.Length, Is.EqualTo(3));
            foreach (var ring in rings)
            {
                Assert.That(ring.sharedMaterial.name, Is.EqualTo("M_Control_Field"));
                var color = ring.sharedMaterial.GetColor("_BaseColor");
                Assert.That(color.b, Is.GreaterThan(color.r), "cold cyan, not purple");
            }
            Assert.That(rings.Any(r => Mathf.Abs(Mathf.DeltaAngle(0f, r.transform.localEulerAngles.y)) > 2f), Is.True, "rings rotate");
            // Mechanics untouched: same radius and duration as before.
            Assert.That(skill.Field.Radius, Is.EqualTo(3f).Within(0.01f));
        }

        [UnityTest] public IEnumerator TacticalFocusGivesControlNoSpecialReaction()
        {
            Keys(Key.Digit2); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            var module = members[1].GetComponent<FieldControlModule>();
            var puppet = members[1].GetComponent<CharacterPuppet>();
            float before = module.CurrentIntensity;
            Keys(Key.Tab); yield return Wait(0.9f);
            Assert.That(focus.IsActive, Is.True, "the squad focus still works while she is controlled");
            Assert.That(module.CurrentIntensity, Is.EqualTo(before).Within(0.05f));
            Assert.That(Mathf.Abs(puppet.LeftArmPitch), Is.LessThan(5f));
            Keys(); yield return Wait(0.3f);
        }

        [UnityTest] public IEnumerator HudNamesControlInHerAccentColour()
        {
            var overlay = squad.GetComponent<TacticalOverlay>();
            var profile = members[1].GetComponent<CharacterProfile>();
            Keys(Key.Tab); yield return Wait(0.25f);
            StringAssert.Contains("CONTROL", overlay.MemberInfo(1));
            StringAssert.Contains(profile.AccentHex, overlay.MemberInfo(1));
            StringAssert.Contains("TRACEWALKER", overlay.MemberInfo(0));
            Keys(); yield return Wait(0.25f);
        }

        [UnityTest] public IEnumerator EncounterAndFirstTraceScenesCarryTheSameControl()
        {
            yield return Load("PrototypeEncounter");
            AssertControl(members[1]);
            yield return Load("FirstTrace");
            AssertControl(members[1]);
            Assert.That(members[1].IsPlayerControlled, Is.False);
        }

        private static void AssertControl(SquadMember member)
        {
            Assert.That(member.name, Is.EqualTo("Member 2 - Control"));
            var profile = member.GetComponent<CharacterProfile>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.CharacterId, Is.EqualTo("origin_control_01"));
            Assert.That(profile.DisplayName, Is.EqualTo("Control"));
            Assert.That(profile.Designation, Is.EqualTo("Field Specialist — Control"));
            Assert.That(profile.Affiliation, Is.EqualTo("ORIGIN"));
            Assert.That(profile.Archetype, Is.EqualTo("Mid-Range Controller"));
            Assert.That(profile.IsTracewalker, Is.False);
            var module = member.GetComponent<FieldControlModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(Get<Renderer>(module, "emitter"), Is.Not.Null);
            Assert.That(Get<GravityFieldSkill>(module, "skill"), Is.Not.Null);
            Assert.That(member.GetComponent<CharacterPuppet>(), Is.Not.Null);
            var visual = member.transform.Find("Control Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(member.transform.Find("Body"), Is.Null);
            var names = visual.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (string part in new[] { "Torso", "Coat Long", "Coat Short", "Hair Long", "Origin Mark", "Stroke", "Field Control Module", "Module Core", "Gravity Staff", "Field Ring", "Chest Module" })
                Assert.That(names, Does.Contain(part));
            var materials = visual.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial.name).Distinct().ToArray();
            Assert.That(materials, Does.Contain("M_Control_Cyan"));
            Assert.That(materials, Does.Contain("M_Control_FieldModule"));
            Assert.That(materials, Does.Contain("M_Control_OriginAmber"));
            Assert.That(materials.All(m => m.StartsWith("M_Control_")), Is.True);
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
