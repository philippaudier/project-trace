using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TRACE.Combat;
using TRACE.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class CombatTests
    {
        private Mouse mouse;
        private TracePlayerInput input;
        private PlayerMeleeAttack attack;
        private Health dummy;
        private float oldCaptureDelta;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("PrototypeLegacy");
            input = Object.FindFirstObjectByType<TracePlayerInput>();
            typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(input, false);
            attack = input.GetComponent<PlayerMeleeAttack>();
            dummy = GameObject.Find("EnemyDummy 1").GetComponent<Health>();
            PositionPlayer(new Vector3(6f, 0.04f, -6.5f));
            yield return Frames(5);
            Assert.That(dummy.GetComponent<Collider>().enabled, Is.True, "Living dummy must retain its collider after startup.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(mouse);
            Time.captureDeltaTime = oldCaptureDelta;
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InputAttackHitsOnceAcrossWindowAndMultipleColliders()
        {
            for (int i = 0; i < 35; i++)
            {
                var extra = new GameObject("Extra Hit Collider " + i);
                extra.layer = dummy.gameObject.layer;
                extra.transform.SetParent(dummy.transform, false);
                extra.transform.localPosition = Vector3.up;
                extra.AddComponent<SphereCollider>().radius = 0.35f;
            }
            Physics.SyncTransforms();
            int damaged = 0;
            dummy.OnDamaged += _ => damaged++;
            Click(true);
            yield return Frames(2);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(100f), "Windup must not deal damage.");
            yield return Frames(18);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(75f));
            Assert.That(damaged, Is.EqualTo(1));
            Assert.That(attack.IsAttacking, Is.False);
        }

        [UnityTest]
        public IEnumerator CooldownRejectsSpamAndHoldingDoesNotRepeat()
        {
            Click(true);
            yield return Frames(15);
            Click(false);
            yield return Frames(1);
            Click(true);
            yield return Frames(60);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(75f));
            Click(false);
            yield return Frames(2);
            Click(true);
            yield return Frames(15);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(50f));
        }

        [UnityTest]
        public IEnumerator SoftTargetingHelpsSmallOffsetWithoutRotatingPlayer()
        {
            dummy.transform.position += Vector3.right * 0.95f;
            Physics.SyncTransforms();
            Quaternion facing = input.transform.rotation;
            Click(true);
            yield return Frames(15);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(75f));
            Assert.That(Quaternion.Angle(facing, input.transform.rotation), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator DirectionPriorityChoosesAlignedTargetOverCloserOffset()
        {
            var second = GameObject.Find("EnemyDummy 2").GetComponent<Health>();
            dummy.transform.position = new Vector3(6f, 0f, -4.8f);
            second.transform.position = new Vector3(5.05f, 0f, -5.1f);
            Physics.SyncTransforms();
            Click(true);
            yield return Frames(15);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(75f));
            Assert.That(second.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator RangeBehindLayerAndWallRejectInvalidHits()
        {
            PositionPlayer(new Vector3(6f, 0.04f, -9f));
            yield return Swing();
            Assert.That(dummy.CurrentHealth, Is.EqualTo(100f), "Out of range");
            PositionPlayer(new Vector3(6f, 0.04f, -3.5f));
            yield return Swing();
            Assert.That(dummy.CurrentHealth, Is.EqualTo(100f), "Behind attacker");
            PositionPlayer(new Vector3(6f, 0.04f, -6.5f));
            int layer = dummy.gameObject.layer;
            dummy.gameObject.layer = 0;
            yield return Swing();
            Assert.That(dummy.CurrentHealth, Is.EqualTo(100f), "Outside target mask");
            dummy.gameObject.layer = layer;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(6f, 1f, -5.8f);
            wall.transform.localScale = new Vector3(3f, 2f, 0.15f);
            Physics.SyncTransforms();
            yield return Swing();
            Assert.That(dummy.CurrentHealth, Is.EqualTo(100f), "Occluded by wall");
        }

        [UnityTest]
        public IEnumerator FourHitsKillDisableCollisionsThenDeactivate()
        {
            int deaths = 0;
            dummy.OnDeath += () => deaths++;
            for (int i = 0; i < 3; i++)
                yield return Swing();
            Assert.That(dummy.CurrentHealth, Is.EqualTo(25f));
            Click(true);
            yield return Frames(10);
            Assert.That(dummy.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(dummy.gameObject.activeSelf, Is.True, "Keep death feedback briefly visible");
            Assert.That(dummy.GetComponent<Collider>().enabled, Is.False);
            dummy.TakeDamage(100f);
            dummy.Heal(100f);
            Assert.That(dummy.CurrentHealth, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
            yield return Frames(40);
            Assert.That(dummy.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator HitFlashesRecoilsAndRecoversWithoutMovingCollider()
        {
            var visual = dummy.transform.Find("Dummy Visual");
            var renderer = visual.GetComponent<Renderer>();
            Vector3 rest = visual.localPosition;
            Vector3 root = dummy.transform.position;
            dummy.TakeDamage(25f);
            yield return Frames(2);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Color color = properties.GetColor("_BaseColor");
            Assert.That(color.g, Is.GreaterThan(0.5f));
            Assert.That(Vector3.Distance(visual.localPosition, rest), Is.GreaterThan(0.05f));
            Assert.That(dummy.transform.position, Is.EqualTo(root));
            yield return Frames(20);
            Assert.That(Vector3.Distance(visual.localPosition, rest), Is.LessThan(0.001f));
            renderer.GetPropertyBlock(properties);
            Assert.That(Vector4.Distance(properties.GetColor("_BaseColor"), renderer.sharedMaterial.GetColor("_BaseColor")), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator DisablingInputCancelsWindupAndReenableDoesNotDuplicateHit()
        {
            Click(true);
            yield return Frames(1);
            input.enabled = false;
            yield return Frames(25);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(100f));
            Assert.That(attack.IsAttacking, Is.False);
            Click(false);
            input.enabled = true;
            yield return Frames(5);
            Click(true);
            yield return Frames(15);
            Assert.That(dummy.CurrentHealth, Is.EqualTo(75f));
        }

        private IEnumerator Swing()
        {
            Click(true);
            yield return Frames(15);
            Click(false);
            yield return Frames(16);
        }

        private void PositionPlayer(Vector3 position)
        {
            var controller = input.GetComponent<CharacterController>();
            controller.enabled = false;
            input.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private void Click(bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, pressed));
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }
    }

    public sealed class HealthTests
    {
        private GameObject owner;
        private Health health;
        [SetUp] public void SetUp() { owner = new GameObject("Health Test"); health = owner.AddComponent<Health>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(owner); }

        [Test]
        public void DamageHealAndClamping()
        {
            float received = 0f;
            int deaths = 0;
            health.OnDamaged += amount => received += amount;
            health.OnDeath += () => deaths++;
            health.TakeDamage(30f);
            Assert.That(health.CurrentHealth, Is.EqualTo(70f));
            health.Heal(500f);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            health.TakeDamage(500f);
            Assert.That(received, Is.EqualTo(130f));
            Assert.That(health.CurrentHealth, Is.Zero);
            health.TakeDamage(1f);
            health.Heal(100f);
            Assert.That(health.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void InvalidAmountsAndDisabledHealthAreIgnored()
        {
            foreach (float amount in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            { health.TakeDamage(amount); health.Heal(amount); }
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            health.enabled = false;
            health.TakeDamage(25f);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void ReentrantDamageRaisesDeathOnlyOnce()
        {
            int deaths = 0;
            health.OnDeath += () => deaths++;
            health.OnDamaged += _ => health.TakeDamage(1000f);
            health.TakeDamage(1f);
            Assert.That(deaths, Is.EqualTo(1));
        }
    }
}
