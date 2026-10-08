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
    public sealed class ComboVisualCapture
    {
        [UnityTest]
        public IEnumerator CapturePreparationDashAndShieldCounter()
        {
            float oldCapture = Time.captureDeltaTime;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 60f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
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
                Assert.That(squad.SwitchToMember(1), Is.True);
                Place(members[1], new Vector3(-11f, 0.08f, 5f));
                yield return SettleCamera();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Frames(45);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(enemies.Any(e => e.GetComponent<ComboOpportunity>().Type == ComboOpportunityType.Grouped), Is.True);
                Capture("Combo-grouped-ready.png");

                Assert.That(squad.SwitchToMember(0), Is.True);
                Place(members[0], new Vector3(-11f, 0.08f, 6f));
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                yield return SettleCamera();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Frames(18);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(enemies.Any(e => e.GetComponent<ComboOpportunity>().ConsumptionCount == 1), Is.True);
                Capture("Combo-dash-impact.png");

                Assert.That(squad.SwitchToMember(2), Is.True);
                Place(members[2], new Vector3(-11f, 0.08f, 6f));
                yield return SettleCamera();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Frames(2);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                foreach (var enemy in enemies) enemy.enabled = true;
                for (int i = 0; i < 150 && !members.Any(m => m.GetComponent<ComboOpportunity>().Type == ComboOpportunityType.Protected); i++)
                    yield return null;
                int bearer = Array.FindIndex(members, m => m.GetComponent<ComboOpportunity>().Type == ComboOpportunityType.Protected);
                Assert.That(bearer, Is.GreaterThanOrEqualTo(0), "Enemy hits must prepare a shield counter.");
                Assert.That(squad.ActiveMember, Is.EqualTo(members[2]), "Preparation never forces a switch.");
                foreach (var enemy in enemies) enemy.enabled = false;
                if (bearer != 2) Assert.That(squad.SwitchToMember(bearer), Is.True);
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                var opportunity = members[bearer].GetComponent<ComboOpportunity>();
                var victim = enemies.First(e => !e.GetComponent<Health>().IsDead);
                Vector3 destination = members[bearer].transform.position + Vector3.forward * 1.25f;
                victim.GetComponent<NavMeshAgent>().Warp(destination);
                members[bearer].transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                yield return SettleCamera();
                Capture("Combo-protected-ready.png");
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                for (int i = 0; i < 60 && opportunity.ConsumptionCount == 0; i++) yield return null;
                Assert.That(opportunity.ConsumptionCount, Is.EqualTo(1));
                yield return Frames(3);
                Capture("Combo-counter-impact.png");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                Time.timeScale = 1f;
                Time.captureDeltaTime = oldCapture;
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            }
        }

        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private static IEnumerator SettleCamera()
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.3f);
            Time.timeScale = 1f;
        }
        private static void Place(SquadMember member, Vector3 position)
        {
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false; body.enabled = false;
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true; motor.enabled = true;
            Physics.SyncTransforms();
        }
        private static void Capture(string name)
        {
            string output = Environment.GetEnvironmentVariable("TRACE_CAPTURE_OUTPUT") ?? Path.Combine(Application.dataPath, "../Docs/Validation");
            Directory.CreateDirectory(output);
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
            File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            Object.Destroy(pixels);
            Object.Destroy(target);
        }
    }
}
