using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Camera;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class SwitchTests
    {
        private SquadController squad;
        private SquadMember[] members;
        private TracePlayerInput input;
        private ThirdPersonOrbit orbit;
        private BasicMeleeEnemy[] enemies;
        private Keyboard keyboard;
        private Mouse mouse;
        private float previousCapture;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("PrototypeSwitch");
            squad = Object.FindFirstObjectByType<SquadController>();
            members = squad.Members.ToArray();
            input = Object.FindFirstObjectByType<TracePlayerInput>();
            Set(input, "captureCursor", false);
            orbit = Object.FindFirstObjectByType<ThirdPersonOrbit>();
            enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None);
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            yield return Frames(10);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = previousCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OneInputOwnerAndExactlyOnePlayerMotor()
        {
            Assert.That(Object.FindObjectsByType<TracePlayerInput>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(members.Length, Is.EqualTo(3));
            AssertOwnership(0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NumberKeysSwitchOneToTwoToThreeToOne()
        {
            Keys(Key.Digit2);
            yield return Frames(1);
            AssertOwnership(1);
            Keys();
            yield return Frames(15);
            Keys(Key.Digit3);
            yield return Frames(1);
            AssertOwnership(2);
            Keys();
            yield return Frames(15);
            Keys(Key.Digit1);
            yield return Frames(1);
            AssertOwnership(0);
        }

        [UnityTest]
        public IEnumerator TransferPreservesAllPositionsRotationsAndHealth()
        {
            members[1].Health.TakeDamage(17f);
            members[2].Health.TakeDamage(33f);
            var positions = members.Select(m => m.transform.position).ToArray();
            var rotations = members.Select(m => m.transform.rotation).ToArray();
            var health = members.Select(m => m.Health.CurrentHealth).ToArray();
            Assert.That(squad.SwitchToMember(1), Is.True);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Vector3.Distance(members[i].transform.position, positions[i]), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(members[i].transform.rotation, rotations[i]), Is.LessThan(0.01f));
                Assert.That(members[i].Health.CurrentHealth, Is.EqualTo(health[i]));
                Assert.That(members[i].Companion.RecoveryCount, Is.Zero);
            }
            AssertOwnership(1);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameMemberAndInvalidIndicesAreNoOpsWithoutConsumingCooldown()
        {
            Assert.That(squad.SwitchToMember(0), Is.False);
            Assert.That(squad.SwitchToMember(-1), Is.False);
            Assert.That(squad.SwitchToMember(3), Is.False);
            Assert.That(squad.SwitchToMember(1), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CooldownRejectsSpamAndHeldNumberDoesNotCycle()
        {
            Assert.That(squad.SwitchToMember(1), Is.True);
            Assert.That(squad.SwitchToMember(2), Is.False);
            yield return Frames(8);
            Assert.That(squad.SwitchToMember(2), Is.False);
            yield return Frames(6);
            Keys(Key.Digit3);
            yield return Frames(1);
            AssertOwnership(2);
            yield return Frames(60);
            AssertOwnership(2);
            Assert.That(squad.SwitchToMember(0), Is.True);
        }

        [UnityTest]
        public IEnumerator EachActiveMemberReceivesMovementAndSprint()
        {
            for (int i = 0; i < 3; i++)
            {
                if (i != 0) Assert.That(squad.SwitchToMember(i), Is.True);
                Place(members[i], new Vector3(8f, 0.05f, -12f));
                yield return Frames(5);
                Vector3 start = members[i].transform.position;
                Keys(Key.W, Key.LeftShift);
                yield return Frames(60);
                Keys();
                Assert.That(members[i].transform.position.z - start.z, Is.InRange(4.8f, 6.4f));
                AssertOwnership(i);
                yield return Frames(20);
            }
        }

        [UnityTest]
        public IEnumerator SwitchingDuringSprintTransfersHeldInputWithoutNavMeshConflict()
        {
            Keys(Key.D, Key.LeftShift);
            yield return Frames(25);
            Vector3 start = members[1].transform.position;
            Assert.That(squad.SwitchToMember(1), Is.True);
            Assert.That(Vector3.Distance(start, members[1].transform.position), Is.LessThan(0.0001f));
            yield return Frames(30);
            Assert.That(members[1].transform.position.x - start.x, Is.GreaterThan(1.5f));
            AssertOwnership(1);
            Assert.That(members[1].GetComponent<NavMeshAgent>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator CameraTransfersSmoothlyAndKeepsOrientation()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(160f, 40f) });
            yield return Frames(10);
            MouseButtons();
            yield return new WaitForSecondsRealtime(0.2f); // Let existing look smoothing settle before measuring preservation.
            yield return Frames(2);
            var cameraTarget = GameObject.Find("Camera Orbit Target").transform;
            Vector3 before = cameraTarget.position;
            Quaternion orientation = cameraTarget.rotation;
            Assert.That(squad.SwitchToMember(2), Is.True);
            Assert.That(orbit.FollowedCharacter, Is.EqualTo(members[2].transform));
            Assert.That(Vector3.Distance(before, cameraTarget.position), Is.LessThan(0.0001f));
            yield return Frames(1);
            Assert.That(orbit.IsTransitioning, Is.True);
            Assert.That(Vector3.Distance(before, cameraTarget.position), Is.LessThan(0.6f));
            // Orbit uses unscaled time; captureDeltaTime advances gameplay faster than wall time in batch.
            yield return new WaitForSecondsRealtime(0.22f);
            yield return Frames(2);
            Assert.That(orbit.IsTransitioning, Is.False);
            Assert.That(Quaternion.Angle(orientation, cameraTarget.rotation), Is.LessThan(0.5f));
            Assert.That(Vector3.Distance(cameraTarget.position, members[2].transform.position + Vector3.up * 1.8f), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator FormationRebuildsAroundNewLeaderAndSettles()
        {
            Assert.That(squad.SwitchToMember(2), Is.True);
            Assert.That(squad.Leader, Is.EqualTo(members[2].Receiver));
            Assert.That(squad.Companions, Does.Contain(members[0].Companion));
            Assert.That(squad.Companions, Has.No.Member(members[2].Companion));
            yield return Frames(360);
            foreach (var companion in squad.Companions)
            {
                Assert.That(Vector3.Distance(companion.transform.position, squad.FormationPosition(companion.FormationOffset)), Is.LessThan(0.95f));
                Assert.That(companion.RecoveryCount, Is.Zero);
            }
            Assert.That(Vector3.Distance(squad.Companions[0].transform.position, squad.Companions[1].transform.position), Is.GreaterThan(1.1f));
        }

        [UnityTest]
        public IEnumerator AllThreeCanAttackWithTheirOwnDamageAndRange()
        {
            for (int i = 0; i < 3; i++)
            {
                if (i != 0) Assert.That(squad.SwitchToMember(i), Is.True);
                Place(members[i], new Vector3(8f, 0.05f, -12f));
                Health victim = Victim(new Vector3(8f, 0.05f, i == 2 ? -8f : -10.8f));
                yield return Frames(6);
                MouseButtons(attack: true);
                yield return Frames(28);
                MouseButtons();
                Assert.That(victim.CurrentHealth, Is.EqualTo(i == 0 ? 75f : i == 1 ? 88f : 92f));
                yield return Frames(90);
                Assert.That(victim.CurrentHealth, Is.EqualTo(i == 0 ? 75f : i == 1 ? 88f : 92f), "Holding a button must not repeatedly attack.");
                Object.Destroy(victim.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EveryMemberCanDodgeWithRightMouseAndReceiveIFrames()
        {
            for (int i = 0; i < 3; i++)
            {
                if (i != 0) Assert.That(squad.SwitchToMember(i), Is.True);
                Place(members[i], new Vector3(10f, 0.05f, -12f));
                yield return Frames(8);
                Vector3 start = members[i].transform.position;
                Keys(Key.D);
                MouseButtons(dodge: true);
                yield return Frames(5);
                Assert.That(members[i].GetComponent<ThirdPersonMotor>().IsDodging, Is.True);
                Assert.That(members[i].Receiver.IsInvulnerable, Is.True);
                Assert.That(members[i].Receiver.TryTakeDamage(10f), Is.False);
                Keys();
                MouseButtons();
                yield return Frames(25);
                Assert.That(members[i].transform.position.x - start.x, Is.InRange(2.9f, 3.1f));
            }
        }

        [UnityTest]
        public IEnumerator SwitchingDuringDodgeCancelsOldDodgeAndInvulnerability()
        {
            MouseButtons(dodge: true);
            yield return Frames(5);
            Assert.That(members[0].Receiver.IsInvulnerable, Is.True);
            Assert.That(squad.SwitchToMember(1), Is.True);
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().IsDodging, Is.False);
            Assert.That(members[0].Receiver.IsInvulnerable, Is.False);
            AssertOwnership(1);
        }

        [UnityTest]
        public IEnumerator SwitchingToAttackingAIStopsPendingDamageAndPreservesCooldown()
        {
            PrepareAIShot();
            var victim = enemies[0].GetComponent<Health>();
            for (int i = 0; i < 120 && !members[2].Companion.Attack.IsWindingUp; i++) yield return null;
            Assert.That(members[2].Companion.Attack.IsWindingUp, Is.True);
            float readyAt = members[2].Companion.Attack.ReadyAt;
            Assert.That(squad.SwitchToMember(2), Is.True);
            Assert.That(members[2].Companion.Attack.IsWindingUp, Is.False);
            Assert.That(members[2].Companion.Attack.ReadyAt, Is.EqualTo(readyAt));
            Assert.That(members[2].GetComponent<PlayerRangedAttack>().CurrentTarget, Is.EqualTo(victim));
            MouseButtons(attack: true);
            yield return Frames(35);
            MouseButtons();
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f), "Cancelled AI strike and early player click must deal no damage.");
            yield return Frames(60);
            MouseButtons(attack: true);
            yield return Frames(25);
            Assert.That(victim.CurrentHealth, Is.EqualTo(92f));
            AssertOwnership(2);
        }

        [UnityTest]
        public IEnumerator SwitchingAwayCancelsPlayerWindupAndRoundTripCannotResetCooldown()
        {
            Assert.That(squad.SwitchToMember(1), Is.True);
            Place(members[1], new Vector3(8f, 0.05f, -12f));
            var victim = Victim(new Vector3(8f, 0.05f, -10.8f));
            yield return Frames(16);
            MouseButtons(attack: true);
            yield return Frames(1);
            Assert.That(members[1].GetComponent<PlayerMeleeAttack>().IsAttacking, Is.True);
            MouseButtons();
            Assert.That(squad.SwitchToMember(0), Is.True);
            yield return Frames(14);
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f));
            Assert.That(squad.SwitchToMember(1), Is.True);
            Place(members[1], new Vector3(8f, 0.05f, -12f));
            MouseButtons(attack: true);
            yield return Frames(20);
            MouseButtons();
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f));
            yield return Frames(35);
            MouseButtons(attack: true);
            yield return Frames(25);
            Assert.That(victim.CurrentHealth, Is.EqualTo(88f));
            Object.Destroy(victim.gameObject);
        }

        [UnityTest]
        public IEnumerator ControlledRangedAttackCannotShootThroughWalls()
        {
            Assert.That(squad.SwitchToMember(2), Is.True);
            Place(members[2], new Vector3(8f, 0.05f, -12f));
            var victim = Victim(new Vector3(8f, 0.05f, -8f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(8f, 1.5f, -10f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.5f);
            Physics.SyncTransforms();
            yield return Frames(5);
            MouseButtons(attack: true);
            yield return Frames(30);
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f));
            Object.Destroy(wall);
            Object.Destroy(victim.gameObject);
        }

        [UnityTest]
        public IEnumerator DeadPreferredRangedTargetIsDroppedAndSoftTargetingResumes()
        {
            Assert.That(squad.SwitchToMember(2), Is.True);
            Place(members[2], new Vector3(8f, 0.05f, -12f));
            var dead = Victim(new Vector3(8f, 0.05f, -9f));
            dead.TakeDamage(100f);
            var live = Victim(new Vector3(8.5f, 0.05f, -8f));
            members[2].GetComponent<PlayerRangedAttack>().PreferTarget(dead);
            yield return Frames(6);
            MouseButtons(attack: true);
            yield return Frames(25);
            Assert.That(live.CurrentHealth, Is.EqualTo(92f));
            Object.Destroy(dead.gameObject);
            Object.Destroy(live.gameObject);
        }

        [UnityTest]
        public IEnumerator DeadMemberCannotBeSelected()
        {
            members[1].Health.TakeDamage(100f);
            Assert.That(squad.SwitchToMember(1), Is.False);
            Keys(Key.Digit2);
            yield return Frames(2);
            AssertOwnership(0);
            Assert.That(squad.SwitchToMember(2), Is.True);
        }

        [UnityTest]
        public IEnumerator ActiveDeathSelectsFirstLivingMemberIgnoringSwitchCooldown()
        {
            Assert.That(squad.SwitchToMember(2), Is.True);
            members[2].Health.TakeDamage(100f);
            yield return Frames(2);
            AssertOwnership(0);
            Assert.That(input.enabled, Is.True);
            members[0].Health.TakeDamage(100f);
            yield return Frames(2);
            AssertOwnership(1);
            Assert.That(orbit.FollowedCharacter, Is.EqualTo(members[1].transform));
            Assert.That(squad.IsDefeated, Is.False);
        }

        [UnityTest]
        public IEnumerator AllDeadIsDefeatWithoutResurrectionOrInput()
        {
            foreach (var member in members) member.Health.TakeDamage(100f);
            yield return Frames(3);
            Assert.That(squad.IsDefeated, Is.True);
            Assert.That(squad.ActiveMember, Is.Null);
            Assert.That(input.enabled, Is.False);
            Assert.That(squad.SwitchToMember(0), Is.False);
            foreach (var member in members)
            {
                Assert.That(member.gameObject.activeSelf, Is.True);
                Assert.That(member.GetComponent<ThirdPersonMotor>().enabled, Is.False);
                Assert.That(member.GetComponent<NavMeshAgent>().enabled, Is.False);
                Assert.That(member.GetComponent<CharacterController>().enabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator IndicatorTracksSelectedMemberAndNeverShowsTwoActiveRings()
        {
            var indicator = GameObject.Find("Active Member Ring");
            Assert.That(indicator.GetComponent<LineRenderer>().loop, Is.True);
            for (int i = 1; i < 3; i++)
            {
                Assert.That(squad.SwitchToMember(i), Is.True);
                yield return Frames(16);
                Assert.That(Vector3.Distance(indicator.transform.position, members[i].transform.position + Vector3.up * 0.06f), Is.LessThan(0.01f));
            }
        }

        [UnityTest]
        public IEnumerator OffMeshFormerPlayerIsNotTeleportedBySwitchAndRecoversLater()
        {
            Place(members[0], new Vector3(50f, 0.05f, 50f));
            Vector3 before = members[0].transform.position;
            Assert.That(squad.SwitchToMember(1), Is.True);
            Assert.That(Vector3.Distance(before, members[0].transform.position), Is.LessThan(0.0001f));
            yield return Frames(60);
            Assert.That(members[0].Companion.RecoveryCount, Is.Zero);
            yield return Frames(90);
            Assert.That(members[0].Companion.RecoveryCount, Is.EqualTo(1));
            Assert.That(members[0].GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatedCombatSwitchesKeepExactlyOneControllerAndSquadAlive()
        {
            Place(members[0], new Vector3(-11f, 0.05f, 6f));
            // Start the whole squad in the encounter. Switching immediately to a companion
            // still at spawn otherwise sends the squad into recall instead of combat.
            Assert.That(members[1].GetComponent<NavMeshAgent>().Warp(new Vector3(-12.6f, 0.05f, 3.8f)), Is.True);
            Assert.That(members[2].GetComponent<NavMeshAgent>().Warp(new Vector3(-9.1f, 0.05f, 3f)), Is.True);
            Physics.SyncTransforms();
            foreach (var enemy in enemies) enemy.gameObject.SetActive(true);
            for (int i = 0; i < 18; i++)
            {
                int index = (i + 1) % 3;
                foreach (var member in members) member.Health.Heal(100f);
                Assert.That(squad.SwitchToMember(index), Is.True);
                MouseButtons(attack: true);
                yield return Frames(1);
                MouseButtons();
                yield return Frames(20);
                AssertOwnership(index);
            }
            Assert.That(enemies.Sum(e => e.GetComponent<Health>().CurrentHealth), Is.LessThan(300f));
        }

        private void PrepareAIShot()
        {
            foreach (var member in members) member.Receiver.GrantInvulnerability(0f, 100f);
            Set(members[0].Companion, "enemyMask", (LayerMask)0);
            Set(members[1].Companion, "enemyMask", (LayerMask)0);
            Place(members[0], new Vector3(8f, 0.05f, -12f));
            members[2].GetComponent<NavMeshAgent>().Warp(new Vector3(10f, 0.05f, -14f));
            members[2].transform.rotation = Quaternion.identity;
            enemies[0].gameObject.SetActive(true);
            enemies[0].GetComponent<NavMeshAgent>().Warp(new Vector3(10f, 0.05f, -9f));
            enemies[0].GetComponent<NavMeshAgent>().speed = 0f;
            Physics.SyncTransforms();
        }

        private void AssertOwnership(int index)
        {
            Assert.That(squad.ActiveMember, Is.EqualTo(members[index]));
            for (int i = 0; i < 3; i++)
            {
                bool controlled = i == index;
                Assert.That(members[i].IsPlayerControlled, Is.EqualTo(controlled));
                Assert.That(members[i].GetComponent<ThirdPersonMotor>().enabled, Is.EqualTo(controlled));
                Assert.That(members[i].Companion.enabled, Is.EqualTo(!controlled && !members[i].Health.IsDead));
                if (controlled) Assert.That(members[i].GetComponent<NavMeshAgent>().enabled, Is.False);
            }
        }

        private static Health Victim(Vector3 position)
        {
            var go = new GameObject("Switch Test Victim");
            go.layer = LayerMask.NameToLayer("Damageable");
            go.transform.position = position;
            var body = go.AddComponent<CapsuleCollider>();
            body.center = Vector3.up * 0.9f;
            body.height = 1.8f;
            body.radius = 0.4f;
            Physics.SyncTransforms();
            return go.AddComponent<Health>();
        }

        private static void Place(SquadMember member, Vector3 position)
        {
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false;
            body.enabled = false;
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true;
            motor.enabled = member.IsPlayerControlled;
            Physics.SyncTransforms();
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private void MouseButtons(bool attack = false, bool dodge = false) => InputSystem.QueueStateEvent(mouse,
            new MouseState().WithButton(MouseButton.Left, attack).WithButton(MouseButton.Right, dodge));
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    }
}
