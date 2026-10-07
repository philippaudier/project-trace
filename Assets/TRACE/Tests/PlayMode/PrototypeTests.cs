using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TRACE.Characters;
using TRACE.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class PrototypeTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private ThirdPersonMotor motor;
        private CharacterController controller;
        private TracePlayerInput input;
        private UnityEngine.Camera camera;
        private float previousCaptureDelta;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Prototype");
            motor = Object.FindFirstObjectByType<ThirdPersonMotor>();
            controller = motor.GetComponent<CharacterController>();
            input = motor.GetComponent<TracePlayerInput>();
            // Tests also work in the interactive Test Runner without Game-view cursor capture.
            typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(input, false);
            camera = UnityEngine.Camera.main;
            yield return Frames(30);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            Time.captureDeltaTime = previousCaptureDelta;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WalkSprintDecelerateAndDisableInput()
        {
            Teleport(new Vector3(12f, 0.05f, -10f));
            Keys(Key.W);
            yield return Frames(60);
            float walkSpeed = motor.PlanarVelocity.magnitude;
            Assert.That(walkSpeed, Is.InRange(3f, 3.4f));
            Keys(Key.W, Key.LeftShift);
            yield return Frames(60);
            Assert.That(motor.PlanarVelocity.magnitude, Is.GreaterThan(walkSpeed * 1.7f));
            Assert.That(motor.transform.position.z, Is.GreaterThan(-3f));
            input.enabled = false;
            yield return Frames(30);
            Assert.That(motor.PlanarVelocity.magnitude, Is.LessThan(0.01f));
            Keys();
            input.enabled = true;
            yield return Frames(5);
            Keys(Key.D);
            yield return Frames(30);
            Assert.That(motor.PlanarVelocity.x, Is.GreaterThan(2.8f));
            Keys();
            yield return Frames(30);
            Assert.That(motor.PlanarVelocity.magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator DiagonalDoesNotMoveFasterAndRotationFollows()
        {
            Teleport(new Vector3(10f, 0.05f, -10f));
            Keys(Key.W, Key.D);
            yield return Frames(60);
            Assert.That(motor.PlanarVelocity.magnitude, Is.InRange(3f, 3.4f));
            Assert.That(Mathf.DeltaAngle(motor.transform.eulerAngles.y, 45f), Is.InRange(-2f, 2f));
        }

        [UnityTest]
        public IEnumerator MouseOrbitPitchClampAndCameraRelativeMovement()
        {
            Teleport(new Vector3(10f, 0.05f, -10f));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(750f, 0f) });
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Mathf.DeltaAngle(camera.transform.eulerAngles.y, 90f), Is.InRange(-3f, 3f));
            Vector3 start = motor.transform.position;
            Keys(Key.W);
            yield return Frames(45);
            Assert.That(motor.transform.position.x - start.x, Is.GreaterThan(1.5f));
            Assert.That(Mathf.Abs(motor.transform.position.z - start.z), Is.LessThan(0.2f));
            Keys();
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0f, -10000f) });
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x), Is.InRange(64f, 66f));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0f, 10000f) });
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x), Is.InRange(-31f, -29f));
        }

        [UnityTest]
        public IEnumerator ClimbRampReachPlatformAndDescendGrounded()
        {
            Keys(Key.W);
            for (int i = 0; i < 360 && motor.transform.position.z < 10.5f; i++)
                yield return null;
            Keys();
            yield return Frames(20);
            Assert.That(motor.transform.position.y, Is.GreaterThan(1.9f));
            Assert.That(motor.transform.position.z, Is.GreaterThan(10f));
            Assert.That(motor.IsGrounded, Is.True);
            Keys(Key.S);
            int groundedFrames = 0;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                if (motor.IsGrounded) groundedFrames++;
            }
            Assert.That(motor.transform.position.y, Is.LessThan(1.3f));
            Assert.That(groundedFrames, Is.GreaterThan(100));
        }

        [UnityTest]
        public IEnumerator GravityLandsAndSmallStepsAreClimbable()
        {
            Teleport(new Vector3(12f, 4f, 0f));
            yield return Frames(100);
            Assert.That(motor.transform.position.y, Is.InRange(-0.1f, 0.1f));
            Assert.That(motor.IsGrounded, Is.True);
            Teleport(new Vector3(7f, 0.05f, 1f));
            Keys(Key.W);
            for (int i = 0; i < 180 && motor.transform.position.z < 5.8f; i++)
                yield return null;
            Keys();
            yield return Frames(15);
            Assert.That(motor.transform.position.y, Is.GreaterThan(0.65f), "Step position: " + motor.transform.position);
        }

        [UnityTest]
        public IEnumerator WallsBlockPlayerAndCameraRecoversAfterOcclusion()
        {
            Teleport(new Vector3(-6f, 0.05f, 0f));
            yield return Frames(60);
            var follow = Object.FindFirstObjectByType<CinemachineThirdPersonFollow>();
            Transform target = Object.FindFirstObjectByType<CinemachineCamera>().Follow;
            Assert.That(Vector3.Distance(camera.transform.position, target.position), Is.LessThan(5f));
            Assert.That(Physics.Linecast(target.position, camera.transform.position, 1), Is.False);
            Assert.That(Physics.CheckSphere(camera.transform.position, 0.2f, 1), Is.False);
            Keys(Key.S);
            yield return Frames(100);
            Assert.That(motor.transform.position.z, Is.GreaterThan(-3.5f));
            Keys();
            Teleport(new Vector3(10f, 0.05f, 0f));
            yield return Frames(150);
            Assert.That(Vector3.Distance(camera.transform.position, target.position), Is.GreaterThan(follow.CameraDistance - 0.3f));
        }

        [UnityTest]
        public IEnumerator SteepSlopeDoesNotAllowClimbing()
        {
            var ramp = GameObject.Find("Ramp 20 degrees").transform;
            ramp.SetPositionAndRotation(new Vector3(0f, 2.498f, 7f), Quaternion.Euler(-60f, 0f, 0f));
            Physics.SyncTransforms();
            Keys(Key.W);
            yield return Frames(200);
            Assert.That(motor.transform.position.y, Is.LessThan(0.6f));
            Assert.That(motor.transform.position.z, Is.LessThan(6f));
        }

        [UnityTest]
        public IEnumerator SceneReloadRetainsWiringAndInput()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return Frames(30);
            var reloaded = Object.FindFirstObjectByType<ThirdPersonMotor>();
            var reader = reloaded.GetComponent<TracePlayerInput>();
            typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(reader, false);
            Keys(Key.W);
            yield return Frames(45);
            Assert.That(reloaded.transform.position.z, Is.GreaterThan(1.5f));
            Assert.That(Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        private void Teleport(Vector3 position)
        {
            motor.enabled = false;
            controller.enabled = false;
            motor.transform.position = position;
            controller.enabled = true;
            motor.enabled = true;
            Physics.SyncTransforms();
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
