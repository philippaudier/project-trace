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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class SquadVisualCapture
    {
        [UnityTest]
        public IEnumerator CaptureActualGameCamera()
        {
            Time.captureDeltaTime = 1f / 60f;
            yield return SceneManager.LoadSceneAsync("PrototypeSquad");
            var squad = Object.FindFirstObjectByType<SquadController>();
            var input = squad.Leader.GetComponent<TracePlayerInput>();
            typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, false);
            input.enabled = false;
            var motor = input.GetComponent<ThirdPersonMotor>();
            motor.enabled = false;
            
            var companions = Object.FindObjectsByType<CompanionController>(FindObjectsSortMode.None);
            foreach (var companion in companions) companion.Receiver.GrantInvulnerability(0f, 1000f);
            for (int i = 0; i < 60; i++) yield return null;
            Capture("Squad-formation.png");
            var cc = input.GetComponent<CharacterController>();
            cc.enabled = false;
            input.transform.position = new Vector3(-10f, 0.04f, 6f);
            cc.enabled = true;
            Physics.SyncTransforms();
            var ranged = companions.Single(c => c.Role == CompanionController.CombatRole.Ranged);
            var shot = ranged.transform.Find("Attack Cue");
            bool captured = false;
            for (int i = 0; i < 600; i++)
            {
                squad.Leader.Health.Heal(100f); yield return null;
                if (!captured && shot.gameObject.activeSelf && shot.localScale.z > 2f)
                {
                    Capture("Squad-combat.png");
                    captured = true;
                }
            }
            Assert.That(captured, Is.True);
            foreach (var enemy in Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None))
                enemy.GetComponent<Health>().TakeDamage(1000f);
            for (int i = 0; i < 240; i++) yield return null;
            cc.enabled = false; input.transform.position = new Vector3(3f, 0.04f, 0f); cc.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 300; i++) yield return null;
            Capture("Squad-regroup.png");
            companions[0].Health.TakeDamage(100f);
            for (int i = 0; i < 30; i++) yield return null;
            Capture("Squad-incapacitated.png");
            Time.captureDeltaTime = 0f;
            LogAssert.NoUnexpectedReceived();
        }
        private static void Capture(string name)
        {
            var camera = UnityEngine.Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var priorTarget = camera.targetTexture;
            var priorActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("C:/Users/Philippe/Documents/Programming/Unity/Projects/project-trace/Docs/Validation/" + name, pixels.EncodeToPNG());
            camera.targetTexture = priorTarget;
            RenderTexture.active = priorActive;
            Object.Destroy(pixels);
            Object.Destroy(target);
        }
    }
}




