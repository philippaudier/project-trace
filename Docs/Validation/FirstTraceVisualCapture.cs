// Copy into isolated PlayMode tests; run with graphics and TRACE_CAPTURE_OUTPUT set.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Input;
using TRACE.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class FirstTraceVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureDockClueHallAndArena()
        {
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return SceneManager.LoadSceneAsync("FirstTrace");
                var squad = Object.FindFirstObjectByType<SquadController>();
                var story = Object.FindFirstObjectByType<StoryDirector>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                var members = squad.Members.ToArray();
                yield return new WaitForSecondsRealtime(4.6f);
                yield return Capture("FirstTrace-dock.png");

                var terminal = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.Prompt.Contains("terminal"));
                Place(members[0], terminal.transform.position + Vector3.left * 1.3f, Quaternion.Euler(0f, 90f, 0f));
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("FirstTrace-terminal.png");
                // Let the arrival exchange finish: the prompt is suppressed while a line is on screen.
                var dialogue = Object.FindFirstObjectByType<DialogueRunner>();
                float deadline = Time.unscaledTime + 8f;
                while (dialogue.IsPlaying && Time.unscaledTime < deadline) yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Clue));
                yield return Capture("FirstTrace-archive.png");

                Place(members[0], Get<Transform>(story, "hallZone").position, Quaternion.identity);
                yield return new WaitForSecondsRealtime(1.6f);
                Assert.That(story.EventTriggered, Is.True);
                yield return Capture("FirstTrace-hall-event.png");

                Place(members[0], Get<Transform>(story, "tensionZone").position, Quaternion.identity);
                yield return new WaitForSecondsRealtime(1.2f);
                Place(members[0], Get<Transform>(story, "arenaZone").position + Vector3.forward * 6f, Quaternion.identity);
                yield return new WaitForSecondsRealtime(4.5f);
                yield return Capture("FirstTrace-arena.png");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = 1f;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            }
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Place(SquadMember member, Vector3 position, Quaternion rotation)
        {
            var body = member.GetComponent<CharacterController>(); body.enabled = false;
            member.transform.SetPositionAndRotation(position + Vector3.up * 0.08f, rotation); body.enabled = true;
            Physics.SyncTransforms();
        }
        private static IEnumerator Capture(string name)
        {
            string output = Environment.GetEnvironmentVariable("TRACE_CAPTURE_OUTPUT");
            Directory.CreateDirectory(output);
            var camera = UnityEngine.Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            camera.targetTexture = target;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Object.Destroy(pixels); Object.Destroy(target);
        }
    }
}
