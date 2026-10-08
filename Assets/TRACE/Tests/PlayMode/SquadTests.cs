using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    public sealed class SquadTests
    {
        private SquadController squad;
        private CompanionController melee;
        private CompanionController ranged;
        private BasicMeleeEnemy[] enemies;
        private TracePlayerInput input;
        private float previousCapture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return SceneManager.LoadSceneAsync("PrototypeSquad");
            squad = Object.FindFirstObjectByType<SquadController>();
            var members = Object.FindObjectsByType<CompanionController>(FindObjectsSortMode.None);
            melee = members.Single(c => c.Role == CompanionController.CombatRole.Melee);
            ranged = members.Single(c => c.Role == CompanionController.CombatRole.Ranged);
            enemies = Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None);
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            input = squad.Leader.GetComponent<TracePlayerInput>();
            Set(input, "captureCursor", false);
            input.enabled = false;
            input.GetComponent<ThirdPersonMotor>().enabled = false;
            squad.Leader.GrantInvulnerability(0f, 1000f);
            melee.Receiver.GrantInvulnerability(0f, 1000f);
            ranged.Receiver.GrantInvulnerability(0f, 1000f);
            yield return Frames(5);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = previousCapture;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SceneHasOnePlayerTwoDistinctCompanionsAndThreeEnemies()
        {
            Assert.That(Object.FindObjectsByType<TracePlayerInput>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(enemies.Length, Is.EqualTo(3));
            foreach (var member in new[] { melee, ranged })
            {
                Assert.That(member.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
                Assert.That(member.GetComponent<ThirdPersonMotor>(), Is.Null);
                Assert.That(member.Health.CurrentHealth, Is.EqualTo(100f));
                Assert.That(Physics.GetIgnoreCollision(member.GetComponent<Collider>(), input.GetComponent<Collider>()), Is.True);
            }
            Assert.That(Physics.GetIgnoreCollision(melee.GetComponent<Collider>(), ranged.GetComponent<Collider>()), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContinuousSprintAndTurnStayTogetherWithoutRecovery()
        {
            var controller = input.GetComponent<CharacterController>();
            float largestGap = 0f;
            for (int i = 0; i < 240; i++)
            {
                Vector3 direction = i < 120 ? Vector3.right : Vector3.back;
                input.transform.rotation = Quaternion.LookRotation(direction);
                controller.Move(direction * (6.2f * Time.deltaTime));
                largestGap = Mathf.Max(largestGap, Vector3.Distance(input.transform.position, melee.transform.position),
                    Vector3.Distance(input.transform.position, ranged.transform.position));
                yield return null;
            }
            yield return Frames(240);
            Assert.That(largestGap, Is.LessThan(9f));
            Assert.That(melee.RecoveryCount + ranged.RecoveryCount, Is.Zero);
            AssertFormation(melee);
            AssertFormation(ranged);
        }

        [UnityTest]
        public IEnumerator ReactivationRestoresCollisionExclusionAndFollowing()
        {
            melee.gameObject.SetActive(false);
            yield return Frames(5);
            melee.gameObject.SetActive(true);
            yield return Frames(5);
            Assert.That(Physics.GetIgnoreCollision(melee.GetComponent<Collider>(), input.GetComponent<Collider>()), Is.True);
            Assert.That(Physics.GetIgnoreCollision(melee.GetComponent<Collider>(), ranged.GetComponent<Collider>()), Is.True);
            PlaceLeader(new Vector3(4f, 0f, -8f));
            yield return Frames(240);
            AssertFormation(melee);
        }

        [UnityTest]
        public IEnumerator DyingDuringWindupCancelsCompanionDamagePermanently()
        {
            melee.gameObject.SetActive(false);
            PrepareCombat();
            var enemy = ActivateEnemy(new Vector3(8f, 0f, -8f));
            for (int i = 0; i < 180 && ranged.CurrentTarget == null; i++) yield return null;
            Assert.That(ranged.CurrentTarget, Is.Not.Null);
            ranged.Health.TakeDamage(100f);
            yield return Frames(180);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            ranged.gameObject.SetActive(false);
            ranged.gameObject.SetActive(true);
            yield return Frames(90);
            Assert.That(ranged.Health.IsDead, Is.True);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator BothFollowConvergeAndStayStillAtRest()
        {
            PlaceLeader(new Vector3(7f, 0f, -10f));
            yield return Frames(360);
            AssertFormation(melee);
            AssertFormation(ranged);
            Vector3 a = melee.transform.position;
            Vector3 b = ranged.transform.position;
            yield return Frames(120);
            Assert.That(Vector3.Distance(a, melee.transform.position), Is.LessThan(0.12f));
            Assert.That(Vector3.Distance(b, ranged.transform.position), Is.LessThan(0.12f));
            Assert.That(Vector3.Distance(a, b), Is.GreaterThan(1.1f));
        }

        [UnityTest]
        public IEnumerator FollowNavigatesAroundSolidObstacleWithoutTeleporting()
        {
            PlaceLeader(new Vector3(-16f, 0f, 9f));
            Warp(melee, new Vector3(-11f, 0f, 11f));
            Warp(ranged, new Vector3(-10f, 0f, 10f));
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(melee.transform.position,
                squad.FormationPosition(melee.FormationOffset), NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(path.corners.Length, Is.GreaterThan(2));
            yield return Frames(420);
            AssertFormation(melee);
            AssertFormation(ranged);
            Assert.That(melee.RecoveryCount + ranged.RecoveryCount, Is.Zero);
            Assert.That(Physics.CheckSphere(melee.transform.position + Vector3.up, 0.3f, 1), Is.False);
        }

        [UnityTest]
        public IEnumerator OverlappingMembersSeparateAndDoNotPushLeader()
        {
            Vector3 leaderPosition = input.transform.position;
            Warp(melee, leaderPosition);
            Warp(ranged, leaderPosition);
            yield return Frames(180);
            Assert.That(Vector3.Distance(melee.transform.position, ranged.transform.position), Is.GreaterThan(1.1f));
            Assert.That(Vector3.Distance(input.transform.position, leaderPosition), Is.LessThan(0.01f));
            AssertFormation(melee);
            AssertFormation(ranged);
        }

        [UnityTest]
        public IEnumerator MeleeDetectsApproachesDamagesOncePerCooldownThenReturns()
        {
            ranged.gameObject.SetActive(false);
            PrepareCombat();
            var enemy = ActivateEnemy(new Vector3(8f, 0f, -8f));
            Health victim = enemy.GetComponent<Health>();
            Vector3 start = melee.transform.position;
            yield return UntilDamaged(victim);
            Assert.That(melee.CurrentTarget, Is.EqualTo(victim));
            Assert.That(victim.CurrentHealth, Is.EqualTo(88f));
            Assert.That(Vector3.Distance(start, melee.transform.position), Is.GreaterThan(1f));
            Assert.That(Vector3.Distance(melee.transform.position, enemy.transform.position), Is.LessThan(1.85f));
            yield return Frames(25);
            Assert.That(victim.CurrentHealth, Is.EqualTo(88f));
            victim.TakeDamage(1000f);
            yield return Frames(180);
            Assert.That(melee.CurrentTarget, Is.Null);
            Assert.That(melee.State, Is.EqualTo(CompanionController.CompanionState.Follow));
            AssertFormation(melee);
        }

        [UnityTest]
        public IEnumerator RangedDamagesFromDistanceAndRespectsCooldown()
        {
            melee.gameObject.SetActive(false);
            PrepareCombat();
            var enemy = ActivateEnemy(new Vector3(8f, 0f, -8f));
            Health victim = enemy.GetComponent<Health>();
            yield return UntilDamaged(victim);
            Assert.That(victim.CurrentHealth, Is.EqualTo(92f));
            Assert.That(Vector3.Distance(ranged.transform.position, enemy.transform.position), Is.InRange(3f, 6.2f));
            yield return Frames(50);
            Assert.That(victim.CurrentHealth, Is.EqualTo(92f));
            victim.TakeDamage(1000f);
            yield return Frames(240);
            AssertFormation(ranged);
        }

        [UnityTest]
        public IEnumerator CompanionYieldsLeaderSpaceWhileEngagedInCombat()
        {
            ranged.gameObject.SetActive(false);
            PrepareCombat();
            var enemy = ActivateEnemy(new Vector3(8f, 0f, -8f));
            yield return UntilDamaged(enemy.GetComponent<Health>());
            PlaceLeader(melee.transform.position);
            yield return Frames(150);
            Assert.That(Vector3.Distance(melee.transform.position, input.transform.position), Is.GreaterThan(0.9f));
        }

        [UnityTest]
        public IEnumerator RangedBacksAwayWhenEnemyIsTooClose()
        {
            melee.gameObject.SetActive(false);
            PrepareCombat();
            var enemy = ActivateEnemy(ranged.transform.position + Vector3.forward * 1.5f);
            yield return Frames(120);
            Assert.That(Vector3.Distance(ranged.transform.position, enemy.transform.position), Is.GreaterThan(3f));
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.LessThan(100f));
        }

        [UnityTest]
        public IEnumerator StableTargetIsKeptWhenAnotherEnemyBecomesCloser()
        {
            melee.gameObject.SetActive(false);
            PrepareCombat();
            var first = ActivateEnemy(new Vector3(8f, 0f, -8f));
            yield return UntilDamaged(first.GetComponent<Health>());
            var second = ActivateEnemy(ranged.transform.position + Vector3.right * 3.8f, 1);
            yield return Frames(45);
            Assert.That(ranged.CurrentTarget, Is.EqualTo(first.GetComponent<Health>()));
            Assert.That(second.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator WallsBlockDetectionAndCommittedRangedShot()
        {
            melee.gameObject.SetActive(false);
            PrepareCombat();
            Warp(ranged, new Vector3(8f, 0f, -13f));
            var enemy = ActivateEnemy(new Vector3(8f, 0f, -8f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(8f, 1.5f, -10.5f);
            wall.transform.localScale = new Vector3(12f, 3f, 0.5f);
            Physics.SyncTransforms();
            yield return Frames(25);
            Assert.That(ranged.CurrentTarget, Is.Null);
            wall.SetActive(false);
            for (int i = 0; i < 30 && ranged.CurrentTarget == null; i++) yield return null;
            Assert.That(ranged.CurrentTarget, Is.Not.Null);
            wall.SetActive(true);
            Physics.SyncTransforms();
            yield return Frames(25);
            Assert.That(enemy.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator TooDistantEnemyIsAbandonedAndSquadRegroups()
        {
            PrepareCombat();
            var enemy = ActivateEnemy(new Vector3(8f, 0f, -8f));
            yield return Frames(45);
            PlaceLeader(new Vector3(0f, 0f, -15f));
            yield return Frames(360);
            Assert.That(melee.CurrentTarget, Is.Null);
            Assert.That(ranged.CurrentTarget, Is.Null);
            AssertFormation(melee);
            AssertFormation(ranged);
        }

        [UnityTest]
        public IEnumerator FarRecoveryWaitsThenWarpsToSafeFormation()
        {
            Set(melee, "followSpeed", 0f);
            Set(melee, "catchUpSpeed", 0f);
            Warp(melee, new Vector3(17f, 0f, 17f));
            yield return Frames(90);
            Assert.That(melee.RecoveryCount, Is.Zero);
            yield return Frames(45);
            Assert.That(melee.RecoveryCount, Is.EqualTo(1));
            Assert.That(Vector3.Distance(melee.transform.position, input.transform.position), Is.InRange(1.2f, 5f));
            Assert.That(melee.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
            Assert.That(Physics.CheckSphere(melee.transform.position + Vector3.up, 0.35f, 1), Is.False);
        }

        [UnityTest]
        public IEnumerator StalledRecoveryWorksBelowTeleportDistance()
        {
            Set(melee, "followSpeed", 0f);
            Set(melee, "catchUpSpeed", 0f);
            Warp(melee, new Vector3(8f, 0f, -8f));
            yield return Frames(180);
            Assert.That(melee.RecoveryCount, Is.Zero);
            yield return Frames(90);
            Assert.That(melee.RecoveryCount, Is.EqualTo(1));
            AssertFormation(melee);
        }

        [UnityTest]
        public IEnumerator EnemyCanKillCompanionAndSurvivorKeepsFollowing()
        {
            PrepareCombat();
            melee.Receiver.ClearInvulnerability();
            melee.Health.TakeDamage(80f);
            Vector3 corpsePosition = melee.transform.position;
            var enemy = ActivateEnemy(corpsePosition + Vector3.forward * 1.3f);
            for (int i = 0; i < 180 && !melee.Health.IsDead; i++) yield return null;
            Assert.That(melee.Health.IsDead, Is.True);
            Assert.That(melee.State, Is.EqualTo(CompanionController.CompanionState.Dead));
            Assert.That(melee.GetComponent<NavMeshAgent>().enabled, Is.False);
            Assert.That(melee.GetComponent<Collider>().enabled, Is.False);
            Assert.That(melee.CurrentTarget, Is.Null);
            enemy.gameObject.SetActive(false);
            PlaceLeader(new Vector3(2f, 0f, -14f));
            yield return Frames(300);
            Assert.That(melee.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(corpsePosition, melee.transform.position), Is.LessThan(0.6f));
            AssertFormation(ranged);
        }

        [UnityTest]
        public IEnumerator LeaderDeathStopsBothCompanionsAndAllEnemies()
        {
            PrepareCombat();
            foreach (var enemy in enemies) enemy.gameObject.SetActive(true);
            squad.Leader.Health.TakeDamage(100f);
            yield return Frames(10);
            Vector3 a = melee.transform.position;
            Vector3 b = ranged.transform.position;
            yield return Frames(90);
            Assert.That(melee.CurrentTarget, Is.Null);
            Assert.That(ranged.CurrentTarget, Is.Null);
            Assert.That(Vector3.Distance(a, melee.transform.position), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(b, ranged.transform.position), Is.LessThan(0.01f));
            foreach (var enemy in enemies) Assert.That(enemy.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Idle));
        }

        [UnityTest]
        public IEnumerator ThreeEnemiesFightBothRolesAndSurvivorsReturnToFormation()
        {
            PlaceLeader(new Vector3(-11f, 0f, 6f));
            Warp(melee, new Vector3(-12.6f, 0f, 4f));
            Warp(ranged, new Vector3(-9.1f, 0f, 3f));
            foreach (var enemy in enemies) enemy.gameObject.SetActive(true);
            bool meleeEngaged = false;
            bool rangedEngaged = false;
            for (int i = 0; i < 360; i++)
            {
                meleeEngaged |= melee.CurrentTarget != null;
                rangedEngaged |= ranged.CurrentTarget != null;
                yield return null;
            }
            Assert.That(meleeEngaged && rangedEngaged, Is.True);
            Assert.That(enemies.Sum(e => e.GetComponent<Health>().CurrentHealth), Is.LessThan(300f));
            foreach (var enemy in enemies) enemy.GetComponent<Health>().TakeDamage(1000f);
            yield return Frames(300);
            AssertFormation(melee);
            AssertFormation(ranged);
            Assert.That(Vector3.Distance(melee.transform.position, ranged.transform.position), Is.GreaterThan(1.1f));
        }

        private void PrepareCombat()
        {
            PlaceLeader(new Vector3(8f, 0f, -12f));
            if (melee.gameObject.activeSelf) Warp(melee, new Vector3(6.4f, 0f, -14.2f));
            if (ranged.gameObject.activeSelf) Warp(ranged, new Vector3(9.9f, 0f, -15f));
        }

        private BasicMeleeEnemy ActivateEnemy(Vector3 position, int index = 0)
        {
            var enemy = enemies[index];
            enemy.gameObject.SetActive(true);
            Assert.That(enemy.GetComponent<NavMeshAgent>().Warp(position), Is.True);
            enemy.GetComponent<NavMeshAgent>().speed = 0f;
            Physics.SyncTransforms();
            return enemy;
        }

        private static IEnumerator UntilDamaged(Health victim)
        {
            for (int i = 0; i < 360 && victim.CurrentHealth >= victim.MaxHealth; i++) yield return null;
            Assert.That(victim.CurrentHealth, Is.LessThan(victim.MaxHealth), "Companion must land an attack within six seconds.");
        }

        private void PlaceLeader(Vector3 position)
        {
            var controller = input.GetComponent<CharacterController>();
            controller.enabled = false;
            input.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Warp(CompanionController member, Vector3 position)
        {
            Assert.That(member.GetComponent<NavMeshAgent>().Warp(position), Is.True);
            Physics.SyncTransforms();
        }

        private void AssertFormation(CompanionController member) =>
            Assert.That(Vector3.Distance(member.transform.position, squad.FormationPosition(member.FormationOffset)),
                Is.LessThan(0.95f), member.name + " must settle near its own offset.");

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    }
}
