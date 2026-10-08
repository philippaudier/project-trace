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
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class TracewalkerAnimationTests
    {
        private SquadController squad;
        private TacticalFocus focus;
        private SquadMember member;
        private CharacterPuppet puppet;
        private Transform visual;
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
            yield return SceneManager.LoadSceneAsync("Prototype");
            squad = Object.FindFirstObjectByType<SquadController>();
            focus = squad.GetComponent<TacticalFocus>();
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            member = squad.Members[0];
            puppet = member.GetComponent<CharacterPuppet>();
            visual = member.transform.Find("Tracewalker Visual");
            foreach (var enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
            foreach (var other in squad.Members) if (!other.IsPlayerControlled) other.Companion.enabled = false;
            yield return Wait(0.3f);
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

        [UnityTest] public IEnumerator PuppetIsWiredToPivotsAndRestsStill()
        {
            Assert.That(puppet, Is.Not.Null);
            Assert.That(puppet.IsActive, Is.True);
            foreach (string pivot in new[] { "body", "upperBody", "head", "leftHip", "rightHip", "leftShoulder", "rightShoulder" })
                Assert.That(Get<Transform>(puppet, pivot), Is.Not.Null, pivot);
            Assert.That(Get<ThirdPersonMotor>(puppet, "motor"), Is.Not.Null);
            Assert.That(Get<PlayerMeleeAttack>(puppet, "melee"), Is.Not.Null);
            Assert.That(Get<CompanionController>(puppet, "companion"), Is.Not.Null);
            Assert.That(Get<Health>(puppet, "health"), Is.Not.Null);
            Assert.That(Get<TacticalFocus>(puppet, "focus"), Is.Not.Null);
            // The blade swings with the right arm, the mark rides the left arm.
            Assert.That(visual.Find("Upper Body/Shoulder R/Blade"), Is.Not.Null);
            Assert.That(visual.Find("Upper Body/Shoulder L/Origin Mark"), Is.Not.Null);
            Assert.That(Mathf.Abs(puppet.LegSwing), Is.LessThan(2f));
            Assert.That(puppet.Speed, Is.LessThan(0.2f));
            Assert.That(Mathf.Abs(puppet.Lean), Is.LessThan(3f), "breathing stays subtle");
            yield return null;
        }

        [UnityTest] public IEnumerator WalkingSwingsTheLegsAndStoppingSettles()
        {
            Keys(Key.W);
            float peak = 0f;
            float deadline = Time.unscaledTime + 0.7f;
            while (Time.unscaledTime < deadline) { peak = Mathf.Max(peak, Mathf.Abs(puppet.LegSwing)); yield return null; }
            Assert.That(puppet.Speed, Is.GreaterThan(1.5f));
            Assert.That(peak, Is.GreaterThan(8f));
            Assert.That(Get<Transform>(puppet, "leftHip").localEulerAngles, Is.Not.EqualTo(Vector3.zero));
            Keys(); yield return Wait(0.8f);
            Assert.That(puppet.Speed, Is.LessThan(0.3f));
            Assert.That(Mathf.Abs(puppet.LegSwing), Is.LessThan(2f));
            Assert.That(Mathf.Abs(puppet.RightArmPitch), Is.LessThan(2f));
        }

        [UnityTest] public IEnumerator AttackPullsTheArmBackThenSwingsForwardAndRecovers()
        {
            var attack = member.GetComponent<PlayerMeleeAttack>();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Wait(0.035f);
            Assert.That(attack.IsAttacking, Is.True);
            Assert.That(puppet.RightArmPitch, Is.GreaterThan(10f), "windup pulls the arm back");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Wait(0.11f);
            Assert.That(puppet.RightArmPitch, Is.LessThan(-20f), "swing whips the arm forward");
            yield return Wait(0.9f);
            Assert.That(attack.IsAttacking, Is.False);
            Assert.That(Mathf.Abs(puppet.RightArmPitch), Is.LessThan(5f), "recovered");
        }

        [UnityTest] public IEnumerator DodgeCrouchesTheBodyThenStandsUp()
        {
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = Get<Transform>(puppet, "body");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return Wait(0.12f);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            Assert.That(motor.IsDodging, Is.True);
            Assert.That(puppet.Crouch, Is.GreaterThan(0.1f));
            Assert.That(body.localPosition.y, Is.LessThan(-0.1f));
            Assert.That(puppet.Lean, Is.GreaterThan(4f));
            yield return Wait(0.9f);
            Assert.That(motor.IsDodging, Is.False);
            Assert.That(puppet.Crouch, Is.LessThan(0.02f));
            Assert.That(body.localPosition.y, Is.GreaterThan(-0.02f));
        }

        [UnityTest] public IEnumerator DashStrikeLeansForwardWithTheBladeAhead()
        {
            var motor = member.GetComponent<ThirdPersonMotor>();
            Assert.That(member.GetComponent<CharacterSkill>().Activate(), Is.True);
            yield return Wait(0.12f);
            Assert.That(motor.IsSkillDashing, Is.True);
            Assert.That(puppet.Lean, Is.GreaterThan(12f));
            Assert.That(puppet.RightArmPitch, Is.LessThan(-40f));
            yield return Wait(1.2f);
            Assert.That(motor.IsSkillDashing, Is.False);
            Assert.That(puppet.Lean, Is.LessThan(4f));
        }

        [UnityTest] public IEnumerator TacticalFocusRaisesTheLeftHandToTheModule()
        {
            Keys(Key.Tab); yield return Wait(0.9f);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(puppet.LeftArmPitch, Is.LessThan(-40f));
            Keys(); yield return Wait(0.8f);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(puppet.LeftArmPitch, Is.GreaterThan(-5f));
        }

        [UnityTest] public IEnumerator HitFlinchesBackAndDeathHandsThePoseToFeedback()
        {
            var receiver = member.GetComponent<DamageReceiver>();
            Assert.That(receiver.TryTakeDamage(10f), Is.True);
            yield return Wait(0.05f);
            Assert.That(puppet.Lean, Is.LessThan(-4f), "flinch leans back");
            yield return Wait(0.8f);
            Assert.That(puppet.Lean, Is.GreaterThan(-2f));
            Assert.That(receiver.TryTakeDamage(1000f), Is.True);
            yield return Wait(0.1f);
            Assert.That(member.Health.IsDead, Is.True);
            Assert.That(puppet.IsActive, Is.False);
            Assert.That(visual.localEulerAngles.z, Is.EqualTo(90f).Within(0.5f), "CompanionFeedback lays the body down");
            Assert.That(visual.localPosition.y, Is.EqualTo(0.35f).Within(0.01f), "the puppet no longer moves the body");
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
