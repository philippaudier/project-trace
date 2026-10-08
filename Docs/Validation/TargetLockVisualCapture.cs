// Copy into isolated PlayMode tests; run with graphics and TRACE_CAPTURE_OUTPUT set.
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
    public sealed class TargetLockVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureLockCameraAndFocus()
        {
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            float oldFixed = Time.fixedDeltaTime;
            TacticalFocus focus = null;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype");
                var squad = Object.FindFirstObjectByType<SquadController>();
                var targeting = squad.GetComponent<TargetingSystem>();
                focus = squad.GetComponent<TacticalFocus>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                var members = squad.Members.ToArray();
                var enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
                foreach (var enemy in enemies) enemy.enabled = false;
                yield return new WaitForSecondsRealtime(0.3f);
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                Vector3 origin = members[0].transform.position;
                Vector3[] spots = { origin + new Vector3(-2.5f, 0f, 6f), origin + new Vector3(0.5f, 0f, 7f), origin + new Vector3(3.5f, 0f, 5.5f), origin + new Vector3(1.5f, 0f, 10f) };
                for (int i = 0; i < enemies.Length; i++) enemies[i].GetComponent<NavMeshAgent>().Warp(spots[i]);
                yield return new WaitForSecondsRealtime(0.3f);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(targeting.IsLocked, Is.True);
                yield return Capture("TargetLock-locked.png");

                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 1f) });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                yield return new WaitForSecondsRealtime(1.2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(targeting.IsLocked, Is.True);
                yield return Capture("TargetLock-strafe.png");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return new WaitForSecondsRealtime(0.35f);
                Assert.That(focus.IsActive, Is.True);
                yield return Capture("TargetLock-focus.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.25f);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (focus != null) focus.enabled = false;
                Time.timeScale = 1f; Time.fixedDeltaTime = oldFixed;
                InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            }
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
