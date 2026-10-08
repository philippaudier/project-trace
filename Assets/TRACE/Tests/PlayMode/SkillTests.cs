using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class SkillTests
    {
        private SquadController squad;
        private SquadMember[] members;
        private CharacterSkill[] skills;
        private BasicMeleeEnemy[] enemies;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private GameObject wall;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Prototype");
            squad = Object.FindFirstObjectByType<SquadController>();
            members = squad.Members.ToArray();
            skills = members.Select(m => m.GetComponent<CharacterSkill>()).ToArray();
            var input = squad.GetComponent<TracePlayerInput>();
            typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, false);
            enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None);
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            yield return Frames(10);
            FreezeCompanions();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (wall != null) Object.Destroy(wall);
            Time.timeScale = 1f;
            Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SceneHasThreeDistinctSkillsAndFourExistingEnemies()
        {
            Assert.That(skills[0], Is.TypeOf<DashStrike>());
            Assert.That(skills[1], Is.TypeOf<GravityFieldSkill>());
            Assert.That(skills[2], Is.TypeOf<PulseShield>());
            Assert.That(enemies.Length, Is.EqualTo(4));
            Assert.That(members.All(m => m.GetComponent<Shield>() != null), Is.True);
            Assert.That(Object.FindFirstObjectByType<SkillHud>(), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QStartsDashThenDealsThirtyFiveAtArrival()
        {
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            var enemy = EnemyAt(new Vector3(-11f, 0f, 7.5f));
            var hp = enemy.GetComponent<Health>();
            Keys(Key.E);
            yield return Frames(1);
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().IsSkillDashing, Is.True);
            Assert.That(hp.CurrentHealth, Is.EqualTo(100f));
            Assert.That(members[0].transform.position.z, Is.LessThan(7f), "Dash must not teleport.");
            Keys();
            yield return Frames(16);
            Assert.That(hp.CurrentHealth, Is.EqualTo(65f));
            Assert.That(members[0].transform.position.z, Is.GreaterThan(5.5f));
            Assert.That(skills[0].CooldownRemaining, Is.GreaterThan(4.5f));
            yield return Frames(20);
            Assert.That(hp.CurrentHealth, Is.EqualTo(65f), "One arrival causes one hit.");
        }

        [UnityTest]
        public IEnumerator UntargetedDashMovesForwardThreeMetres()
        {
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            Vector3 start = members[0].transform.position;
            Assert.That(skills[0].Activate(), Is.True);
            yield return Frames(16);
            Assert.That(members[0].transform.position.z - start.z, Is.InRange(2.7f, 3.2f));
            Assert.That(skills[0].Activate(), Is.False);
        }

        [UnityTest]
        public IEnumerator UntargetedDashUsesMovementDirection()
        {
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            Keys(Key.D);
            yield return Frames(1);
            Vector3 start = members[0].transform.position;
            Assert.That(skills[0].Activate(), Is.True);
            Keys();
            yield return Frames(16);
            Assert.That(members[0].transform.position.x - start.x, Is.GreaterThan(2.5f));
        }

        [UnityTest]
        public IEnumerator DashCannotCrossWallOrDamageThroughIt()
        {
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            var enemy = EnemyAt(new Vector3(-11f, 0f, 7f));
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(-11f, 1.5f, 4.5f);
            wall.transform.localScale = new Vector3(4f, 3f, 0.25f);
            Physics.SyncTransforms();
            Assert.That(skills[0].Activate(), Is.True);
            yield return Frames(18);
            Assert.That(members[0].transform.position.z, Is.LessThan(4.2f));
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator DashCooldownExpiresAfterFiveSeconds()
        {
            Assert.That(skills[0].Activate(), Is.True);
            Assert.That(skills[0].Activate(), Is.False);
            yield return Frames(290);
            Assert.That(skills[0].IsReady, Is.False);
            yield return Frames(20);
            Assert.That(skills[0].IsReady, Is.True);
            Assert.That(skills[0].Activate(), Is.True);
        }

        [UnityTest]
        public IEnumerator SwitchCancelsDashWithoutDelayedDamageOrCooldownReset()
        {
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            var enemy = EnemyAt(new Vector3(-11f, 0f, 7f));
            skills[0].Activate();
            yield return Frames(2);
            Assert.That(squad.SwitchToMember(1), Is.True);
            FreezeCompanions();
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().IsSkillDashing, Is.False);
            yield return Frames(20);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(squad.SwitchToMember(0), Is.True);
            Assert.That(skills[0].Activate(), Is.False);
        }

        [UnityTest]
        public IEnumerator GravityAppearsNearTargetAndSlowsFortyPercent()
        {
            Select(1);
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            var enemy = EnemyAt(new Vector3(-11f, 0f, 7f));
            var agent = enemy.GetComponent<NavMeshAgent>();
            float before = agent.speed;
            Keys(Key.E);
            yield return Frames(2);
            Keys();
            var field = ((GravityFieldSkill)skills[1]).Field;
            Assert.That(field.gameObject.activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(field.transform.position, enemy.transform.position), Is.LessThan(0.4f));
            Assert.That(agent.speed, Is.EqualTo(before * 0.6f).Within(0.01f));
            Assert.That(skills[1].Activate(), Is.False);
        }

        [UnityTest]
        public IEnumerator GravityPullUsesNavMeshAndRestoresSpeedOnExpiry()
        {
            Select(1);
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            skills[1].Activate();
            var field = ((GravityFieldSkill)skills[1]).Field;
            var enemy = EnemyAt(field.transform.position + Vector3.right * 2f);
            var agent = enemy.GetComponent<NavMeshAgent>();
            float before = agent.speed;
            float initial = Vector3.Distance(enemy.transform.position, field.transform.position);
            yield return Frames(45);
            Assert.That(Vector3.Distance(enemy.transform.position, field.transform.position), Is.LessThan(initial - 0.5f));
            Assert.That(agent.isOnNavMesh, Is.True);
            Assert.That(agent.speed, Is.EqualTo(before * 0.6f).Within(0.01f));
            yield return Frames(145);
            Assert.That(field.gameObject.activeSelf, Is.False);
            Assert.That(agent.speed, Is.EqualTo(before).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator LeavingGravityRestoresSpeedWithoutWaitingForZoneExpiry()
        {
            Select(1);
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            skills[1].Activate();
            var field = ((GravityFieldSkill)skills[1]).Field;
            var enemy = EnemyAt(field.transform.position + Vector3.right);
            var agent = enemy.GetComponent<NavMeshAgent>();
            float before = agent.speed;
            yield return Frames(3);
            Assert.That(agent.speed, Is.LessThan(before));
            agent.Warp(new Vector3(-16f, 0f, 15f));
            yield return Frames(9);
            Assert.That(agent.speed, Is.EqualTo(before));
            Assert.That(field.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator GravityCooldownPersistsAfterSwitchAndExpiresAtEightSeconds()
        {
            Select(1);
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            Assert.That(skills[1].Activate(), Is.True);
            yield return Frames(15);
            Select(2);
            Assert.That(((GravityFieldSkill)skills[1]).Field.gameObject.activeSelf, Is.True);
            yield return Frames(15);
            Select(1);
            Assert.That(skills[1].Activate(), Is.False);
            yield return Frames(460);
            Assert.That(skills[1].IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator GravityContinuesAfterCasterDeathAndCleansUp()
        {
            Select(1);
            PositionActive(new Vector3(-11f, 0.08f, 3f));
            skills[1].Activate();
            var field = ((GravityFieldSkill)skills[1]).Field;
            var enemy = EnemyAt(field.transform.position + Vector3.right);
            float speed = enemy.GetComponent<NavMeshAgent>().speed;
            members[1].Health.TakeDamage(100f);
            yield return Frames(4);
            Assert.That(field.gameObject.activeSelf, Is.True);
            Assert.That(enemy.GetComponent<NavMeshAgent>().speed, Is.LessThan(speed));
            FreezeCompanions();
            yield return Frames(190);
            Assert.That(enemy.GetComponent<NavMeshAgent>().speed, Is.EqualTo(speed));
        }

        [UnityTest]
        public IEnumerator QAppliesThirtyShieldToAllThreeMembers()
        {
            Select(2);
            Keys(Key.E);
            yield return Frames(2);
            Keys();
            foreach (var member in members) Assert.That(member.GetComponent<Shield>().CurrentAmount, Is.EqualTo(30f));
            Assert.That(skills[2].Activate(), Is.False);
        }

        [UnityTest]
        public IEnumerator ShieldAbsorbsBeforeHealthAndOverflowPassesThrough()
        {
            Select(2);
            skills[2].Activate();
            var receiver = members[0].Receiver;
            Assert.That(receiver.TryTakeDamage(20f), Is.True);
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(members[0].GetComponent<Shield>().CurrentAmount, Is.EqualTo(10f));
            Assert.That(receiver.TryTakeDamage(25f), Is.True);
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(85f));
            Assert.That(members[0].GetComponent<Shield>().CurrentAmount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnconsumedShieldExpiresAtFiveSeconds()
        {
            Select(2);
            skills[2].Activate();
            yield return Frames(310);
            foreach (var member in members) Assert.That(member.GetComponent<Shield>().CurrentAmount, Is.Zero);
            members[0].Receiver.TryTakeDamage(20f);
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(80f));
        }

        [UnityTest]
        public IEnumerator ShieldCooldownIsTenSecondsAndDoesNotResetOnTransfer()
        {
            Select(2);
            skills[2].Activate();
            yield return Frames(15);
            Select(0);
            yield return Frames(15);
            Select(2);
            Assert.That(skills[2].CooldownRemaining, Is.GreaterThan(9f));
            yield return Frames(580);
            Assert.That(skills[2].Activate(), Is.True);
        }

        [UnityTest]
        public IEnumerator DodgeIFramesDoNotConsumeShield()
        {
            Select(2);
            skills[2].Activate();
            yield return Frames(3); // The newly enabled motor samples its ground support before dodging.
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return Frames(4);
            Assert.That(members[2].Receiver.IsInvulnerable, Is.True);
            Assert.That(members[2].Receiver.TryTakeDamage(20f), Is.False);
            Assert.That(members[2].GetComponent<Shield>().CurrentAmount, Is.EqualTo(30f));
        }

        [UnityTest]
        public IEnumerator ShieldRejectsInvalidDamageAndNeverShieldsDeadMembers()
        {
            members[0].Health.TakeDamage(100f);
            yield return Frames(15);
            Select(2);
            skills[2].Activate();
            Assert.That(members[0].GetComponent<Shield>().CurrentAmount, Is.Zero);
            foreach (float invalid in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
                Assert.That(members[2].Receiver.TryTakeDamage(invalid), Is.False);
            Assert.That(members[2].GetComponent<Shield>().CurrentAmount, Is.EqualTo(30f));
        }

        [UnityTest]
        public IEnumerator ThreeSkillsHaveIndependentCooldownsThroughFullRotation()
        {
            Assert.That(skills[0].Activate(), Is.True);
            yield return Frames(15);
            Select(1);
            Assert.That(skills[1].Activate(), Is.True);
            Assert.That(skills[2].IsReady, Is.True);
            yield return Frames(15);
            Select(2);
            Assert.That(skills[2].Activate(), Is.True);
            yield return Frames(15);
            Select(0);
            Assert.That(skills.All(s => s.CooldownRemaining > 0f), Is.True);
            Assert.That(skills[0].Activate(), Is.False);
        }

        [UnityTest]
        public IEnumerator InactiveCompanionsCannotActivateOrConsumeCooldown()
        {
            Assert.That(skills[1].Activate(), Is.False);
            Assert.That(skills[2].Activate(), Is.False);
            Keys(Key.E);
            yield return Frames(3);
            Keys();
            Assert.That(skills[0].IsReady, Is.False);
            Assert.That(skills[1].IsReady && skills[2].IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator SimultaneousSwitchAndQUsesNewMemberOnly()
        {
            Keys(Key.Digit3, Key.E);
            yield return Frames(2);
            Keys();
            Assert.That(squad.ActiveMember, Is.EqualTo(members[2]));
            Assert.That(skills[2].IsReady, Is.False);
            Assert.That(skills[0].IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseAndDodgeRejectSkillWithoutConsumingCooldown()
        {
            Time.timeScale = 0f;
            Assert.That(skills[0].Activate(), Is.False);
            Time.timeScale = 1f;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return Frames(2);
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().IsDodging, Is.True);
            Assert.That(skills[0].Activate(), Is.False);
            Assert.That(skills[0].IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator ShieldPersistsWhenSupportDiesUntilNormalExpiry()
        {
            Select(2);
            skills[2].Activate();
            members[2].Health.TakeDamage(100f);
            yield return Frames(2);
            Assert.That(members[2].GetComponent<Shield>().CurrentAmount, Is.Zero);
            Assert.That(members[0].GetComponent<Shield>().CurrentAmount, Is.EqualTo(30f));
            Assert.That(skills[2].Activate(), Is.False);
            yield return Frames(310);
            Assert.That(members[0].GetComponent<Shield>().CurrentAmount, Is.Zero);
        }

        private void Select(int index)
        {
            Assert.That(squad.SwitchToMember(index), Is.True);
            FreezeCompanions();
        }
        private void FreezeCompanions()
        {
            foreach (var member in members)
            {
                if (member.IsPlayerControlled || member.Health.IsDead) continue;
                member.Companion.enabled = false;
                member.Companion.Attack.Cancel();
                var agent = member.GetComponent<NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            }
        }
        private void PositionActive(Vector3 position)
        {
            var member = squad.ActiveMember;
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false;
            body.enabled = false;
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true;
            motor.enabled = true;
            Physics.SyncTransforms();
        }
        private BasicMeleeEnemy EnemyAt(Vector3 position)
        {
            var enemy = enemies[0];
            enemy.gameObject.SetActive(true);
            enemy.enabled = false;
            var agent = enemy.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(position), Is.True);
            agent.ResetPath();
            agent.isStopped = true;
            Physics.SyncTransforms();
            return enemy;
        }
        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    }
}
