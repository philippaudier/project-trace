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
using TRACE.Skills;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class ControlVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureBodyFieldSquadAndFirstTrace()
        {
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype");
                var squad = Object.FindFirstObjectByType<SquadController>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                foreach (var enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
                var control = squad.Members[1];
                var member = control.transform;
                var camera = UnityEngine.Camera.main;
                var brain = camera.GetComponent<CinemachineBrain>();
                yield return new WaitForSecondsRealtime(0.3f);
                // Switch to Control so she stands still under player control, then frame her.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
                yield return new WaitForSecondsRealtime(0.1f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.8f);
                brain.enabled = false;
                Look(camera, member.position + new Vector3(1.6f, 1.4f, 2.4f), member.position + Vector3.up * 1.05f);
                yield return Capture("Control-front.png");
                Look(camera, member.position + new Vector3(-2.2f, 1.5f, -1.7f), member.position + Vector3.up * 1.0f);
                yield return Capture("Control-back.png");
                // Both squad members side by side for the silhouette check.
                var tracewalker = squad.Members[0].transform;
                Vector3 mid = (member.position + tracewalker.position) * 0.5f;
                Look(camera, mid + new Vector3(0f, 1.6f, 4.6f), mid + Vector3.up * 0.95f);
                yield return Capture("Control-vs-tracewalker.png");
                // Gravity Field deployed: cyan rings and the back module lit.
                Assert.That(control.GetComponent<GravityFieldSkill>().Activate(), Is.True);
                yield return new WaitForSecondsRealtime(0.5f);
                var field = control.GetComponent<GravityFieldSkill>().Field.transform;
                Look(camera, member.position + new Vector3(-2.6f, 2.4f, -2.2f), field.position + Vector3.up * 0.4f);
                yield return Capture("Control-field.png");
                brain.enabled = true;

                yield return SceneManager.LoadSceneAsync("FirstTrace");
                squad = Object.FindFirstObjectByType<SquadController>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("Control-firsttrace.png");
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

        private static void Look(UnityEngine.Camera camera, Vector3 from, Vector3 at)
        {
            camera.transform.position = from;
            camera.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up);
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
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
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Object.Destroy(pixels); Object.Destroy(target);
        }
    }
}
