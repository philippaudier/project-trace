using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class ComboTests
    {
        private SquadController squad;
        private SquadMember[] members;
        private BasicMeleeEnemy[] enemies;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private GameObject wall;
        private GravityField Field => members[1].GetComponent<GravityFieldSkill>().Field;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Prototype");
            squad = Object.FindFirstObjectByType<SquadController>();
            members = squad.Members.ToArray();
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None);
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            yield return Frames(10);
            FreezeCompanions();
            PlaceActive(new Vector3(-11f, 0.08f, 3f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (wall != null) Object.Destroy(wall);
            Time.timeScale = 1f;
            Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GravityRequiresDwellThenOffersGroupedWithSourceAndTarget()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var opportunity = enemy.GetComponent<ComboOpportunity>();
            Field.Spawn(enemy.transform.position);
            yield return Frames(25);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
            yield return Frames(16);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.Grouped));
            Assert.That(opportunity.RemainingDuration, Is.InRange(2.3f, 2.5f));
            Assert.That(opportunity.Source, Is.EqualTo(Field));
            Assert.That(opportunity.Target, Is.EqualTo(enemy.GetComponent<Health>()));
            Assert.That(squad.ActiveMember, Is.EqualTo(members[0]), "Opportunity must not force a switch.");
        }

        [UnityTest]
        public IEnumerator InterruptedExposureDoesNotAccumulateAcrossSeparateVisits()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var agent = enemy.GetComponent<NavMeshAgent>();
            Field.Spawn(enemy.transform.position);
            yield return Frames(20);
            agent.Warp(new Vector3(-16f, 0f, 15f));
            yield return Frames(5);
            agent.Warp(new Vector3(-11f, 0f, 7f));
            yield return Frames(20);
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.None));
            yield return Frames(20);
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Grouped));
        }

        [UnityTest]
        public IEnumerator GroupedExpiresWithoutRefreshWhileStillInsideSameCast()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var opportunity = enemy.GetComponent<ComboOpportunity>();
            Set(Field, "duration", 8f);
            Field.Spawn(enemy.transform.position);
            yield return Frames(200);
            Assert.That(Field.gameObject.activeSelf, Is.True);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
            Assert.That(opportunity.Source, Is.Null);
            yield return Frames(90);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
        }

        [UnityTest]
        public IEnumerator GroupedConsumedOncePerCastAndCanReturnOnNextCast()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var opportunity = enemy.GetComponent<ComboOpportunity>();
            Field.Spawn(enemy.transform.position);
            yield return Frames(40);
            Assert.That(opportunity.TryConsume(ComboOpportunityType.Grouped), Is.True);
            Assert.That(opportunity.TryConsume(ComboOpportunityType.Grouped), Is.False);
            yield return Frames(100);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
            Field.Spawn(enemy.transform.position);
            yield return Frames(40);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.Grouped));
            Assert.That(opportunity.ConsumptionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NormalDashStillDealsThirtyFiveWithoutSplash()
        {
            var primary = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var nearby = EnemyAt(1, new Vector3(-9.6f, 0f, 7f));
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(17);
            Assert.That(primary.GetComponent<Health>().CurrentHealth, Is.EqualTo(65f));
            Assert.That(nearby.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator ControlPreparesThenManualSwitchAndQExploitGrouped()
        {
            Select(1);
            PlaceActive(new Vector3(-11f, 0.08f, 3f));
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return Frames(40);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Grouped));
            Assert.That(squad.ActiveMember, Is.EqualTo(members[1]));
            Select(0);
            PlaceActive(new Vector3(-11f, 0.08f, 3f));
            yield return Frames(1);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return Frames(17);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(47.5f).Within(0.01f));
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.None));
            Assert.That(enemy.GetComponent<ComboOpportunity>().ConsumptionCount, Is.EqualTo(1));
            yield return Frames(60);
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.None));
        }

        [UnityTest]
        public IEnumerator ComboSplashHitsEachNearbyEnemyOnceAndDoesNotChain()
        {
            var primary = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var nearby = EnemyAt(1, new Vector3(-9.6f, 0f, 7f));
            var distant = EnemyAt(2, new Vector3(-7.8f, 0f, 7f));
            Group(primary); Group(nearby);
            var extra = new GameObject("Duplicate Hit Collider");
            extra.layer = nearby.gameObject.layer;
            extra.transform.SetParent(nearby.transform, false);
            extra.transform.localPosition = Vector3.up;
            extra.AddComponent<SphereCollider>().radius = 0.3f;
            Physics.SyncTransforms();
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(17);
            Assert.That(primary.GetComponent<Health>().CurrentHealth, Is.EqualTo(47.5f).Within(0.01f));
            Assert.That(nearby.GetComponent<Health>().CurrentHealth, Is.EqualTo(82.5f).Within(0.01f));
            Assert.That(distant.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(nearby.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Grouped));
        }

        [UnityTest]
        public IEnumerator SplashCannotCrossWalls()
        {
            var primary = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var nearby = EnemyAt(1, new Vector3(-9.4f, 0f, 7f));
            Group(primary);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(-10.2f, 1.5f, 7f);
            wall.transform.localScale = new Vector3(0.2f, 3f, 3f);
            Physics.SyncTransforms();
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(17);
            Assert.That(primary.GetComponent<Health>().CurrentHealth, Is.EqualTo(47.5f).Within(0.01f));
            Assert.That(nearby.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator KillingPrimaryStillProducesComboSplashAndFeedback()
        {
            var primary = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var nearby = EnemyAt(1, new Vector3(-9.6f, 0f, 7f));
            primary.GetComponent<Health>().TakeDamage(70f);
            Group(primary);
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(17);
            Assert.That(primary.GetComponent<Health>().IsDead, Is.True);
            Assert.That(primary.GetComponent<ComboOpportunity>().ConsumptionCount, Is.EqualTo(1));
            Assert.That(nearby.GetComponent<Health>().CurrentHealth, Is.EqualTo(82.5f).Within(0.01f));
            Assert.That(primary.transform.Find("Combo Impact").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator CancelledDashLeavesGroupedForAnotherAttempt()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            Group(enemy);
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(2);
            Select(1);
            yield return Frames(16);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Grouped));
        }

        [UnityTest]
        public IEnumerator PulseShieldNeedsTwentyAbsorbedDamageForProtected()
        {
            Select(2);
            Assert.That(members[2].GetComponent<PulseShield>().Activate(), Is.True);
            var opportunity = members[0].GetComponent<ComboOpportunity>();
            members[0].Receiver.TryTakeDamage(10f);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
            members[0].Receiver.TryTakeDamage(10f);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.Protected));
            Assert.That(opportunity.Source, Is.EqualTo(members[0].GetComponent<Shield>()));
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(members[0].GetComponent<Shield>().CurrentAmount, Is.EqualTo(10f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShieldCanOfferOnlyOnceAndRawHealthDamageDoesNotPrepare()
        {
            var member = members[0];
            var opportunity = member.GetComponent<ComboOpportunity>();
            member.Health.TakeDamage(20f);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
            Protect(member);
            Assert.That(opportunity.TryConsume(ComboOpportunityType.Protected), Is.True);
            yield return Frames(40);
            member.Receiver.TryTakeDamage(10f);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
            Protect(member);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.Protected));
        }

        [UnityTest]
        public IEnumerator ProtectedExpiresAndDoesNotRefreshOnFurtherAbsorption()
        {
            Protect(members[0]);
            yield return Frames(150);
            members[0].Receiver.TryTakeDamage(10f);
            yield return Frames(40);
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.None));
        }

        [UnityTest]
        public IEnumerator ProtectedBoostsExactlyNextMeleeAttack()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 4.2f));
            Protect(members[0]);
            Click(true);
            yield return Frames(18);
            Click(false);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(70f).Within(0.01f));
            Assert.That(members[0].GetComponent<ComboOpportunity>().ConsumptionCount, Is.EqualTo(1));
            yield return Frames(20);
            Click(true);
            yield return Frames(18);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(45f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator MissedMeleeDoesNotConsumeProtected()
        {
            Protect(members[0]);
            Click(true);
            yield return Frames(20);
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Protected));
        }

        [UnityTest]
        public IEnumerator ProtectedBoostsPlayerRangedShot()
        {
            Select(2);
            PlaceActive(new Vector3(-11f, 0.08f, 3f));
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            Protect(members[2]);
            Click(true);
            yield return Frames(30);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(90.4f).Within(0.01f));
            Assert.That(members[2].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.None));
        }

        [UnityTest]
        public IEnumerator ProtectedBoostsNormalDash()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            Protect(members[0]);
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(17);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(58f).Within(0.01f));
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.None));
        }

        [UnityTest]
        public IEnumerator GroupedTakesPriorityWithoutStackingProtected()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            Group(enemy); Protect(members[0]);
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(17);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(47.5f).Within(0.01f));
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Protected));
        }

        [UnityTest]
        public IEnumerator AutomaticCompanionHitConsumesNeitherOpportunity()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            members[1].GetComponent<NavMeshAgent>().Warp(new Vector3(-11f, 0f, 5.8f));
            Physics.SyncTransforms();
            Group(enemy); Protect(members[1]);
            enemy.enabled = true; // Companion targeting intentionally ignores disabled enemy brains.
            enemy.GetComponent<NavMeshAgent>().speed = 0f;
            members[1].Companion.enabled = true;
            for (int i = 0; i < 90 && enemy.GetComponent<Health>().CurrentHealth == 100f; i++) yield return null;
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(88f).Within(0.01f));
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Grouped));
            Assert.That(members[1].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Protected));
        }

        [UnityTest]
        public IEnumerator ProtectedStaysOnBearerAcrossSwitchAndOthersCannotSpendIt()
        {
            Protect(members[0]);
            Select(2);
            PlaceActive(new Vector3(-11f, 0.08f, 3f));
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            Click(true);
            yield return Frames(30);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(92f));
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Protected));
            Select(0);
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Protected));
        }

        [UnityTest]
        public IEnumerator DeathClearsOpportunityAndPauseFreezesItsWindow()
        {
            Protect(members[0]);
            var opportunity = members[0].GetComponent<ComboOpportunity>();
            float remaining = opportunity.RemainingDuration;
            Time.timeScale = 0f;
            yield return Frames(20);
            Assert.That(opportunity.RemainingDuration, Is.EqualTo(remaining).Within(0.001f));
            Assert.That(opportunity.TryConsume(ComboOpportunityType.Protected), Is.False);
            Time.timeScale = 1f;
            members[0].Health.TakeDamage(100f);
            Assert.That(opportunity.Type, Is.EqualTo(ComboOpportunityType.None));
            Assert.That(opportunity.Offer(ComboOpportunityType.Protected, 3f, this.squad), Is.False);
        }

        [UnityTest]
        public IEnumerator ReadyMarkerAndImpactFeedbackHaveDistinctLifetimes()
        {
            FrameLabels();
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            Group(enemy);
            yield return Frames(2);
            Assert.That(enemy.transform.Find("Combo Ready").gameObject.activeSelf, Is.True);
            Assert.That(enemy.GetComponentInChildren<TextMesh>().text, Does.Contain("1 puis E"));
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(15);
            Assert.That(enemy.transform.Find("Combo Ready").gameObject.activeSelf, Is.False);
            Assert.That(enemy.transform.Find("Combo Impact").gameObject.activeSelf, Is.True);
            Assert.That(enemy.GetComponentInChildren<TextMesh>().text, Is.EqualTo("COMBO !"));
            yield return Frames(30);
            Assert.That(enemy.transform.Find("Combo Impact").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator InvulnerableTargetDoesNotConsumeEitherOpportunity()
        {
            var enemy = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var receiver = enemy.gameObject.AddComponent<DamageReceiver>();
            receiver.GrantInvulnerability(0f, 2f);
            Group(enemy); Protect(members[0]);
            members[0].GetComponent<DashStrike>().Activate();
            yield return Frames(17);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(enemy.GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Grouped));
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.Protected));
        }

        [UnityTest]
        public IEnumerator IFramesDoNotGenerateProtected()
        {
            members[0].GetComponent<Shield>().Grant(30f, 5f);
            members[0].Receiver.GrantInvulnerability(0f, 1f);
            Assert.That(members[0].Receiver.TryTakeDamage(100f), Is.False);
            Assert.That(members[0].GetComponent<ComboOpportunity>().Type, Is.EqualTo(ComboOpportunityType.None));
            Assert.That(members[0].GetComponent<Shield>().CurrentAmount, Is.EqualTo(30f));
            yield return null;
        }

        private void Group(BasicMeleeEnemy enemy) => Assert.That(enemy.GetComponent<ComboOpportunity>()
            .Offer(ComboOpportunityType.Grouped, 2.5f, Field), Is.True);

        [UnityTest]
        public IEnumerator GroupedCrowdShowsOneInstructionAndActiveCounterHasPriority()
        {
            FrameLabels();
            var first = EnemyAt(0, new Vector3(-11f, 0f, 7f));
            var second = EnemyAt(1, new Vector3(-9.7f, 0f, 7.3f));
            Group(first); Group(second);
            yield return Frames(3);
            Assert.That(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Count(t => t.text.Contains("COMBO")), Is.EqualTo(1));
            Assert.That(first.transform.Find("Combo Ready").gameObject.activeSelf, Is.True);
            Assert.That(second.transform.Find("Combo Ready").gameObject.activeSelf, Is.True);
            Protect(members[0]);
            yield return Frames(3);
            Assert.That(members[0].GetComponentInChildren<TextMesh>().text, Does.Contain("CONTRE"));
            Assert.That(first.GetComponentInChildren<TextMesh>(), Is.Null);
            Assert.That(second.GetComponentInChildren<TextMesh>(), Is.Null);
        }
        private static void Protect(SquadMember member)
        {
            member.GetComponent<Shield>().Grant(30f, 5f);
            member.Receiver.TryTakeDamage(20f);
        }
        private static void FrameLabels()
        {
            // Headless runs do not render a Cinemachine frame. Give viewport-selection tests
            // an explicit, repeatable camera instead of depending on a graphics-driven blend.
            var camera = UnityEngine.Camera.main;
            camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
            camera.aspect = 16f / 9f;
            camera.transform.SetPositionAndRotation(new Vector3(-11f, 6f, -3f),
                Quaternion.LookRotation(new Vector3(0f, -3f, 10f)));
        }
        private void Select(int index) { Assert.That(squad.SwitchToMember(index), Is.True); FreezeCompanions(); }
        private void FreezeCompanions()
        {
            foreach (var member in members)
            {
                if (member.IsPlayerControlled) continue;
                member.Companion.enabled = false;
                member.Companion.Attack.Cancel();
                var agent = member.GetComponent<NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            }
        }
        private void PlaceActive(Vector3 position)
        {
            var member = squad.ActiveMember;
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false; body.enabled = false;
            member.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true; motor.enabled = true;
            Physics.SyncTransforms();
        }
        private BasicMeleeEnemy EnemyAt(int index, Vector3 position)
        {
            var enemy = enemies[index];
            enemy.gameObject.SetActive(true);
            enemy.enabled = false;
            var agent = enemy.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(position), Is.True);
            agent.ResetPath(); agent.isStopped = true;
            Physics.SyncTransforms();
            return enemy;
        }
        private void Click(bool held) => InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, held));
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
