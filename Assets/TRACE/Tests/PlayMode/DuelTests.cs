using System.Collections;
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

namespace TRACE.Tests
{
    public sealed class DuelTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private TracePlayerInput input;
        private ThirdPersonMotor motor;
        private DamageReceiver receiver;
        private Health playerHealth;
        private BasicMeleeEnemy enemy;
        private Health enemyHealth;
        private float previousCapture;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Prototype");
            input = Object.FindFirstObjectByType<TracePlayerInput>();
            typeof(TracePlayerInput).GetField("captureCursor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(input, false);
            motor = input.GetComponent<ThirdPersonMotor>();
            receiver = input.GetComponent<DamageReceiver>();
            playerHealth = input.GetComponent<Health>();
            enemy = Object.FindFirstObjectByType<BasicMeleeEnemy>();
            enemyHealth = enemy.GetComponent<Health>();
            yield return Frames(10);
            Assert.That(enemy.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True, "Baked NavMesh must load with the scene.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            Time.captureDeltaTime = previousCapture;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator IdleDetectChaseStopAndAttack()
        {
            Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Idle));
            PlacePlayer(new Vector3(-11f, 0.04f, 6f));
            yield return Frames(5);
            Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Chase));
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Windup);
            Assert.That(Vector3.Distance(enemy.transform.position, input.transform.position), Is.InRange(1f, 1.8f));
            Vector3 stopped = enemy.transform.position;
            yield return Frames(10);
            Assert.That(Vector3.Distance(stopped, enemy.transform.position), Is.LessThan(0.08f));
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator WindupSingleHitAndRecoveryAreRespected()
        {
            yield return EnterWindup();
            yield return Frames(20);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f));
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Active);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(80f));
            yield return Frames(30);
            Assert.That(enemy.Phase, Is.EqualTo(BasicMeleeEnemy.AttackPhase.Recovery));
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(80f), "No repeated damage during active or recovery frames.");
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Active);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60f));
        }

        [UnityTest]
        public IEnumerator EnemyLosesTargetAndHandlesInactiveTarget()
        {
            PlacePlayer(new Vector3(-11f, 0.04f, 6f));
            yield return Frames(5);
            Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Chase));
            PlacePlayer(new Vector3(0f, 0.04f, -10f));
            yield return Frames(5);
            Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Idle));
            PlacePlayer(new Vector3(-11f, 0.04f, 6f));
            yield return Frames(5);
            input.gameObject.SetActive(false);
            yield return Frames(5);
            Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Idle));
        }

        [UnityTest]
        public IEnumerator NavMeshPursuitGoesAroundObstacle()
        {
            PlacePlayer(new Vector3(-11f, 0.04f, 6f));
            yield return Frames(5);
            PlacePlayer(new Vector3(-16f, 0.04f, 9f));
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(enemy.transform.position, input.transform.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(path.corners.Length, Is.GreaterThan(2));
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Windup, 360);
            Assert.That(Vector3.Distance(enemy.transform.position, input.transform.position), Is.LessThan(1.8f));
            Assert.That(Physics.CheckSphere(enemy.transform.position + Vector3.up, 0.3f, 1), Is.False);
        }

        [UnityTest]
        public IEnumerator DodgeMovesCameraRelativeAndStopsAtConfiguredDistance()
        {
            PlacePlayer(new Vector3(12f, 0.04f, -12f));
            yield return Frames(5);
            Vector3 start = input.transform.position;
            Keys(Key.D, Key.Space);
            yield return Frames(2);
            Assert.That(motor.IsDodging, Is.True);
            Assert.That(motor.DodgeDirection.x, Is.GreaterThan(0.99f));
            Keys();
            yield return Frames(24);
            Assert.That(input.transform.position.x - start.x, Is.InRange(2.9f, 3.1f));
            Assert.That(Mathf.Abs(input.transform.position.z - start.z), Is.LessThan(0.1f));
            Assert.That(motor.IsDodging, Is.False);
            yield return Frames(30);
            start = input.transform.position;
            Keys(Key.Space);
            yield return Frames(24);
            Assert.That(start.z - input.transform.position.z, Is.InRange(2.9f, 3.1f), "No-input dodge goes backwards.");
        }

        [UnityTest]
        public IEnumerator DodgeCooldownAndHeldKeyDoNotRepeat()
        {
            PlacePlayer(new Vector3(12f, 0.04f, -8f));
            yield return Frames(5);
            Vector3 start = input.transform.position;
            Keys(Key.Space);
            yield return Frames(22);
            Keys();
            yield return Frames(1);
            Keys(Key.Space);
            yield return Frames(70);
            Assert.That(start.z - input.transform.position.z, Is.InRange(2.9f, 3.1f));
            Keys();
            yield return Frames(2);
            Keys(Key.Space);
            yield return Frames(25);
            Assert.That(start.z - input.transform.position.z, Is.InRange(5.9f, 6.1f));
        }

        [UnityTest]
        public IEnumerator DodgeDoesNotPassThroughWall()
        {
            PlacePlayer(new Vector3(-6f, 0.04f, -2.5f));
            yield return Frames(5);
            Keys(Key.Space);
            yield return Frames(25);
            Assert.That(input.transform.position.z, Is.GreaterThan(-3.5f));
            Assert.That(motor.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator InvulnerabilityHasBothBoundariesAndClearsOnDisable()
        {
            PlacePlayer(new Vector3(12f, 0.04f, -8f));
            yield return Frames(5);
            Keys(Key.Space);
            yield return Frames(1);
            Assert.That(receiver.TryTakeDamage(5f), Is.True, "Startup before invulnerability.");
            yield return Frames(5);
            Assert.That(receiver.IsInvulnerable, Is.True);
            Assert.That(receiver.TryTakeDamage(20f), Is.False);
            yield return Frames(18);
            Assert.That(receiver.IsInvulnerable, Is.False);
            Assert.That(receiver.TryTakeDamage(5f), Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(90f));
            receiver.GrantInvulnerability(0f, 1f);
            motor.enabled = false;
            Assert.That(receiver.IsInvulnerable, Is.False);
        }

        [UnityTest]
        public IEnumerator TimedDodgeAvoidsRealStrikeAndAllowsCounterattack()
        {
            yield return EnterWindup();
            yield return Frames(19);
            Keys(Key.W, Key.Space); // Dodge into the enemy collider: still inside the hitbox.
            yield return Frames(2);
            Keys();
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Active);
            Assert.That(receiver.IsInvulnerable, Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f));
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Recovery);
            yield return Frames(10);
            Assert.That(receiver.IsInvulnerable, Is.False);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f), "Dodged hit cannot reapply after i-frames expire.");
            Click(true);
            yield return Frames(15);
            Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(75f));
        }

        [UnityTest]
        public IEnumerator EarlyDodgeIsHitAfterInvulnerabilityExpires()
        {
            yield return EnterWindup();
            Keys(Key.W, Key.Space);
            yield return Frames(2);
            Keys();
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Active);
            Assert.That(receiver.IsInvulnerable, Is.False);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(80f));
        }

        [UnityTest]
        public IEnumerator LateDodgeDoesNotUndoDamage()
        {
            yield return EnterWindup();
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Active);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(80f));
            Keys(Key.W, Key.Space);
            yield return Frames(8);
            Assert.That(receiver.IsInvulnerable, Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(80f));
        }

        [UnityTest]
        public IEnumerator SpatialDodgeMissesWithoutIFramesAndEnemyResumesChase()
        {
            yield return EnterWindup();
            Vector3 enemyStart = enemy.transform.position;
            Keys(Key.D, Key.Space);
            yield return Frames(2);
            Keys();
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Active);
            Assert.That(receiver.IsInvulnerable, Is.False);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f), "Moving outside the committed hitbox must also avoid the strike.");
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Recovery);
            Assert.That(Vector3.Distance(enemyStart, enemy.transform.position), Is.LessThan(0.08f));
            yield return Frames(65);
            Assert.That(Vector3.Distance(enemyStart, enemy.transform.position), Is.GreaterThan(0.3f));
        }

        [UnityTest]
        public IEnumerator DodgeCancelsPlayerAttack()
        {
            PlacePlayer(new Vector3(6f, 0.04f, -6.5f));
            yield return Frames(5);
            Click(true);
            Keys(Key.W, Key.Space);
            yield return Frames(15);
            Assert.That(GameObject.Find("EnemyDummy 1").GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(input.GetComponent<PlayerMeleeAttack>().IsAttacking, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayerCanKillEnemyAndDeathCancelsAttack()
        {
            yield return EnterWindup();
            for (int i = 0; i < 4; i++)
            {
                Click(true);
                yield return Frames(15);
                Click(false);
                if (i < 3) yield return Frames(16);
            }
            Assert.That(enemyHealth.IsDead, Is.True);
            Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Dead));
            Assert.That(enemy.GetComponent<NavMeshAgent>().enabled, Is.False);
            Assert.That(enemy.GetComponent<Collider>().enabled, Is.False);
            float healthAfterDeath = playerHealth.CurrentHealth;
            yield return Frames(40);
            Assert.That(enemy.gameObject.activeSelf, Is.False);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(healthAfterDeath));
        }

        [UnityTest]
        public IEnumerator PlayerDeathStopsControlAndEnemyAggression()
        {
            yield return EnterWindup();
            receiver.TryTakeDamage(100f);
            yield return Frames(3);
            Assert.That(playerHealth.IsDead, Is.True);
            Assert.That(motor.enabled, Is.False);
            Assert.That(input.enabled, Is.False);
            Assert.That(input.GetComponent<PlayerMeleeAttack>().enabled, Is.False);
            Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Idle));
        }

        private IEnumerator EnterWindup()
        {
            PlacePlayer(new Vector3(-11f, 0.04f, 9.5f));
            yield return UntilPhase(BasicMeleeEnemy.AttackPhase.Windup);
        }

        private IEnumerator UntilPhase(BasicMeleeEnemy.AttackPhase phase, int frames = 240)
        {
            for (int i = 0; i < frames && enemy.Phase != phase; i++) yield return null;
            Assert.That(enemy.Phase, Is.EqualTo(phase));
        }

        private void PlacePlayer(Vector3 position)
        {
            var controller = input.GetComponent<CharacterController>();
            motor.enabled = false;
            controller.enabled = false;
            input.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            motor.enabled = true;
            Physics.SyncTransforms();
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private void Click(bool pressed) => InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, pressed));
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    }
}
