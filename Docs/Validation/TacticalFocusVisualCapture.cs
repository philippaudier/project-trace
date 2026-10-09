// Copy into isolated PlayMode tests; run with graphics and TRACE_CAPTURE_OUTPUT set.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Tactical;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class TacticalFocusVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureBeforeEnterActiveAndFirstTrace()
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
                var enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
                foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
                foreach (var member in squad.Members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                var leader = squad.Members[0].transform;
                // One pursuer close enough to wind up, two more at reading distance, one of them grouped.
                var melee = enemies.OfType<BasicMeleeEnemy>().ToArray();
                Place(melee[0], leader.position + leader.forward * 1.6f + leader.right * 0.8f);
                Place(melee[1], leader.position + leader.forward * 4.5f - leader.right * 2.2f);
                if (melee.Length > 2) Place(melee[2], leader.position + leader.forward * 6f + leader.right * 2.6f);
                melee[1].enabled = false;
                if (melee.Length > 2) melee[2].enabled = false;
                // Elevated three-quarter view so the scan, the markers and the ghost read from above.
                var camera = UnityEngine.Camera.main;
                camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
                camera.transform.position = leader.position - leader.forward * 4.2f - leader.right * 2.2f + Vector3.up * 4.4f;
                camera.transform.LookAt(leader.position + leader.forward * 2.6f);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture("Focus-before.png");
                // Enter right at the start of a wind-up: Focus stretches it, so it is still readable when active.
                float deadline = Time.unscaledTime + 6f;
                while (!(melee[0].IsPreparingAttack && melee[0].WindupRemaining > 0.25f) && Time.unscaledTime < deadline) yield return null;
                melee[1].GetComponent<ComboOpportunity>().Offer(ComboOpportunityType.Grouped, 6f, squad);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return new WaitForSecondsRealtime(0.1f);
                yield return Capture("Focus-enter.png");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("Focus-active.png");
                // Not a wallhack: a temporary wall between the camera and an enemy hides its outline with it.
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Vector3 target = melee[1].transform.position + Vector3.up;
                Vector3 toTarget = target - camera.transform.position;
                wall.transform.position = camera.transform.position + toTarget * 0.6f + Vector3.Cross(Vector3.up, toTarget.normalized) * 0.35f;
                wall.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                wall.transform.localScale = new Vector3(0.9f, 3f, 0.15f);
                yield return new WaitForSecondsRealtime(0.1f);
                yield return Capture("Focus-occlusion.png");
                Object.Destroy(wall);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.6f);

                yield return SceneManager.LoadSceneAsync("FirstTrace");
                squad = Object.FindFirstObjectByType<SquadController>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                yield return new WaitForSecondsRealtime(1.2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture("Focus-firsttrace.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.5f);
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

        private static void Place(EnemyBrain enemy, Vector3 position)
        {
            enemy.transform.position = position + Vector3.up * 0.08f;
            enemy.gameObject.SetActive(true);
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
