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
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class TracewalkerVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureBodyFocusSquadAndFirstTrace()
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
                var member = squad.Members[0].transform;
                var camera = UnityEngine.Camera.main;
                var brain = camera.GetComponent<CinemachineBrain>();
                yield return new WaitForSecondsRealtime(0.3f);
                brain.enabled = false;
                Look(camera, member.position + new Vector3(1.5f, 1.4f, 2.3f), member.position + Vector3.up * 1.0f);
                yield return Capture("Tracewalker-front.png");
                Look(camera, member.position + new Vector3(-2.2f, 1.5f, -1.6f), member.position + Vector3.up * 1.0f);
                yield return Capture("Tracewalker-back.png");
                Look(camera, member.position + new Vector3(0.9f, 1.35f, 1.3f), member.position + Vector3.up * 1.25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return new WaitForSecondsRealtime(0.45f);
                Assert.That(member.GetComponent<TraceModule>().IsFocused, Is.True);
                yield return Capture("Tracewalker-focus.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.4f);
                // Clear of the camera test corner (x = -11, z in [-4, 2]) so the orbit camera can sit behind the squad.
                Place(squad.Members[0], new Vector3(-11f, 0f, 12f), Quaternion.identity);
                brain.enabled = true;
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("Tracewalker-squad.png");

                yield return SceneManager.LoadSceneAsync("FirstTrace");
                squad = Object.FindFirstObjectByType<SquadController>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("Tracewalker-firsttrace.png");
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
        private static void Place(SquadMember member, Vector3 position, Quaternion rotation)
        {
            var body = member.GetComponent<CharacterController>(); body.enabled = false;
            member.transform.SetPositionAndRotation(position + Vector3.up * 0.08f, rotation); body.enabled = true;
            Physics.SyncTransforms();
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
