// Copy into isolated PlayMode tests; run with graphics and TRACE_CAPTURE_OUTPUT set.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Input;
using TRACE.Narrative;
using TRACE.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class HudVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureExplorationCombatFocusAndInteraction()
        {
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype");
                var squad = Object.FindFirstObjectByType<SquadController>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                var enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
                foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
                var leader = squad.Members[0].transform;
                // Clear of the camera test corner behind the spawn so the orbit camera can frame the squad.
                var leaderBody = leader.GetComponent<CharacterController>(); leaderBody.enabled = false;
                leader.position = new Vector3(-11f, 0.08f, 12f); leaderBody.enabled = true; Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Capture("Hud-exploration.png");
                // Each member active: operator portrait, squad minis, accents.
                foreach (var key in new[] { Key.Digit2, Key.Digit3, Key.Digit1 })
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return new WaitForSecondsRealtime(0.1f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Capture($"Hud-portraits-{squad.ActiveMember.GetComponent<TRACE.Characters.CharacterProfile>().DisplayName}.png");
                }
                yield return Capture("Hud-portraits-1600x900.png", 1600, 900);
                yield return Capture("Hud-portraits-1920x1080.png", 1920, 1080);

                // Two hostiles ahead, one locked: combat HUD with the target panel.
                for (int i = 0; i < 2; i++)
                {
                    enemies[i].transform.position = leader.position + leader.forward * (5f + i * 1.5f) + leader.right * (i == 0 ? -1.2f : 1.6f);
                    enemies[i].gameObject.SetActive(true);
                }
                Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(0.8f);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle));
                yield return new WaitForSecondsRealtime(0.1f);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture("Hud-combat-lock.png");
                var eliteField = typeof(EnemyBrain).GetField("elite", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var enemy in enemies) eliteField.SetValue(enemy, true);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("Hud-target-elite.png");
                foreach (var enemy in enemies) eliteField.SetValue(enemy, false);
                yield return new WaitForSecondsRealtime(0.3f);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Capture("Hud-focus.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.5f);

                yield return SceneManager.LoadSceneAsync("FirstTrace");
                squad = Object.FindFirstObjectByType<SquadController>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                var dialogue = Object.FindFirstObjectByType<DialogueRunner>();
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Capture("Hud-firsttrace.png");
                float deadline = Time.unscaledTime + 10f;
                while (dialogue.IsPlaying && Time.unscaledTime < deadline) yield return null;
                var terminal = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.Prompt.Contains("terminal"));
                var member = squad.Members[0];
                var body = member.GetComponent<CharacterController>(); body.enabled = false;
                member.transform.SetPositionAndRotation(terminal.transform.position + Vector3.left * 1.3f + Vector3.up * 0.08f, Quaternion.Euler(0f, 90f, 0f));
                body.enabled = true; Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Capture("Hud-interact.png");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = 1f;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            }
        }

        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static IEnumerator Capture(string name, int width = 1280, int height = 720)
        {
            string output = Environment.GetEnvironmentVariable("TRACE_CAPTURE_OUTPUT");
            Directory.CreateDirectory(output);
            var camera = UnityEngine.Camera.main;
            var target = new RenderTexture(width, height, 24);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            camera.targetTexture = target;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Object.Destroy(pixels); Object.Destroy(target);
        }
    }
}
