// Copy into isolated PlayMode tests; run with graphics and TRACE_CAPTURE_OUTPUT set.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using System.Linq;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Encounter;
using TRACE.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class FieldTestVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureFieldTestViews()
        {
            yield return SceneManager.LoadSceneAsync("FieldTest");
            var squad = Object.FindFirstObjectByType<SquadController>();
            squad.GetComponent<TracePlayerInput>().GetType().GetField("captureCursor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(squad.GetComponent<TracePlayerInput>(), false);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("FieldTest-spawn-player.png");
            var camera = UnityEngine.Camera.main;
            camera.GetComponent<CinemachineBrain>().enabled = false;
            (string name, Vector3 from, Vector3 at)[] views =
            {
                ("FieldTest-overview.png", new Vector3(10f, 190f, -150f), new Vector3(0f, 0f, 10f)),
                ("FieldTest-yard.png", new Vector3(10f, 9f, -88f), new Vector3(0f, 0f, -30f)),
                ("FieldTest-hangar.png", new Vector3(-16f, 4f, 12f), new Vector3(10f, 3f, 34f)),
                ("FieldTest-hangar-door.png", new Vector3(0f, 2.2f, 36f), new Vector3(0f, 2f, -10f)),
                ("FieldTest-lobby.png", new Vector3(-24f, 2.2f, 32f), new Vector3(-38f, 1.5f, 46f)),
                ("FieldTest-machine.png", new Vector3(-24f, 6f, 80f), new Vector3(-48f, 1f, 92f)),
                ("FieldTest-ravine.png", new Vector3(-74f, 6f, -14f), new Vector3(-88f, 0f, 22f)),
                ("FieldTest-pit.png", new Vector3(-60f, 3f, 60f), new Vector3(-74f, -3f, 76f)),
                ("FieldTest-highroute.png", new Vector3(40f, 8f, -40f), new Vector3(40f, 6f, 30f)),
                ("FieldTest-tower-view.png", new Vector3(88f, 14f, 99f), new Vector3(30f, 0f, 10f)),
                ("FieldTest-trace.png", new Vector3(60f, 2f, 92f), new Vector3(60f, 1.5f, 104f)),
                ("FieldTest-trace-chamber.png", new Vector3(60f, 1.7f, 105f), new Vector3(60f, 0.8f, 116f)),
                ("FieldTest-basin.png", new Vector3(80f, 3.5f, 58f), new Vector3(92f, 0.6f, 71f)),
                ("FieldTest-ravine-floor.png", new Vector3(-82f, 1.8f, 0f), new Vector3(-88f, 0.5f, 22f)),
            };
            foreach (var view in views)
            {
                // The aerial view sits beyond the fog end; the ground views keep the scene's fog.
                RenderSettings.fog = !view.name.Contains("overview");
                camera.farClipPlane = view.name.Contains("overview") ? 1000f : 400f;
                camera.transform.position = view.from;
                camera.transform.rotation = Quaternion.LookRotation(view.at - view.from, Vector3.up);
                yield return Capture(view.name);
            }

            // Tactical Focus in the wet yard: the Focus look must stay distinct from the environment grade.
            RenderSettings.fog = true; camera.farClipPlane = 400f;
            camera.GetComponent<CinemachineBrain>().enabled = true;
            var settings = InputSystem.settings;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var members = squad.Members.ToArray();
            Vector3[] spots = { new Vector3(2f, 0.08f, -60f), new Vector3(0f, 0.08f, -62f), new Vector3(4f, 0.08f, -62f) };
            for (int i = 0; i < members.Length; i++)
            {
                var body = members[i].GetComponent<CharacterController>(); if (body != null) body.enabled = false;
                var agent = members[i].GetComponent<NavMeshAgent>(); if (agent != null && agent.enabled) agent.Warp(spots[i]);
                members[i].transform.SetPositionAndRotation(spots[i], Quaternion.identity);
                if (body != null) body.enabled = true;
            }
            Object.FindFirstObjectByType<EncounterDebugStations>().Spawn(0);
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("FieldTest-focus-off.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("FieldTest-focus-on.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.5f);
            InputSystem.RemoveDevice(keyboard);
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
