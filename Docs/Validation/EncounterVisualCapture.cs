// Copy into isolated PlayMode tests; run with graphics and TRACE_CAPTURE_OUTPUT set.
// IMGUI (encounter HUD, threat hint) is not part of a camera render; captures show world and tactical overlay.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Encounter;
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
    public sealed class EncounterVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureSpawnFocusAndGuard()
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
                yield return SceneManager.LoadSceneAsync("PrototypeEncounter");
                var squad = Object.FindFirstObjectByType<SquadController>();
                var encounter = Object.FindFirstObjectByType<EncounterController>();
                focus = squad.GetComponent<TacticalFocus>();
                Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
                var members = squad.Members.ToArray();
                float deadline = Time.unscaledTime + 6f;
                while (encounter.State != EncounterController.EncounterState.Spawning && Time.unscaledTime < deadline) yield return null;
                Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Spawning));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture("Encounter-spawn.png");

                deadline = Time.unscaledTime + 3f;
                while (encounter.State != EncounterController.EncounterState.Fighting && Time.unscaledTime < deadline) yield return null;
                encounter.enabled = false;
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                var all = encounter.Waves.SelectMany(w => w.enemies).ToArray();
                var marksman = (MarksmanEnemy)Spawn(all.First(e => e.Archetype == "MARKSMAN"), members[0].transform.position + new Vector3(2.5f, 0f, 8f));
                var bulwark = (BasicMeleeEnemy)Spawn(all.First(e => e.Archetype == "BULWARK"), members[0].transform.position + new Vector3(-2f, 0f, 4.5f), Quaternion.Euler(0f, 180f, 0f));
                bulwark.enabled = false;
                deadline = Time.unscaledTime + 4f;
                while (marksman.State != MarksmanEnemy.MarksmanState.Aim && Time.unscaledTime < deadline) yield return null;
                Assert.That(marksman.State, Is.EqualTo(MarksmanEnemy.MarksmanState.Aim));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(focus.IsActive, Is.True);
                Assert.That(bulwark.transform.Find("Guard Arc").gameObject.activeSelf, Is.True);
                yield return Capture("Encounter-focus.png");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.25f);
                marksman.enabled = false;
                Place(members[0], bulwark.transform.position + Vector3.back * 1.4f);
                foreach (var member in members) if (!member.IsPlayerControlled) member.Companion.enabled = false;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(bulwark.GetComponent<FrontalGuard>().BlockedCount, Is.EqualTo(1));
                yield return Capture("Encounter-guard.png");
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
        private static EnemyBrain Spawn(EnemyBrain enemy, Vector3 position) => Spawn(enemy, position, Quaternion.identity);
        private static EnemyBrain Spawn(EnemyBrain enemy, Vector3 position, Quaternion rotation)
        {
            enemy.gameObject.SetActive(true);
            Assert.That(enemy.GetComponent<NavMeshAgent>().Warp(position), Is.True);
            enemy.transform.rotation = rotation;
            Physics.SyncTransforms();
            return enemy;
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
