// Reproduction utility: copy into the isolated project's PlayMode tests, then run with graphics enabled.
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
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class SwitchVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureSwitchAndRangedCombat()
        {
            Time.captureDeltaTime = 1f / 60f;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("PrototypeSwitch");
            var squad = Object.FindFirstObjectByType<SquadController>();
            var members = squad.Members.ToArray();
            var input = Object.FindFirstObjectByType<TracePlayerInput>();
            typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, false);
            var enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None);
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.3f);
            Capture("Switch-member1.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(squad.ActiveMember, Is.EqualTo(members[1]));
            yield return new WaitForSecondsRealtime(0.25f);
            Capture("Switch-member2.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(squad.ActiveMember, Is.EqualTo(members[2]));
            var motor = members[2].GetComponent<ThirdPersonMotor>();
            var body = members[2].GetComponent<CharacterController>();
            motor.enabled = false;
            body.enabled = false;
            members[2].transform.SetPositionAndRotation(new Vector3(8f, 0.05f, -12f), Quaternion.identity);
            body.enabled = true;
            motor.enabled = true;
            members[0].GetComponent<NavMeshAgent>().Warp(new Vector3(6.4f, 0.05f, -14.2f));
            members[1].GetComponent<NavMeshAgent>().Warp(new Vector3(9.9f, 0.05f, -15f));
            yield return new WaitForSecondsRealtime(0.25f);
            enemies[0].gameObject.SetActive(true);
            enemies[0].GetComponent<NavMeshAgent>().Warp(new Vector3(8f, 0.05f, -7.3f));
            enemies[0].GetComponent<NavMeshAgent>().speed = 0f;
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, true));
            var victim = enemies[0].GetComponent<Health>();
            for (int i = 0; i < 90 && victim.CurrentHealth == 100f; i++) yield return null;
            Assert.That(victim.CurrentHealth, Is.LessThan(100f));
            Capture("Switch-ranged-shot.png");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            members[2].Health.TakeDamage(100f);
            yield return null;
            Assert.That(squad.ActiveMember, Is.EqualTo(members[0]));
            yield return new WaitForSecondsRealtime(0.25f);
            Capture("Switch-auto-replacement.png");
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            Time.captureDeltaTime = 0f;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            LogAssert.NoUnexpectedReceived();
        }

        private static void Capture(string name)
        {
            string folder = Environment.GetEnvironmentVariable("TRACE_CAPTURE_OUTPUT") ?? Path.Combine(Application.dataPath, "../Docs/Validation");
            Directory.CreateDirectory(folder);
            var camera = UnityEngine.Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.Destroy(pixels);
            Object.Destroy(target);
        }
    }
}
