using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Encounter;
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
    public sealed class EncounterTests
    {
        private SquadController squad;
        private EncounterController encounter;
        private SquadMember[] members;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("PrototypeEncounter");
            Bind();
            yield return Frames(5);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private void Bind()
        {
            squad = Object.FindFirstObjectByType<SquadController>();
            encounter = Object.FindFirstObjectByType<EncounterController>();
            members = squad.Members.ToArray();
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
        }

        // ---------------------------------------------------------------- composition

        [UnityTest]
        public IEnumerator SceneHasThreeWavesOfThreeArchetypesAndIsFirstBuildScene()
        {
            Assert.That(Enumerable.Range(0, SceneManager.sceneCountInBuildSettings).Select(SceneUtility.GetScenePathByBuildIndex).Any(p => p.Contains("PrototypeEncounter")), Is.True);
            Assert.That(encounter.WaveCount, Is.EqualTo(3));
            Assert.That(Archetypes(0), Is.EqualTo(new[] { "PURSUER", "PURSUER" }));
            Assert.That(Archetypes(1), Is.EquivalentTo(new[] { "PURSUER", "PURSUER", "MARKSMAN" }));
            Assert.That(Archetypes(2), Is.EquivalentTo(new[] { "BULWARK", "MARKSMAN", "PURSUER", "PURSUER" }));
            Assert.That(encounter.Waves.SelectMany(w => w.enemies).All(e => !e.gameObject.activeSelf), Is.True);
            Assert.That(Object.FindFirstObjectByType<EncounterHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ThreatIndicator>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(9));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArchetypesAreVisuallyDistinctBeforeTheyAttack()
        {
            var pursuer = Wave(0)[0];
            var marksman = Find("MARKSMAN");
            var bulwark = Find("BULWARK");
            Vector3 small = pursuer.transform.Find("Enemy Visual").localScale;
            Vector3 thin = marksman.transform.Find("Enemy Visual").localScale;
            Vector3 heavy = bulwark.transform.Find("Enemy Visual").localScale;
            Assert.That(heavy.x, Is.GreaterThan(small.x * 1.5f));
            Assert.That(thin.x, Is.LessThan(small.x));
            Assert.That(thin.y, Is.GreaterThan(small.y));
            var materials = new[] { pursuer, marksman, bulwark }.Select(e => e.transform.Find("Enemy Visual").GetComponent<Renderer>().sharedMaterial).ToArray();
            Assert.That(materials.Distinct().Count(), Is.EqualTo(3));
            Assert.That(bulwark.GetComponent<FrontalGuard>(), Is.Not.Null);
            Assert.That(bulwark.transform.Find("Guard Plate"), Is.Not.Null);
            Assert.That(marksman.transform.Find("Barrel"), Is.Not.Null);
            Assert.That(((MarksmanEnemy)marksman).Projectile, Is.Not.Null);
            Assert.That(pursuer.GetComponent<FrontalGuard>(), Is.Null);
            yield return null;
        }

        // ---------------------------------------------------------------- pursuer

        [UnityTest]
        public IEnumerator PursuerStillChasesAndStrikesTheControlledMember()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var pursuer = (BasicMeleeEnemy)Spawn(Wave(0)[0], members[0].transform.position + Vector3.forward * 6f);
            Assert.That(pursuer.Archetype, Is.EqualTo("PURSUER"));
            yield return Until(() => pursuer.State == BasicMeleeEnemy.EnemyState.Chase, 2f);
            yield return Until(() => members[0].Health.CurrentHealth < members[0].Health.MaxHealth, 6f);
            Assert.That(members[0].Health.MaxHealth - members[0].Health.CurrentHealth, Is.EqualTo(18f));
            Assert.That(pursuer.CurrentTarget, Is.EqualTo(members[0].Receiver));
        }

        // ---------------------------------------------------------------- marksman

        [UnityTest]
        public IEnumerator MarksmanAcquiresTheControlledMemberAndKeepsItsDistance()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var marksman = (MarksmanEnemy)Spawn(Find("MARKSMAN"), members[0].transform.position + Vector3.forward * 7f);
            yield return Until(() => marksman.CurrentTarget != null, 2f);
            Assert.That(marksman.CurrentTarget, Is.EqualTo(members[0].Receiver));
            Assert.That(marksman.IsEngaged, Is.True);
            PlaceActive(marksman.transform.position + Vector3.back * 2.2f, Quaternion.identity);
            yield return Until(() => marksman.State == MarksmanEnemy.MarksmanState.Reposition, 3f);
            yield return Until(() => Vector3.Distance(marksman.transform.position, members[0].transform.position) > 4f, 3f);
            Assert.That(marksman.GetComponent<Health>().IsDead, Is.False);
        }

        [UnityTest]
        public IEnumerator MarksmanTelegraphsItsAimThenHitsAStationaryMember()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var marksman = (MarksmanEnemy)Spawn(Find("MARKSMAN"), members[0].transform.position + Vector3.forward * 8f);
            yield return Until(() => marksman.State == MarksmanEnemy.MarksmanState.Aim, 4f);
            Assert.That(marksman.IsAimLineVisible, Is.True);
            Assert.That(marksman.IsPreparingAttack, Is.True);
            Assert.That(marksman.PreparationRemaining, Is.GreaterThan(0.9f));
            float aimStartedAt = Time.time;
            yield return Until(() => marksman.ShotsFired == 1, 2f);
            Assert.That(Time.time - aimStartedAt, Is.GreaterThanOrEqualTo(1.0f));
            Assert.That(marksman.Projectile.InFlight, Is.True);
            yield return Until(() => members[0].Health.CurrentHealth < members[0].Health.MaxHealth, 2f);
            Assert.That(members[0].Health.MaxHealth - members[0].Health.CurrentHealth, Is.EqualTo(14f));
            Assert.That(marksman.Projectile.InFlight, Is.False);
            Assert.That(marksman.Projectile.HitCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MarksmanShotIsAvoidedBySteppingOutOfItsLine()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var marksman = (MarksmanEnemy)Spawn(Find("MARKSMAN"), members[0].transform.position + Vector3.forward * 8f);
            yield return Until(() => marksman.ShotsFired == 1, 6f);
            Vector3 heading = marksman.Projectile.Direction;
            PlaceActive(members[0].transform.position + Vector3.Cross(Vector3.up, heading).normalized * 2.5f, Quaternion.identity);
            yield return Until(() => !marksman.Projectile.InFlight, 3f);
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(members[0].Health.MaxHealth));
            Assert.That(marksman.Projectile.HitCount, Is.Zero, "victim " + (marksman.Projectile.LastVictim != null ? marksman.Projectile.LastVictim.name : "none") +
                " heading " + heading + " marksman " + marksman.transform.position + " active " + members[0].transform.position +
                " m1 " + members[1].transform.position + " m2 " + members[2].transform.position);
        }

        [UnityTest]
        public IEnumerator MarksmanShotIsConsumedByDodgeInvulnerability()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var marksman = (MarksmanEnemy)Spawn(Find("MARKSMAN"), members[0].transform.position + Vector3.forward * 8f);
            yield return Until(() => marksman.ShotsFired == 1, 6f);
            members[0].Receiver.GrantInvulnerability(0f, 2f);
            yield return Until(() => !marksman.Projectile.InFlight, 3f);
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(members[0].Health.MaxHealth));
            Assert.That(marksman.Projectile.LandedCount, Is.EqualTo(1));
            Assert.That(marksman.Projectile.HitCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MarksmanWaitsForItsCooldownBetweenShots()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var marksman = (MarksmanEnemy)Spawn(Find("MARKSMAN"), members[0].transform.position + Vector3.forward * 8f);
            yield return Until(() => marksman.ShotsFired == 1, 6f);
            float first = Time.time;
            yield return Until(() => marksman.ShotsFired == 2, 8f);
            Assert.That(Time.time - first, Is.GreaterThanOrEqualTo(2.4f + 0.5f + 1.1f - 0.05f));
        }

        [UnityTest]
        public IEnumerator CompanionsEngageMarksmanAndGravityFieldGroupsIt()
        {
            encounter.enabled = false;
            var marksman = (MarksmanEnemy)Spawn(Find("MARKSMAN"), members[1].transform.position + Vector3.forward * 5f);
            // Companions ignore disabled brains (V0.7); pin the agent instead so it cannot retreat out of detection range.
            marksman.GetComponent<NavMeshAgent>().speed = 0f;
            yield return Until(() => members[1].Companion.CurrentTarget == marksman.GetComponent<Health>() ||
                members[2].Companion.CurrentTarget == marksman.GetComponent<Health>(), 3f);
            members[1].GetComponent<GravityFieldSkill>().Field.Spawn(marksman.transform.position);
            yield return Until(() => marksman.GetComponent<ComboOpportunity>().Type == ComboOpportunityType.Grouped, 2f);
            Assert.That(marksman.GetComponent<EnemyGravityResponse>().IsSlowed, Is.True);
        }

        // ---------------------------------------------------------------- bulwark

        [UnityTest]
        public IEnumerator BulwarkReducesFrontalHitsAndTakesFullFlankDamage()
        {
            encounter.enabled = false;
            FreezeCompanions();
            Vector3 anchor = new Vector3(-11f, 0f, 9f);
            var bulwark = (BasicMeleeEnemy)Spawn(Find("BULWARK"), anchor, Quaternion.Euler(0f, 180f, 0f));
            bulwark.enabled = false;
            var guard = bulwark.GetComponent<FrontalGuard>();
            var health = bulwark.GetComponent<Health>();
            Assert.That(guard.IsFrontal(anchor + Vector3.back * 2f), Is.True);
            Assert.That(guard.IsFrontal(anchor + Vector3.right * 2f), Is.False);
            Assert.That(guard.IsFrontal(anchor + Vector3.forward * 2f), Is.False);
            PlaceActive(anchor + Vector3.back * 1.4f, Quaternion.identity);
            yield return Frames(2);
            yield return Strike();
            Assert.That(health.MaxHealth - health.CurrentHealth, Is.EqualTo(25f * guard.FrontalMultiplier).Within(0.01f));
            Assert.That(guard.BlockedCount, Is.EqualTo(1));
            float afterFront = health.CurrentHealth;
            PlaceActive(anchor + Vector3.forward * 1.4f, Quaternion.Euler(0f, 180f, 0f));
            yield return Frames(2);
            yield return Strike();
            Assert.That(afterFront - health.CurrentHealth, Is.EqualTo(25f).Within(0.01f));
            Assert.That(guard.FlankCount, Is.EqualTo(1));
            Assert.That(guard.LastFlankedAt, Is.GreaterThan(guard.LastBlockedAt));
        }

        [UnityTest]
        public IEnumerator BulwarkHeavyAttackIsSlowTelegraphedAndHeavy()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var bulwark = (BasicMeleeEnemy)Spawn(Find("BULWARK"), members[0].transform.position + Vector3.forward * 1.6f);
            yield return Until(() => bulwark.IsPreparingAttack, 3f);
            Assert.That(bulwark.PreparationRemaining, Is.GreaterThan(0.8f));
            float windupStart = Time.time;
            yield return Until(() => members[0].Health.CurrentHealth < members[0].Health.MaxHealth, 3f);
            Assert.That(Time.time - windupStart, Is.GreaterThanOrEqualTo(0.85f));
            Assert.That(members[0].Health.MaxHealth - members[0].Health.CurrentHealth, Is.EqualTo(30f));
            Assert.That(bulwark.HitHalfWidth, Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator BulwarkDiesLikeOtherEnemies()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var bulwark = (BasicMeleeEnemy)Spawn(Find("BULWARK"), members[0].transform.position + Vector3.forward * 5f);
            var guard = bulwark.GetComponent<FrontalGuard>();
            Assert.That(guard.Modify(10f, bulwark.transform.position + bulwark.transform.forward), Is.EqualTo(3.5f).Within(0.001f));
            bulwark.GetComponent<Health>().TakeDamage(9999f);
            yield return Frames(2);
            Assert.That(bulwark.State, Is.EqualTo(BasicMeleeEnemy.EnemyState.Dead));
            Assert.That(bulwark.GetComponent<NavMeshAgent>().enabled, Is.False);
            Assert.That(bulwark.GetComponent<CapsuleCollider>().enabled, Is.False);
            Assert.That(guard.Modify(10f, bulwark.transform.position + bulwark.transform.forward), Is.EqualTo(10f));
            yield return Wait(0.7f);
            Assert.That(bulwark.gameObject.activeSelf, Is.False);
        }

        // ---------------------------------------------------------------- encounter flow

        [UnityTest]
        public IEnumerator WaveOneStartsAfterWarningWithOnlyItsEnemies()
        {
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Pending));
            yield return Until(() => encounter.State == EncounterController.EncounterState.Spawning, 3f);
            Assert.That(encounter.WaveIndex, Is.Zero);
            Assert.That(Wave(0).All(e => !e.gameObject.activeSelf), Is.True);
            Assert.That(Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Count(l => l.name.StartsWith("Spawn Marker")), Is.EqualTo(2));
            yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting, 2f);
            Assert.That(Wave(0).All(e => e.gameObject.activeSelf && e.GetComponent<NavMeshAgent>().isOnNavMesh), Is.True);
            Assert.That(Wave(1).Concat(Wave(2)).All(e => !e.gameObject.activeSelf), Is.True);
            Assert.That(encounter.EnemiesAlive, Is.EqualTo(2));
            Assert.That(encounter.Duration, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator NextWaveWaitsForEveryEnemyThenHealsAndSpawnsAfterDelay()
        {
            yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting, 5f);
            foreach (var enemy in Wave(0)) enemy.enabled = false;
            members[0].Health.TakeDamage(30f);
            Kill(Wave(0)[0]);
            yield return Wait(5f);
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Fighting));
            Assert.That(encounter.WaveIndex, Is.Zero);
            Assert.That(encounter.EnemiesAlive, Is.EqualTo(1));
            Kill(Wave(0)[1]);
            yield return Until(() => encounter.State == EncounterController.EncounterState.Cleared, 1f);
            Assert.That(members[0].Health.CurrentHealth, Is.EqualTo(70f + encounter.InterWaveHeal));
            float clearedAt = Time.time;
            Assert.That(Wave(1).All(e => !e.gameObject.activeSelf), Is.True);
            yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting, 6f);
            Assert.That(Time.time - clearedAt, Is.GreaterThanOrEqualTo(3f));
            Assert.That(encounter.WaveIndex, Is.EqualTo(1));
            Assert.That(Wave(1).All(e => e.gameObject.activeSelf), Is.True);
            Assert.That(encounter.EnemiesAlive, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator ClearingAllThreeWavesCompletesTheEncounter()
        {
            for (int w = 0; w < 3; w++)
            {
                yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting && encounter.WaveIndex == w, 8f);
                foreach (var enemy in Wave(w)) Kill(enemy);
                yield return Frames(2);
            }
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Complete));
            Assert.That(encounter.EnemiesAlive, Is.Zero);
            float duration = encounter.Duration;
            Assert.That(duration, Is.GreaterThan(6f));
            yield return Wait(0.5f);
            Assert.That(encounter.Duration, Is.EqualTo(duration));
            Assert.That(members.All(m => !m.Health.IsDead), Is.True);
        }

        [UnityTest]
        public IEnumerator LosingEveryMemberEndsInDefeat()
        {
            yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting, 5f);
            foreach (var member in members) member.Health.TakeDamage(9999f);
            yield return Until(() => encounter.State == EncounterController.EncounterState.Defeated, 1f);
            Assert.That(squad.IsDefeated, Is.True);
            yield return Frames(2);
            Assert.That(squad.GetComponent<TracePlayerInput>().InMenu, Is.True, "result panel frees the cursor");
            Assert.That(encounter.MembersFallen, Is.EqualTo(3));
            Kill(Wave(0)[0]); Kill(Wave(0)[1]);
            yield return Wait(4f);
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Defeated));
        }

        [UnityTest]
        public IEnumerator RestartIntentReloadsTheEncounterFromWaveOne()
        {
            yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting, 5f);
            foreach (var enemy in Wave(0)) Kill(enemy);
            yield return Until(() => encounter.WaveIndex == 1, 6f);
            members[0].Health.TakeDamage(40f);
            var previous = encounter;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Backspace));
            yield return Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => Object.FindFirstObjectByType<EncounterController>() != previous, 2f);
            yield return Frames(3);
            Bind();
            Assert.That(previous == null, Is.True);
            Assert.That(encounter.WaveIndex, Is.LessThanOrEqualTo(0));
            Assert.That(encounter.State, Is.EqualTo(EncounterController.EncounterState.Pending)
                .Or.EqualTo(EncounterController.EncounterState.Spawning));
            Assert.That(members.All(m => m.Health.CurrentHealth == m.Health.MaxHealth), Is.True);
            Assert.That(Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length, Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator StatisticsTrackSwitchesFocusCombosAndDamage()
        {
            yield return Until(() => encounter.State == EncounterController.EncounterState.Fighting, 5f);
            Assert.That(encounter.SwitchCount, Is.Zero);
            Assert.That(squad.SwitchToMember(1), Is.True);
            yield return Frames(2);
            Assert.That(encounter.SwitchCount, Is.EqualTo(1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return Wait(0.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(0.3f);
            Assert.That(encounter.FocusCount, Is.EqualTo(1));
            members[1].Health.TakeDamage(12f);
            yield return Frames(2);
            Assert.That(encounter.DamageTaken, Is.EqualTo(12f));
            var opportunity = members[1].GetComponent<ComboOpportunity>();
            Assert.That(opportunity.Offer(ComboOpportunityType.Protected, 3f, squad), Is.True);
            Assert.That(opportunity.TryConsume(ComboOpportunityType.Protected), Is.True);
            yield return Frames(2);
            Assert.That(encounter.ComboCount, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- readability

        [UnityTest]
        public IEnumerator ThreatIndicatorPointsAtOffscreenMarksmanAimingAtTheControlledMember()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var camera = UnityEngine.Camera.main;
            camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
            camera.aspect = 16f / 9f;
            var indicator = Object.FindFirstObjectByType<ThreatIndicator>();
            var marksman = (MarksmanEnemy)Spawn(Find("MARKSMAN"), members[0].transform.position + Vector3.forward * 8f);
            // Look away from the marksman: it is behind the camera.
            camera.transform.SetPositionAndRotation(members[0].transform.position + new Vector3(0f, 3f, 2f), Quaternion.LookRotation(Vector3.back + Vector3.down * 0.4f));
            yield return Until(() => marksman.State == MarksmanEnemy.MarksmanState.Aim, 4f);
            yield return Frames(2);
            Assert.That(indicator.Hints.Count, Is.EqualTo(1));
            Assert.That(indicator.Hints[0].Text, Does.Contain("MARKSMAN"));
            Assert.That(indicator.Hints[0].Enemy, Is.EqualTo(marksman));
            camera.transform.LookAt(marksman.transform.position + Vector3.up);
            yield return Frames(2);
            if (marksman.State == MarksmanEnemy.MarksmanState.Aim) Assert.That(indicator.Hints.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TacticalOverlayExplainsEveryArchetypeIncludingTheGuard()
        {
            encounter.enabled = false;
            FreezeCompanions();
            var camera = UnityEngine.Camera.main;
            camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
            camera.aspect = 16f / 9f;
            var bulwark = Spawn(Find("BULWARK"), members[0].transform.position + Vector3.forward * 5f + Vector3.left * 1f, Quaternion.Euler(0f, 180f, 0f));
            var marksman = Spawn(Find("MARKSMAN"), members[0].transform.position + Vector3.forward * 6f + Vector3.right * 1.5f);
            bulwark.enabled = false;
            ((MonoBehaviour)marksman).enabled = false;
            // Keep the camera clear of the corner wall at x = -11, z in [-4, 2].
            camera.transform.position = members[0].transform.position + new Vector3(2.5f, 4.5f, -3.5f);
            camera.transform.LookAt(members[0].transform.position + Vector3.forward * 4f);
            var overlay = squad.GetComponent<TacticalOverlay>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return Wait(0.4f);
            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(overlay.VisibleEnemyCount, Is.EqualTo(2));
            var all = encounter.Waves.SelectMany(w => w.enemies).ToList();
            string bulwarkCard = overlay.EnemyInfo(all.IndexOf(bulwark));
            string marksmanCard = overlay.EnemyInfo(all.IndexOf(marksman));
            StringAssert.Contains("BULWARK", bulwarkCard);
            StringAssert.Contains("FLANQUER", bulwarkCard);
            StringAssert.Contains("MARKSMAN", marksmanCard);
            Assert.That(bulwark.transform.Find("Guard Arc").gameObject.activeSelf, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(0.4f);
            Assert.That(bulwark.transform.Find("Guard Arc").gameObject.activeSelf, Is.False);
        }

        // ---------------------------------------------------------------- helpers

        private EnemyBrain[] Wave(int index) => encounter.Waves[index].enemies;
        private string[] Archetypes(int wave) => Wave(wave).Select(e => e.Archetype).ToArray();
        private EnemyBrain Find(string archetype) => encounter.Waves.SelectMany(w => w.enemies).First(e => e.Archetype == archetype);
        private static void Kill(EnemyBrain enemy) => enemy.GetComponent<Health>().TakeDamage(9999f);

        private static EnemyBrain Spawn(EnemyBrain enemy, Vector3 position) => Spawn(enemy, position, Quaternion.identity);
        private static EnemyBrain Spawn(EnemyBrain enemy, Vector3 position, Quaternion rotation)
        {
            enemy.gameObject.SetActive(true);
            var agent = enemy.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(position), Is.True, enemy.name + " must spawn on the NavMesh");
            enemy.transform.rotation = rotation;
            Physics.SyncTransforms();
            return enemy;
        }

        private IEnumerator Strike()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frames(2);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Wait(0.5f);
        }

        private void FreezeCompanions()
        {
            foreach (var member in members)
            {
                if (member.IsPlayerControlled || member.Health.IsDead) continue;
                member.Companion.enabled = false;
                member.Companion.Attack.Cancel();
                var agent = member.GetComponent<NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            }
        }

        private void PlaceActive(Vector3 position, Quaternion rotation)
        {
            var member = squad.ActiveMember;
            var motor = member.GetComponent<ThirdPersonMotor>();
            var body = member.GetComponent<CharacterController>();
            motor.enabled = false; body.enabled = false;
            member.transform.SetPositionAndRotation(position, rotation);
            body.enabled = true; motor.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out after " + timeoutSeconds + " s of game time.");
        }
        private static IEnumerator Wait(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return null; }
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
