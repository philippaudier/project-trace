// Copy into isolated PlayMode tests; run with graphics and TRACE_CAPTURE_OUTPUT set.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
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
    public sealed class SupportVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureBodyTrioShieldAndFirstTrace()
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
                var support = squad.Members[2];
                var member = support.transform;
                var camera = UnityEngine.Camera.main;
                var brain = camera.GetComponent<CinemachineBrain>();
                yield return new WaitForSecondsRealtime(0.3f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
                yield return new WaitForSecondsRealtime(0.1f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.8f);
                brain.enabled = false;
                Look(camera, member.position + new Vector3(1.6f, 1.4f, 2.4f), member.position + Vector3.up * 1.05f);
                yield return Capture("Support-front.png");
                Look(camera, member.position + new Vector3(-2.2f, 1.5f, -1.7f), member.position + Vector3.up * 1.0f);
                yield return Capture("Support-back.png");
                // The three members side by side.
                Vector3 centre = (squad.Members[0].transform.position + squad.Members[1].transform.position + member.position) / 3f;
                Look(camera, centre + new Vector3(0.3f, 1.7f, 5.4f), centre + Vector3.up * 0.95f);
                yield return Capture("Support-trio.png");
                // Pulse Shield deployed: mint halos on everyone, the back module lit.
                Assert.That(support.GetComponent<PulseShield>().Activate(), Is.True);
                yield return new WaitForSecondsRealtime(0.6f);
                Look(camera, centre + new Vector3(-2.2f, 2.0f, 4.4f), centre + Vector3.up * 1.0f);
                yield return Capture("Support-shield.png");
                brain.enabled = true;

                yield return SceneManager.LoadSceneAsync("FirstTrace");
                squad = Object.FindFirstObjectByType<SquadController>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("Support-firsttrace.png");
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
