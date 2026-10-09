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
            // Silhouette: hair of her own colour, an enveloping coat on both sides, no long weapon: the gauntlet
            // on the left forearm is her signature, and her outline stays well below Control's staff.
            Assert.That(renderers, Does.Contain("M_Support_Hair"));
            Assert.That(renderers, Does.Contain("M_Support_Gauntlet"));
            Assert.That(members[2].transform.Find("Support Visual/Upper Body").GetComponentsInChildren<Transform>().Count(t => t.name == "Coat Side"), Is.EqualTo(2));
            var supportParts = members[2].transform.Find("Support Visual").GetComponentsInChildren<Transform>(true);
            Assert.That(supportParts.Any(t => t.name.Contains("Staff")), Is.False, "no staff");
            Assert.That(members[2].transform.Find("Support Visual/Upper Body/Shoulder L/Support Gauntlet"), Is.Not.Null);
            Assert.That(members[1].transform.Find("Control Visual/Upper Body/Shoulder R/Gravity Staff"), Is.Not.Null);
            Assert.That(Top(members[2], "Support Visual"), Is.LessThan(Top(members[1], "Control Visual") - 0.2f), "compact next to Control's vertical line");
            var moduleBounds = Bounds(members[2].transform.Find("Support Visual/Upper Body/Support Module"));
            Assert.That(moduleBounds.size.x, Is.LessThan(0.4f).And.GreaterThan(0.2f), "module readable, not invasive");
            Assert.That(moduleBounds.size.y, Is.LessThan(0.4f));
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

        [UnityTest] public IEnumerator PulseShieldIsProjectedFromTheGauntletAmplifiedByTheModuleAndMintOnEveryMember()
        {
            var module = members[2].GetComponent<SupportModule>();
            var gauntlet = members[2].GetComponent<SupportGauntlet>();
            var puppet = members[2].GetComponent<CharacterPuppet>();
            float idle = module.IdleIntensity;
            yield return Wait(0.3f);
            Assert.That(module.IsDeployed, Is.False);
            Assert.That(module.CurrentIntensity, Is.EqualTo(idle).Within(0.05f));
            Assert.That(gauntlet.IsProjecting, Is.False);
            Assert.That(gauntlet.CurrentIntensity, Is.EqualTo(gauntlet.IdleIntensity).Within(0.05f));
            Keys(Key.Digit3); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[2]));
            var skill = members[2].GetComponent<PulseShield>();
            Assert.That(skill.Activate(), Is.True);
            yield return Wait(0.2f);
            // The gesture and the equipment: arm extended, barrier projected past the hand, links to the others.
            Assert.That(puppet.IsSkillGesture, Is.True);
            Assert.That(puppet.LeftArmPitch, Is.LessThan(-50f), "gauntlet arm extended forward");
            Assert.That(gauntlet.IsProjecting, Is.True);
            Assert.That(gauntlet.IsLinking, Is.True);
            Assert.That(gauntlet.TetherCount, Is.EqualTo(3));
            Assert.That(gauntlet.CurrentIntensity, Is.GreaterThan(gauntlet.IdleIntensity * 2f));
            yield return Wait(0.6f);
            Assert.That(gauntlet.IsProjecting, Is.False, "the projection is a short burst");
            Assert.That(gauntlet.DeployAmount, Is.GreaterThan(0.5f), "plates stay open while the shield holds");
            foreach (var member in members)
            {
                var shield = member.GetComponent<Shield>();
                Assert.That(shield.CurrentAmount, Is.EqualTo(30f).Within(0.01f), "capacity unchanged on " + member.name);
                var halo = Get<GameObject>(shield, "halo");
                Assert.That(halo.activeSelf, Is.True);
                var lines = halo.GetComponentsInChildren<LineRenderer>();
                Assert.That(lines.Count(l => l.name.EndsWith("Halo")), Is.EqualTo(2));
                Assert.That(lines.Count(l => l.name == "Dome Arc"), Is.EqualTo(3));
                foreach (var line in lines)
                {
                    Assert.That(line.sharedMaterial.name, Is.EqualTo("M_Support_Shield"));
                    var color = line.sharedMaterial.GetColor("_BaseColor");
                    Assert.That(color.g, Is.GreaterThan(color.b), "mint, not Control's cyan");
                }
                Assert.That(halo.GetComponent<FieldVisualPulse>(), Is.Not.Null);
            }
            Assert.That(module.IsDeployed, Is.True);
            Assert.That(module.CurrentIntensity, Is.GreaterThan(idle * 2f));
            Assert.That(module.RingAngle, Is.GreaterThan(0f), "the module ring turns with the shield");
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
            Assert.That(puppet.LeftArmPitch, Is.EqualTo(-puppet.GuardArmPitch).Within(5f), "her usual guard, no focus gesture");
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
            Assert.That(squadPanel.CardPortrait(card), Is.EqualTo(profile.MiniPortrait));
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
            var gauntlet = member.GetComponent<SupportGauntlet>();
            Assert.That(gauntlet, Is.Not.Null);
            Assert.That(gauntlet.TetherCount, Is.EqualTo(3));
            Assert.That(Get<CharacterSkill>(gauntlet, "skill"), Is.EqualTo(member.GetComponent<PulseShield>()));
            Assert.That(member.GetComponent<CharacterPuppet>(), Is.Not.Null);
            var visual = member.transform.Find("Support Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(member.transform.Find("Body"), Is.Null);
            var names = visual.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (string part in new[] { "Torso", "Coat Back", "Hood", "Ponytail", "Hair Clip", "Origin Mark", "Stroke", "Support Module", "Module Core", "Module Ring",
                "Support Gauntlet", "Emitter", "Origin Band", "Barrier Projection", "Chest Lens" })
                Assert.That(names, Does.Contain(part));
            var materials = visual.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial.name).Distinct().ToArray();
            Assert.That(materials, Does.Contain("M_Support_Mint"));
            Assert.That(materials, Does.Contain("M_Support_Module"));
            Assert.That(materials, Does.Contain("M_Support_OriginAmber"));
            Assert.That(materials, Does.Contain("M_Support_Gauntlet"));
            Assert.That(materials, Does.Not.Contain("M_Support_Staff"));
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

        private static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => !(r is LineRenderer)).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }
        private static float Top(SquadMember member, string visual) => Bounds(member.transform.Find(visual)).max.y - member.transform.position.y;
        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
