// Copy into the isolated project's PlayMode tests and run with graphics enabled.
using System;
using System.Collections;
using System.IO;
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
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class SkillsVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureThreeSkillsAndLiveEncounter()
        {
            float oldCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype");
                var squad = Object.FindFirstObjectByType<SquadController>();
                var members = squad.Members.ToArray();
                typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(squad.GetComponent<TracePlayerInput>(), false);
                var enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None);
                foreach (var enemy in enemies) enemy.enabled = false;
                yield return Frames(15);
                Place(members[0], new Vector3(-11f, 0.08f, 5f));
                yield return SettleCamera();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Frames(4);
                Assert.That(members[0].GetComponent<ThirdPersonMotor>().IsSkillDashing, Is.True);
                Capture("Skills-dash.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(16);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
                yield return Frames(1);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Place(members[1], new Vector3(-11f, 0.08f, 6f));
                foreach (var member in members)
                    if (!member.IsPlayerControlled) member.Companion.enabled = false;
                yield return SettleCamera();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Frames(20);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(members[1].GetComponent<GravityFieldSkill>().Field.gameObject.activeSelf, Is.True);
                Capture("Skills-gravity.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
                yield return Frames(1);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Place(members[2], new Vector3(-10f, 0.08f, 5f));
                yield return SettleCamera();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Frames(3);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(members.All(m => m.GetComponent<Shield>().CurrentAmount == 30f), Is.True);
                Capture("Skills-shields.png");
                foreach (var enemy in enemies) enemy.enabled = true;
                foreach (var member in members)
                    if (!member.IsPlayerControlled) member.Companion.enabled = true;
                yield return Frames(110);
                Capture("Skills-live-combat.png");
                Assert.That(members.Any(m => m.GetComponent<Shield>().CurrentAmount < 30f || m.Health.CurrentHealth < 100f), Is.True);
                Assert.That(members.All(m => m.GetComponent<CharacterSkill>().CooldownRemaining > 0f), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                Time.timeScale = 1f;
                Time.captureDeltaTime = oldCapture;
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            }
        }

        private static string Output => Environment.GetEnvironmentVariable("TRACE_CAPTURE_OUTPUT") ?? Path.Combine(Application.dataPath, "../Docs/Validation");
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private static IEnumerator SettleCamera()
        {
            // Camera transitions use wall-clock time. Freeze gameplay so a fast batch renderer
            // cannot consume several seconds of simulated cooldown while framing a capture.
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.3f);
            Time.timeScale = 1f;
        }
        private static void Place(SquadMember member, Vector3 position)
        {
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false;
            body.enabled = false;
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true;
            motor.enabled = true;
            Physics.SyncTransforms();
        }
        private static void Capture(string name)
        {
            Directory.CreateDirectory(Output);
            var camera = UnityEngine.Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, name), pixels.EncodeToPNG());
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            Object.Destroy(pixels);
            Object.Destroy(target);
        }
    }
}
