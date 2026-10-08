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
using TRACE.Skills;
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
    public sealed class TacticalVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureFocusSwitchTelegraphsAndRelease()
        {
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            float oldFixed = Time.fixedDeltaTime;
            TacticalFocus focus = null;
            // Settings first: a device added while the unfocused batch window still resets non-background devices stays disabled.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype");
                var squad = Object.FindFirstObjectByType<SquadController>();
                focus = squad.GetComponent<TacticalFocus>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                var members = squad.Members.ToArray();
                var enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
                foreach (var enemy in enemies) enemy.enabled = false;
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(squad.SwitchToMember(1), Is.True);
                Place(members[1], new Vector3(-11f, 0.08f, 5f));
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                yield return new WaitForSecondsRealtime(0.35f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return new WaitForSecondsRealtime(0.7f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(focus.IsActive, Is.True);
                Assert.That(enemies.Any(e => e.GetComponent<ComboOpportunity>().Type == ComboOpportunityType.Grouped), Is.True);
                yield return Capture("Tactical-grouped.png");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab, Key.Digit1));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(squad.ActiveMember, Is.EqualTo(members[0]));
                Place(members[0], new Vector3(-11f, 0.08f, 5f));
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                members[0].GetComponent<Shield>().Grant(30, 5);
                members[0].GetComponent<ComboOpportunity>().Offer(ComboOpportunityType.Protected, 3, focus);
                // Real enemy windups, positioned to make two committed strike directions visible.
                for (int i = 0; i < 2; i++)
                {
                    enemies[i].GetComponent<NavMeshAgent>().Warp(new Vector3(-11f + (i == 0 ? -0.85f : 0.85f), 0, 6.1f));
                    enemies[i].enabled = true;
                }
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(enemies.Any(e => e.GetComponent<TacticalTelegraph>().IsVisible), Is.True);
                yield return Capture("Tactical-windup.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.22f);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(squad.GetComponent<TacticalOverlay>().IsVisible, Is.False);
                yield return Capture("Tactical-released.png");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (focus != null) focus.enabled = false;
                Time.timeScale = 1f; Time.fixedDeltaTime = oldFixed;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            }
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Place(SquadMember member, Vector3 position)
        {
            var body = member.GetComponent<CharacterController>(); body.enabled = false;
            member.transform.SetPositionAndRotation(position, Quaternion.identity); body.enabled = true;
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
            // The screen-space canvas adopts the capture resolution at the end of the first frame;
            // the second frame places the enemy cards against that size before the image is read.
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
