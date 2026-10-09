using System;
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
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class TargetLockTests
    {
        private SquadController squad;
        private TargetingSystem targeting;
        private TracePlayerInput input;
        private TacticalFocus focus;
        private ThirdPersonOrbit orbit;
        private SquadMember[] members;
        private BasicMeleeEnemy[] enemies;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private GameObject wall;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        // Player at (-11, 0, 3) facing +z; the camera looks along +z from behind and above.
        private static readonly Vector3 Ahead = new Vector3(-11f, 0f, 9f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            yield return SceneManager.LoadSceneAsync("Prototype");
            squad = Object.FindFirstObjectByType<SquadController>();
            targeting = squad.GetComponent<TargetingSystem>();
            input = squad.GetComponent<TracePlayerInput>();
            focus = squad.GetComponent<TacticalFocus>();
            orbit = Object.FindFirstObjectByType<ThirdPersonOrbit>();
            members = squad.Members.ToArray();
            Set(input, "captureCursor", false);
            enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            FrameCamera();
            yield return Frames(10);
            FreezeCompanions();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (wall != null) Object.Destroy(wall);
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f;
            Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PressLocksTheScreenCenteredTargetNotTheNearestOne()
        {
            var near = EnemyAt(0, new Vector3(-13.6f, 0f, 5.5f));
            var centered = EnemyAt(1, Ahead);
            Assert.That(Vector3.Distance(members[0].transform.position, near.transform.position),
                Is.LessThan(Vector3.Distance(members[0].transform.position, centered.transform.position)));
            yield return PressLock();
            Assert.That(targeting.IsLocked, Is.True);
            Assert.That(targeting.LockedTarget, Is.EqualTo(centered.GetComponent<Health>()));
            Assert.That(targeting.LastCandidates.Count, Is.EqualTo(2));
            Assert.That(targeting.LastCandidates.First(c => c.Health == centered.GetComponent<Health>()).Score,
                Is.LessThan(targeting.LastCandidates.First(c => c.Health == near.GetComponent<Health>()).Score));
            Assert.That(Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.name == "Outer Diamond"), Is.True);
        }

        [UnityTest]
        public IEnumerator SecondPressReleasesAndSoftTargetingResumes()
        {
            var centered = EnemyAt(0, Ahead);
            yield return PressLock();
            Assert.That(targeting.IsLocked, Is.True);
            yield return PressLock();
            Assert.That(targeting.IsLocked, Is.False);
            Assert.That(targeting.LastReleaseReason, Is.EqualTo("toggle"));
            Assert.That(Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.name == "Outer Diamond"), Is.False);
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().FacingTarget, Is.Null);
            PlaceActive(Ahead + Vector3.back * 1.4f, Quaternion.identity);
            yield return Strike();
            Assert.That(centered.GetComponent<Health>().CurrentHealth, Is.EqualTo(75f), "soft targeting still hits the enemy ahead");
        }

        [UnityTest]
        public IEnumerator DeathDeactivationAndRangeReleaseTheLock()
        {
            var a = EnemyAt(0, Ahead);
            var b = EnemyAt(1, Ahead + Vector3.right * 2f);
            yield return PressLock();
            Assert.That(targeting.LockedTarget, Is.EqualTo(a.GetComponent<Health>()));
            a.GetComponent<Health>().TakeDamage(9999f);
            yield return Frames(2);
            Assert.That(targeting.IsLocked, Is.False);
            Assert.That(targeting.LastReleaseReason, Is.EqualTo("invalid"));
            Assert.That(targeting.LockTarget(b.GetComponent<Health>()), Is.True);
            b.gameObject.SetActive(false);
            yield return Frames(2);
            Assert.That(targeting.IsLocked, Is.False);
            b.gameObject.SetActive(true);
            Assert.That(targeting.LockTarget(b.GetComponent<Health>()), Is.True);
            Assert.That(b.GetComponent<NavMeshAgent>().Warp(new Vector3(10f, 0f, 19f)), Is.True);
            yield return Frames(2);
            Assert.That(targeting.IsLocked, Is.False);
            Assert.That(targeting.LastReleaseReason, Is.EqualTo("range"));
            yield return PressLock();
            Assert.That(targeting.IsLocked, Is.False, "nothing within MaxLockDistance");
        }

        [UnityTest]
        public IEnumerator HiddenTargetsAreNotAcquiredAndOcclusionIsForgivenOnlyBriefly()
        {
            var hidden = EnemyAt(0, Ahead);
            var side = EnemyAt(1, Ahead + Vector3.right * 2.5f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Ahead + Vector3.back * 2f + Vector3.up * 1.5f;
            wall.transform.localScale = new Vector3(1.6f, 3f, 0.3f);
            Physics.SyncTransforms();
            yield return PressLock();
            Assert.That(targeting.LockedTarget, Is.EqualTo(side.GetComponent<Health>()));
            wall.transform.position = side.transform.position + Vector3.back * 2f + Vector3.up * 1.5f;
            Physics.SyncTransforms();
            yield return Wait(0.6f);
            Assert.That(targeting.IsLocked, Is.True, "short occlusion is tolerated");
            Assert.That(targeting.LostSightTime, Is.GreaterThan(0.4f));
            yield return Wait(1.0f);
            Assert.That(targeting.IsLocked, Is.False);
            Assert.That(targeting.LastReleaseReason, Is.EqualTo("sight"));
            Assert.That(hidden.GetComponent<Health>().IsDead, Is.False);
        }

        [UnityTest]
        public IEnumerator WheelAndStickFlickPickTheScreenNeighbourOnTheRequestedSide()
        {
            var left = EnemyAt(0, Ahead + Vector3.left * 1.6f);
            var center = EnemyAt(1, Ahead);
            var right = EnemyAt(2, Ahead + Vector3.right * 1.6f);
            yield return PressLock();
            Assert.That(targeting.LockedTarget, Is.EqualTo(center.GetComponent<Health>()));
            yield return Wheel(1f);
            Assert.That(targeting.LockedTarget, Is.EqualTo(right.GetComponent<Health>()));
            yield return Wheel(1f);
            Assert.That(targeting.LockedTarget, Is.EqualTo(right.GetComponent<Health>()), "no wrap past the rightmost target");
            yield return Wheel(-1f);
            Assert.That(targeting.LockedTarget, Is.EqualTo(center.GetComponent<Health>()));
            yield return Wheel(-1f);
            Assert.That(targeting.LockedTarget, Is.EqualTo(left.GetComponent<Health>()));
            // Controller: one impulse per switch, re-armed below the threshold, throttled by a cooldown.
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(0.9f, 0f) });
            yield return Frames(3);
            Assert.That(targeting.LockedTarget, Is.EqualTo(center.GetComponent<Health>()));
            yield return RealTime(0.5f);
            Assert.That(targeting.LockedTarget, Is.EqualTo(center.GetComponent<Health>()), "holding the stick does not keep switching");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(0.2f, 0f) });
            yield return Frames(2);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(0.9f, 0f) });
            yield return Frames(3);
            Assert.That(targeting.LockedTarget, Is.EqualTo(right.GetComponent<Health>()));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(0.2f, 0f) });
            yield return Frames(2);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(-0.9f, 0f) });
            yield return Frames(3);
            Assert.That(targeting.LockedTarget, Is.EqualTo(right.GetComponent<Health>()), "second impulse inside the cooldown is ignored");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return Frames(2);
            Assert.That(targeting.SwitchCount, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator MeleeStrikesTheLockedTargetInsteadOfTheSoftOne()
        {
            PlaceActive(Ahead + Vector3.back * 1.4f, Quaternion.identity);
            var soft = EnemyAt(0, Ahead);
            Vector3 lockedPosition = members[0].transform.position + Quaternion.Euler(0f, 50f, 0f) * Vector3.forward * 1.5f;
            var locked = EnemyAt(1, lockedPosition);
            Assert.That(targeting.LockTarget(locked.GetComponent<Health>()), Is.True);
            targeting.Unlock();
            yield return Strike();
            Assert.That(soft.GetComponent<Health>().CurrentHealth, Is.EqualTo(75f), "without lock the swing follows the soft target");
            Assert.That(locked.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(targeting.LockTarget(locked.GetComponent<Health>()), Is.True);
            members[0].GetComponent<ThirdPersonMotor>().FacingTarget = null;
            yield return Strike();
            Assert.That(locked.GetComponent<Health>().CurrentHealth, Is.EqualTo(75f));
            Assert.That(soft.GetComponent<Health>().CurrentHealth, Is.EqualTo(75f), "the soft target is no longer hit");
            Assert.That(members[0].GetComponent<PlayerMeleeAttack>().CurrentTarget, Is.EqualTo(locked.GetComponent<Health>()));
        }

        [UnityTest]
        public IEnumerator DashStrikeAndGravityFieldGoToTheLockedTarget()
        {
            var soft = EnemyAt(0, Ahead + Vector3.back * 1f);
            var locked = EnemyAt(1, members[0].transform.position + Quaternion.Euler(0f, -40f, 0f) * Vector3.forward * 5f);
            Assert.That(targeting.LockTarget(locked.GetComponent<Health>()), Is.True);
            Assert.That(members[0].GetComponent<CharacterSkill>().Activate(), Is.True);
            yield return Wait(0.5f);
            Assert.That(locked.GetComponent<Health>().CurrentHealth, Is.EqualTo(65f));
            Assert.That(soft.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(squad.SwitchToMember(1), Is.True);
            FreezeCompanions();
            yield return Frames(2);
            // Bring Control inside its 6 m cast range; beyond it the skill correctly falls back to soft targeting.
            PlaceActive(new Vector3(-11f, 0.08f, 3f), Quaternion.identity);
            yield return Frames(2);
            var field = members[1].GetComponent<GravityFieldSkill>().Field;
            Assert.That(members[1].GetComponent<CharacterSkill>().Activate(), Is.True);
            Assert.That(Vector3.Distance(field.transform.position, locked.transform.position), Is.LessThan(1.5f));
        }

        [UnityTest]
        public IEnumerator RangedMemberShootsTheLockedTargetOutsideItsAimCone()
        {
            var soft = EnemyAt(0, Ahead);
            Assert.That(squad.SwitchToMember(2), Is.True);
            FreezeCompanions();
            yield return Frames(2);
            PlaceActive(Ahead + Vector3.back * 4f, Quaternion.identity);
            var locked = EnemyAt(1, members[2].transform.position + Quaternion.Euler(0f, -65f, 0f) * Vector3.forward * 4f);
            Assert.That(targeting.LockTarget(locked.GetComponent<Health>()), Is.True);
            members[2].GetComponent<ThirdPersonMotor>().FacingTarget = null;
            yield return Strike();
            yield return Wait(0.3f);
            Assert.That(locked.GetComponent<Health>().CurrentHealth, Is.EqualTo(92f));
            Assert.That(soft.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(members[2].GetComponent<PlayerRangedAttack>().CurrentTarget, Is.EqualTo(locked.GetComponent<Health>()));
        }

        [UnityTest]
        public IEnumerator SquadSwitchKeepsTheLockAndMovesTheFacingOverride()
        {
            var target = EnemyAt(0, Ahead);
            yield return PressLock();
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().FacingTarget, Is.EqualTo(target.transform));
            Assert.That(squad.SwitchToMember(1), Is.True);
            yield return Frames(2);
            Assert.That(targeting.LockedTarget, Is.EqualTo(target.GetComponent<Health>()));
            Assert.That(members[1].GetComponent<ThirdPersonMotor>().FacingTarget, Is.EqualTo(target.transform));
            Assert.That(members[0].GetComponent<ThirdPersonMotor>().FacingTarget, Is.Null);
            Assert.That(orbit.FollowedCharacter, Is.EqualTo(members[1].transform));
        }

        [UnityTest]
        public IEnumerator TacticalFocusAllowsLockSwitchUnlockAndTagsTheCard()
        {
            var center = EnemyAt(0, Ahead);
            var right = EnemyAt(1, Ahead + Vector3.right * 3f);
            var camera = UnityEngine.Camera.main;
            camera.transform.position = new Vector3(-8.5f, 4.5f, -0.5f);
            camera.transform.LookAt(new Vector3(-11f, 0f, 7f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return RealTime(0.3f);
            Assert.That(focus.IsActive, Is.True);
            yield return PressLock();
            Assert.That(targeting.LockedTarget, Is.EqualTo(center.GetComponent<Health>()));
            var overlay = squad.GetComponent<TacticalOverlay>();
            yield return RealTime(0.15f);
            Assert.That(overlay.IsVisible, Is.True);
            StringAssert.Contains("LOCK", overlay.EnemyInfo(0));
            StringAssert.DoesNotContain("LOCK", overlay.EnemyInfo(1));
            yield return Wheel(1f);
            Assert.That(targeting.LockedTarget, Is.EqualTo(right.GetComponent<Health>()));
            yield return RealTime(0.15f);
            StringAssert.Contains("LOCK", overlay.EnemyInfo(1));
            yield return PressLock();
            Assert.That(targeting.IsLocked, Is.False);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(Time.timeScale, Is.LessThan(0.2f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return RealTime(0.3f);
        }

        [UnityTest]
        public IEnumerator CameraDriftsTowardTheLockedTargetButLookInputWins()
        {
            var target = EnemyAt(0, members[0].transform.position + Quaternion.Euler(0f, -60f, 0f) * Vector3.forward * 6f);
            float before = Get<float>(orbit, "yaw");
            Assert.That(targeting.LockTarget(target.GetComponent<Health>()), Is.True);
            yield return RealTime(0.4f);
            Assert.That(orbit.IsAssisting, Is.True);
            float drifted = Get<float>(orbit, "yaw");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before, drifted)), Is.GreaterThan(8f).And.LessThan(59f), "gentle drift, no snap");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(-100f, 0f) });
            yield return Frames(2);
            Assert.That(Get<float>(orbit, "yaw") - drifted, Is.LessThan(-10f), "player look still applies in full");
            Assert.That(orbit.IsAssisting, Is.False);
            targeting.Unlock();
            yield return RealTime(0.4f);
            Assert.That(orbit.IsAssisting, Is.False);
        }

        [UnityTest]
        public IEnumerator LockedMemberStrafesFacingTheTargetAndDodgesAlongInput()
        {
            var target = EnemyAt(0, Ahead);
            Assert.That(targeting.LockTarget(target.GetComponent<Health>()), Is.True);
            var motor = members[0].GetComponent<ThirdPersonMotor>();
            Vector3 start = members[0].transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return Wait(0.6f);
            Assert.That(members[0].transform.position.x, Is.LessThan(start.x - 0.8f), "strafes left in camera space");
            Vector3 toTarget = target.transform.position - members[0].transform.position;
            toTarget.y = 0f;
            Assert.That(Vector3.Angle(members[0].transform.forward, toTarget), Is.LessThan(12f), "keeps facing the target");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return Frames(2);
            Assert.That(motor.IsDodging, Is.True);
            Assert.That(Vector3.Angle(motor.DodgeDirection, Vector3.left), Is.LessThan(15f), "dodge follows the input, not the target");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(1.2f);
            targeting.Unlock();
            yield return Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return Wait(0.5f);
            Assert.That(Vector3.Angle(members[0].transform.forward, Vector3.left), Is.LessThan(15f), "without lock the body turns into the movement again");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        // ---------------------------------------------------------------- helpers

        private IEnumerator PressLock()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle));
            yield return Frames(2);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frames(2);
        }

        private IEnumerator Wheel(float direction)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, direction) });
            yield return Frames(2);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frames(2);
        }

        private IEnumerator Strike()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frames(2);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Wait(0.5f);
        }

        private static void FrameCamera()
        {
            var camera = UnityEngine.Camera.main;
            camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
            camera.aspect = 16f / 9f;
            camera.transform.SetPositionAndRotation(new Vector3(-11f, 6f, -3f), Quaternion.LookRotation(new Vector3(0f, -3f, 10f)));
        }

        private BasicMeleeEnemy EnemyAt(int index, Vector3 position)
        {
            var enemy = enemies[index];
            enemy.gameObject.SetActive(true);
            enemy.enabled = false;
            var agent = enemy.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(position), Is.True);
            agent.ResetPath();
            agent.isStopped = true;
            Physics.SyncTransforms();
            return enemy;
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

        private void PlaceActive(Vector3 position, Quaternion rotation)
        {
            var member = squad.ActiveMember;
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false; body.enabled = false;
            member.transform.SetPositionAndRotation(position, rotation);
            body.enabled = true; motor.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator RealTime(float seconds) { yield return new WaitForSecondsRealtime(seconds); yield return null; }
        private static IEnumerator Wait(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return null; }
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
