using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Camera;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Skills;
using TRACE.Tactical;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class TacticalFocusTests
    {
        private SquadController squad;
        private TacticalFocus focus;
        private TracePlayerInput input;
        private SquadMember[] members;
        private BasicMeleeEnemy[] enemies;
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
            focus = squad.GetComponent<TacticalFocus>(); input = squad.GetComponent<TracePlayerInput>();
            Set(input, "captureCursor", false);
            members = squad.Members.ToArray();
            enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            yield return Wait(0.05f);
            foreach (var member in members)
                if (!member.IsPlayerControlled) member.Companion.enabled = false;
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

        [UnityTest] public IEnumerator HoldReducesScaleAndPhysicsStepAndReleaseRestoresBoth()
        {
            yield return Enter();
            Assert.That(focus.IsActive, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(oldFixed * 0.15f).Within(0.00001f));
            Keys(); yield return Wait(0.22f);
            Assert.That(focus.IsActive || focus.IsRestoring, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(oldFixed));
        }
        [UnityTest] public IEnumerator TransitionsUseRealTimeAndAreNotInstant()
        {
            // Batch frames last ~1 ms, so sample a third of the real-time transition instead of a frame count.
            Keys(Key.Tab); yield return Wait(0.05f);
            Assert.That(Time.timeScale, Is.InRange(0.151f, 0.9999f));
            yield return Wait(0.2f); Assert.That(Time.timeScale, Is.EqualTo(0.15f).Within(0.001f));
            Keys(); yield return Wait(0.05f);
            Assert.That(focus.IsActive, Is.False); Assert.That(input.CombatInputEnabled, Is.True);
            Assert.That(Time.timeScale, Is.InRange(0.151f, 0.9999f));
            yield return Wait(0.2f); Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
        [UnityTest] public IEnumerator RepeatedHoldDoesNotToggleOrCompoundSlowdown()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return Enter(); Assert.That(Time.timeScale, Is.EqualTo(0.15f).Within(0.001f));
                Keys(); yield return Wait(0.035f); Keys(Key.Tab); yield return Wait(0.2f);
                Assert.That(Time.timeScale, Is.EqualTo(0.15f).Within(0.001f));
                Keys(); yield return Wait(0.2f); Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
        }
        [UnityTest] public IEnumerator DisableImmediatelyRestoresTime()
        {
            yield return Enter(); focus.enabled = false;
            Assert.That(Time.timeScale, Is.EqualTo(1f)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(oldFixed));
        }
        [UnityTest] public IEnumerator SceneUnloadRestoresTime()
        {
            yield return Enter(); yield return SceneManager.LoadSceneAsync("PrototypeSwitch");
            Assert.That(Time.timeScale, Is.EqualTo(1f)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(oldFixed));
        }
        [UnityTest] public IEnumerator InputLossRestoresTime()
        {
            yield return Enter(); input.enabled = false; yield return Frames(2);
            Assert.That(focus.IsActive, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
        [UnityTest] public IEnumerator FocusLossRequiresReleaseBeforeReentry()
        {
            yield return Enter(); focus.SendMessage("OnApplicationFocus", false); yield return Frames(3);
            Assert.That(focus.IsActive, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1f));
            focus.SendMessage("OnApplicationFocus", true); yield return Frames(2);
            Assert.That(focus.IsActive, Is.False);
            Keys(); yield return Frames(2); yield return Enter(); Assert.That(focus.IsActive, Is.True);
        }
        [UnityTest] public IEnumerator DeathRestoresTimeEvenWhenSquadAutoSwitches()
        {
            yield return Enter(); members[0].Health.TakeDamage(9999f); yield return Frames(3);
            Assert.That(squad.ActiveMember, Is.Not.EqualTo(members[0]));
            Assert.That(focus.IsActive, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1f));
            Keys(); yield return Frames(2); yield return Enter(); Assert.That(focus.IsActive, Is.True);
        }
        [UnityTest] public IEnumerator ExternalPauseHasPriorityAndIsNotOverwritten()
        {
            yield return Enter(); Time.timeScale = 0f; yield return Frames(3);
            Assert.That(Time.timeScale, Is.Zero); Assert.That(focus.IsActive, Is.False);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(oldFixed));
            Keys(); yield return Frames(2); Keys(Key.Tab); yield return Frames(2);
            Assert.That(Time.timeScale, Is.Zero);
        }
        [UnityTest] public IEnumerator ExternalPhysicsStepIsPreservedOnAbort()
        {
            yield return Enter(); Time.fixedDeltaTime = 0.012f; yield return Frames(2);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.012f).Within(0.000001f)); Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(focus.IsActive, Is.False);
        }
        [UnityTest] public IEnumerator NonDefaultTimeBaselineIsRestored()
        {
            Time.timeScale = 0.5f; Time.fixedDeltaTime = 0.03f; yield return Enter();
            Assert.That(Time.timeScale, Is.EqualTo(0.075f).Within(0.001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.0045f).Within(0.0001f));
            Keys(); yield return Wait(0.22f);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.03f).Within(0.000001f));
        }
        [UnityTest] public IEnumerator CameraMouseSensitivityIsUnscaled()
        {
            var orbit = Object.FindFirstObjectByType<ThirdPersonOrbit>();
            float start = Get<float>(orbit, "yaw");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 0) }); yield return Frames(2);
            float normal = Get<float>(orbit, "yaw") - start;
            yield return Enter(); start = Get<float>(orbit, "yaw");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 0) }); yield return Frames(2);
            Assert.That(Get<float>(orbit, "yaw") - start, Is.EqualTo(normal).Within(0.01f));
            Assert.That(normal, Is.GreaterThan(1f));
            Assert.That(UnityEngine.Camera.main.GetComponent<CinemachineBrain>().IgnoreTimeScale, Is.True);
        }
        [UnityTest] public IEnumerator OneTwoThreeSwitchAndCameraFollowStayAvailable()
        {
            yield return Enter(); var orbit = Object.FindFirstObjectByType<ThirdPersonOrbit>();
            foreach (int index in new[] { 1, 2, 0 })
            {
                Keys(Key.Tab, index == 0 ? Key.Digit1 : index == 1 ? Key.Digit2 : Key.Digit3);
                yield return Wait(0.23f);
                Assert.That(squad.ActiveMember, Is.EqualTo(members[index]));
                Assert.That(orbit.FollowedCharacter, Is.EqualTo(members[index].transform));
                Assert.That(orbit.IsTransitioning, Is.False); Assert.That(focus.IsActive, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0.15f).Within(0.001f));
            }
        }
        [UnityTest] public IEnumerator FocusBlocksMoveSprintAttackDodgeAndSkill()
        {
            yield return Enter(); Vector3 start = members[0].transform.position;
            Keys(Key.Tab, Key.W, Key.LeftShift, Key.E);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            yield return Wait(0.25f);
            var motor = members[0].GetComponent<ThirdPersonMotor>();
            Assert.That(input.Move, Is.EqualTo(Vector2.zero)); Assert.That(input.Sprint, Is.False);
            Assert.That(motor.IsDodging || motor.IsSkillDashing, Is.False);
            Assert.That(members[0].GetComponent<PlayerMeleeAttack>().IsAttacking, Is.False);
            Assert.That(members[0].GetComponent<CharacterSkill>().IsReady, Is.True);
            Assert.That(members[0].GetComponent<CharacterSkill>().Activate(), Is.False);
            Assert.That(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(motor.transform.position.x, motor.transform.position.z)), Is.LessThan(0.02f));
        }
        [UnityTest] public IEnumerator BlockedSkillIsNotBufferedAndReleasePermitsImmediateNewPress()
        {
            yield return Enter(); Keys(Key.Tab, Key.E); yield return Frames(2);
            Keys(Key.E); yield return Frames(2);
            Assert.That(members[0].GetComponent<CharacterSkill>().IsReady, Is.True);
            Keys(); yield return Frames(2); Keys(Key.E); yield return Frames(2);
            Assert.That(members[0].GetComponent<CharacterSkill>().IsReady, Is.False);
        }
        [UnityTest] public IEnumerator EnteringFocusCancelsDashWithoutRefundingCooldown()
        {
            Assert.That(members[0].GetComponent<CharacterSkill>().Activate(), Is.True);
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().IsSkillDashing, Is.True);
            yield return Enter();
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().IsSkillDashing, Is.False);
            Assert.That(members[0].GetComponent<CharacterSkill>().CooldownRemaining, Is.GreaterThan(4f));
        }
        [UnityTest] public IEnumerator CooldownAndComboDurationFollowScaledClock()
        {
            members[0].GetComponent<CharacterSkill>().Activate();
            var combo = members[0].GetComponent<ComboOpportunity>(); combo.Offer(ComboOpportunityType.Protected, 3f, focus);
            yield return Enter();
            float cd = members[0].GetComponent<CharacterSkill>().CooldownRemaining;
            float remaining = combo.RemainingDuration, game = Time.time, real = Time.unscaledTime;
            yield return Wait(0.5f);
            float elapsed = Time.time - game;
            Assert.That(elapsed, Is.EqualTo((Time.unscaledTime - real) * 0.15f).Within(0.015f));
            Assert.That(cd - members[0].GetComponent<CharacterSkill>().CooldownRemaining, Is.EqualTo(elapsed).Within(0.01f));
            Assert.That(remaining - combo.RemainingDuration, Is.EqualTo(elapsed).Within(0.01f));
            Assert.That(combo.Type, Is.EqualTo(ComboOpportunityType.Protected));
        }
        [UnityTest] public IEnumerator ExistingFieldAndShieldKeepTheirScaledDurations()
        {
            var field = members[1].GetComponent<GravityFieldSkill>().Field;
            var shield = members[2].GetComponent<Shield>();
            field.Spawn(members[0].transform.position); shield.Grant(30f, 5f);
            yield return Enter();
            float fieldBefore = field.RemainingDuration, shieldBefore = shield.RemainingDuration, game = Time.time;
            yield return Wait(0.4f);
            float elapsed = Time.time - game;
            Assert.That(fieldBefore - field.RemainingDuration, Is.EqualTo(elapsed).Within(0.01f));
            Assert.That(shieldBefore - shield.RemainingDuration, Is.EqualTo(elapsed).Within(0.01f));
            Assert.That(field.isActiveAndEnabled, Is.True); Assert.That(shield.CurrentAmount, Is.EqualTo(30f));
        }
        [UnityTest] public IEnumerator EnteringFocusCancelsAttackWindup()
        {
            var attack = members[0].GetComponent<PlayerMeleeAttack>(); Set(attack, "windup", 0.5f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return Frames(2);
            Assert.That(attack.IsAttacking, Is.True); float ready = attack.ReadyAt;
            yield return Enter(); Assert.That(attack.IsAttacking, Is.False); Assert.That(attack.ReadyAt, Is.EqualTo(ready));
        }
        [UnityTest] public IEnumerator ComboConsumptionFeedbackDoesNotResetFocusTime()
        {
            var combo = members[0].GetComponent<ComboOpportunity>(); combo.Offer(ComboOpportunityType.Protected, 3f, focus);
            yield return Enter(); Assert.That(combo.TryConsume(ComboOpportunityType.Protected), Is.True);
            yield return Wait(0.3f);
            Assert.That(focus.IsActive, Is.True); Assert.That(Time.timeScale, Is.EqualTo(0.15f).Within(0.001f));
        }
        [UnityTest] public IEnumerator OverlayDisplaysThreeMembersCooldownAndCounterOnlyWhileHeld()
        {
            var overlay = squad.GetComponent<TacticalOverlay>(); Assert.That(overlay.IsVisible, Is.False);
            members[0].GetComponent<CharacterSkill>().Activate();
            members[0].GetComponent<ComboOpportunity>().Offer(ComboOpportunityType.Protected, 3f, focus);
            yield return Enter(); Assert.That(overlay.IsVisible, Is.True);
            StringAssert.Contains("TRACEWALKER", overlay.MemberInfo(0)); StringAssert.Contains("CONTRE", overlay.MemberInfo(0));
            StringAssert.Contains("HP", overlay.MemberInfo(0)); StringAssert.DoesNotContain("READY", overlay.MemberInfo(0));
            StringAssert.Contains("CONTROL", overlay.MemberInfo(1)); StringAssert.Contains("READY", overlay.MemberInfo(1));
            StringAssert.Contains("SUPPORT", overlay.MemberInfo(2)); StringAssert.Contains("READY", overlay.MemberInfo(2));
            Keys(); yield return Frames(3); Assert.That(overlay.IsVisible, Is.False);
        }
        [UnityTest] public IEnumerator VisibleEnemyCardShowsGroupedAndHidesOutsideView()
        {
            var camera = UnityEngine.Camera.main; camera.GetComponent<CinemachineBrain>().enabled = false;
            var enemy = enemies[0]; enemy.gameObject.SetActive(true); enemy.enabled = false;
            enemy.GetComponent<NavMeshAgent>().Warp(members[0].transform.position + Vector3.forward * 5f);
            camera.transform.position = enemy.transform.position + new Vector3(0, 4, -7);
            camera.transform.LookAt(enemy.transform.position + Vector3.up); camera.aspect = 16f / 9f;
            enemy.GetComponent<ComboOpportunity>().Offer(ComboOpportunityType.Grouped, 2.5f, focus);
            yield return Enter(); var overlay = squad.GetComponent<TacticalOverlay>();
            Assert.That(overlay.VisibleEnemyCount, Is.EqualTo(1));
            StringAssert.Contains("GROUPED", overlay.EnemyInfo(0)); StringAssert.Contains("1 + E", overlay.EnemyInfo(0));
            camera.transform.Rotate(0, 180f, 0); yield return Frames(3); Assert.That(overlay.VisibleEnemyCount, Is.Zero);
        }
        [UnityTest] public IEnumerator EnemyWindupTelegraphAndAttackContinueInGameTime()
        {
            var enemy = enemies[0]; enemy.gameObject.SetActive(true);
            enemy.GetComponent<NavMeshAgent>().Warp(members[0].transform.position + Vector3.forward * 1.3f);
            float deadline = Time.unscaledTime + 2f;
            while (enemy.Phase != BasicMeleeEnemy.AttackPhase.Windup && Time.unscaledTime < deadline) yield return null;
            Assert.That(enemy.Phase, Is.EqualTo(BasicMeleeEnemy.AttackPhase.Windup));
            yield return Enter();
            Assert.That(enemy.GetComponent<TacticalTelegraph>().IsVisible, Is.True);
            float remaining = enemy.WindupRemaining, game = Time.time;
            yield return Wait(0.2f);
            Assert.That(remaining - enemy.WindupRemaining, Is.EqualTo(Time.time - game).Within(0.015f));
            Keys(); yield return Wait(0.8f);
            Assert.That(enemy.GetComponent<TacticalTelegraph>().IsVisible, Is.False);
            Assert.That(members[0].Health.CurrentHealth, Is.LessThan(members[0].Health.MaxHealth));
        }

        private IEnumerator Enter() { Keys(Key.Tab); yield return Wait(0.22f); }
        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
