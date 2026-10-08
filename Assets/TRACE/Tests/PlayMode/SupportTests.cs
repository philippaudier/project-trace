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
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class SupportTests
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

        [UnityTest] public IEnumerator MemberThreeIsSupportInEveryPlayableScene()
        {
            AssertSupport(members[2]);
            yield return Load("PrototypeEncounter");
            AssertSupport(members[2]);
            yield return Load("FirstTrace");
            AssertSupport(members[2]);
            Assert.That(members[2].IsPlayerControlled, Is.False);
        }

        [UnityTest] public IEnumerator SupportDiffersFromControlAndTheTracewalkerInPaletteSilhouetteAndMovement()
        {
            var support = members[2].GetComponent<CharacterProfile>();
            var control = members[1].GetComponent<CharacterProfile>();
            var tracewalker = members[0].GetComponent<CharacterProfile>();
            Assert.That(support.IsTracewalker, Is.False);
            Assert.That(members[2].GetComponent<TraceModule>(), Is.Null);
            Assert.That(members[2].GetComponent<FieldControlModule>(), Is.Null);
            // Mint: green leads; Control's cyan has blue leading; the Tracewalker's amber has red leading.
            Assert.That(support.AccentColor.g, Is.GreaterThan(support.AccentColor.b).And.GreaterThan(support.AccentColor.r));
            Assert.That(control.AccentColor.b, Is.GreaterThan(control.AccentColor.g));
            Assert.That(tracewalker.AccentColor.r, Is.GreaterThan(tracewalker.AccentColor.g));
            var renderers = members[2].transform.Find("Support Visual").GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial.name).ToArray();
            Assert.That(renderers.Count(m => m == "M_Support_Mint" || m == "M_Support_Module"), Is.GreaterThan(renderers.Count(m => m == "M_Support_OriginAmber")));
            // Silhouette: hair of her own colour, an enveloping coat on both sides, a wider emitter ring than Control's.
            Assert.That(renderers, Does.Contain("M_Support_Hair"));
            Assert.That(members[2].transform.Find("Support Visual/Upper Body").GetComponentsInChildren<Transform>().Count(t => t.name == "Coat Side"), Is.EqualTo(2));
            float supportRing = members[2].transform.Find("Support Visual/Upper Body/Shoulder R/Stabilization Staff/Emitter Ring").GetChild(0).localPosition.magnitude;
            float controlRing = members[1].transform.Find("Control Visual/Upper Body/Shoulder R/Gravity Staff/Field Ring").GetChild(0).localPosition.magnitude;
            Assert.That(supportRing, Is.GreaterThan(controlRing));
            float supportStance = Mathf.Abs(members[2].transform.Find("Support Visual/Hip R").localPosition.x);
            float controlStance = Mathf.Abs(members[1].transform.Find("Control Visual/Hip R").localPosition.x);
            Assert.That(supportStance, Is.GreaterThan(controlStance));
            // Movement: calmer than Control, no Tactical Focus gesture.
            var puppet = members[2].GetComponent<CharacterPuppet>();
            var controlPuppet = members[1].GetComponent<CharacterPuppet>();
            Assert.That(puppet.LegSwingAmplitude, Is.LessThan(controlPuppet.LegSwingAmplitude));
            Assert.That(puppet.PoseResponse, Is.LessThan(controlPuppet.PoseResponse));
            Assert.That(puppet.FocusGesture, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator PulseShieldIsMintOnEveryMemberAndLightsTheSupportModule()
        {
            var module = members[2].GetComponent<SupportModule>();
            float idle = module.IdleIntensity;
            yield return Wait(0.3f);
            Assert.That(module.IsDeployed, Is.False);
            Assert.That(module.CurrentIntensity, Is.EqualTo(idle).Within(0.05f));
            Keys(Key.Digit3); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[2]));
            var skill = members[2].GetComponent<PulseShield>();
            Assert.That(skill.Activate(), Is.True);
            yield return Wait(0.7f);
            foreach (var member in members)
            {
                var shield = member.GetComponent<Shield>();
                Assert.That(shield.CurrentAmount, Is.EqualTo(30f).Within(0.01f), "capacity unchanged on " + member.name);
                var halo = Get<GameObject>(shield, "halo");
                Assert.That(halo.activeSelf, Is.True);
                var rings = halo.GetComponentsInChildren<LineRenderer>();
                Assert.That(rings.Length, Is.EqualTo(2));
                foreach (var ring in rings)
                {
                    Assert.That(ring.sharedMaterial.name, Is.EqualTo("M_Support_Shield"));
                    var color = ring.sharedMaterial.GetColor("_BaseColor");
                    Assert.That(color.g, Is.GreaterThan(color.b), "mint, not Control's cyan");
                }
                Assert.That(halo.GetComponent<FieldVisualPulse>(), Is.Not.Null);
            }
            Assert.That(module.IsDeployed, Is.True);
            Assert.That(module.CurrentIntensity, Is.GreaterThan(idle * 2f));
        }

        [UnityTest] public IEnumerator TacticalFocusGivesSupportNoSpecialReaction()
        {
            Keys(Key.Digit3); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            var module = members[2].GetComponent<SupportModule>();
            var puppet = members[2].GetComponent<CharacterPuppet>();
            float before = module.CurrentIntensity;
            Keys(Key.Tab); yield return Wait(0.9f);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(module.CurrentIntensity, Is.EqualTo(before).Within(0.05f));
            Assert.That(Mathf.Abs(puppet.LeftArmPitch), Is.LessThan(5f));
            Keys(); yield return Wait(0.3f);
        }

        [UnityTest] public IEnumerator HudShowsSupportWithHerPortraitMintAccentAndPulseShield()
        {
            var hud = Object.FindFirstObjectByType<HudRoot>();
            var squadPanel = hud.GetComponentInChildren<SquadStatusPanel>(true);
            var active = hud.GetComponentInChildren<ActiveOperatorPanel>(true);
            var profile = members[2].GetComponent<CharacterProfile>();
            yield return Wait(0.2f);
            int card = squadPanel.CardMember(0) == members[2] ? 0 : 1;
            StringAssert.Contains("SUPPORT", squadPanel.CardLabel(card));
            Assert.That(squadPanel.CardPortrait(card), Is.EqualTo(profile.Portrait));
            Assert.That(profile.Portrait.name, Is.EqualTo("Portrait_Support"));
            Keys(Key.Digit3); yield return Wait(0.15f); Keys(); yield return Wait(0.2f);
            StringAssert.Contains("SUPPORT", active.NameLabel);
            StringAssert.Contains("DEFENSIVE SUPPORT", active.RoleLabel);
            StringAssert.Contains("PULSE SHIELD", active.SkillLabel);
            StringAssert.Contains("READY", active.SkillLabel);
            Assert.That(active.PortraitSprite, Is.EqualTo(profile.Portrait));
            var overlay = squad.GetComponent<TacticalOverlay>();
            Keys(Key.Tab); yield return Wait(0.25f);
            StringAssert.Contains(profile.AccentHex, overlay.MemberInfo(2));
            Keys(); yield return Wait(0.25f);
        }

        private static void AssertSupport(SquadMember member)
        {
            Assert.That(member.name, Is.EqualTo("Member 3 - Support"));
            var profile = member.GetComponent<CharacterProfile>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.CharacterId, Is.EqualTo("origin_support_01"));
            Assert.That(profile.DisplayName, Is.EqualTo("Support"));
            Assert.That(profile.Designation, Is.EqualTo("Field Specialist — Support"));
            Assert.That(profile.Affiliation, Is.EqualTo("ORIGIN"));
            Assert.That(profile.Archetype, Is.EqualTo("Defensive Support"));
            Assert.That(profile.IsTracewalker, Is.False);
            Assert.That(profile.Portrait, Is.Not.Null);
            var module = member.GetComponent<SupportModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(Get<Renderer>(module, "emitter"), Is.Not.Null);
            Assert.That(module.ShieldCount, Is.EqualTo(3));
            Assert.That(member.GetComponent<CharacterPuppet>(), Is.Not.Null);
            var visual = member.transform.Find("Support Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(member.transform.Find("Body"), Is.Null);
            var names = visual.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (string part in new[] { "Torso", "Coat Back", "Hood", "Hair Long", "Ribbon L", "Origin Mark", "Stroke", "Support Module", "Module Core", "Stabilization Staff", "Emitter Ring", "Chest Lens" })
                Assert.That(names, Does.Contain(part));
            var materials = visual.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial.name).Distinct().ToArray();
            Assert.That(materials, Does.Contain("M_Support_Mint"));
            Assert.That(materials, Does.Contain("M_Support_Module"));
            Assert.That(materials, Does.Contain("M_Support_OriginAmber"));
            Assert.That(materials.All(m => m.StartsWith("M_Support_")), Is.True);
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
