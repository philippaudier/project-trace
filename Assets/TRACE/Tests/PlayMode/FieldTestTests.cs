using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Encounter;
using TRACE.Input;
using TRACE.Narrative;
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
    public sealed class FieldTestTests
    {
        private static readonly Vector3 Spawn = new Vector3(10f, 5f, -98f);
        private static readonly (string name, Vector3 position)[] Places =
        {
            ("Exterior Yard", new Vector3(0f, 0f, -35f)), ("Hangar Floor", new Vector3(0f, 0f, 25f)), ("Hangar Balcony", new Vector3(19f, 6f, 25f)),
            ("Lobby", new Vector3(-32f, 0f, 39f)), ("Lab", new Vector3(-50f, 0f, 71f)), ("Service Corridor", new Vector3(-26.5f, 0f, 71f)),
            ("Machine Room", new Vector3(-38f, 0f, 87f)), ("Back Passage", new Vector3(4f, 0f, 88f)), ("Ravine Mid", new Vector3(-86f, 0f, 12f)),
            ("Service Pit", new Vector3(-70f, -3.5f, 66f)), ("High Route", new Vector3(40f, 6f, -10f)), ("Open Exterior", new Vector3(70f, 0f, 20f)),
            ("Relay Platform", new Vector3(70f, 5f, 40f)), ("Observation Tower", new Vector3(88f, 12f, 99f)), ("Trace Chamber", new Vector3(60f, 0f, 115f)),
        };
        private SquadController squad;
        private SquadMember[] members;
        private EncounterDebugStations stations;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime; Time.captureDeltaTime = 0f; Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("FieldTest");
            squad = Object.FindFirstObjectByType<SquadController>();
            members = squad.Members.ToArray();
            stations = Object.FindFirstObjectByType<EncounterDebugStations>();
            stations.ReloadOnReset = false;
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            yield return Wait(0.3f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var focus = squad != null ? squad.GetComponent<TacticalFocus>() : null;
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f; Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator TheSceneIsACompactOrganisedSemiOpenZone()
        {
            var root = GameObject.Find("FieldTest").transform;
            foreach (string path in new[] { "Environment/Terrain", "Environment/Architecture/Exterior", "Environment/Architecture/Interior",
                "Environment/Architecture/Vertical", "Environment/Architecture/TraceTest", "Gameplay/PlayerSpawn", "Gameplay/Encounters", "Debug",
                "Lighting", "Audio", "Navigation" })
                Assert.That(root.Find(path), Is.Not.Null, path);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var renderer in root.Find("Environment").GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.x, Is.InRange(200f, 300f));
            Assert.That(bounds.size.z, Is.InRange(200f, 300f));
            Assert.That(Vector3.Distance(members[0].transform.position, Spawn), Is.LessThan(1f), "starts on the calm overlook");
            Assert.That(Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length, Is.Zero, "no enemy awake before a station is used");
            yield return null;
        }

        [UnityTest] public IEnumerator EveryZoneIsReachableFromTheSpawn()
        {
            Assert.That(NavMesh.SamplePosition(Spawn, out var start, 1.5f, NavMesh.AllAreas), Is.True);
            var path = new NavMeshPath();
            foreach (var (name, position) in Places)
            {
                Assert.That(NavMesh.SamplePosition(position, out var hit, 1.5f, NavMesh.AllAreas), Is.True, name + " on the NavMesh");
                Assert.That(NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, Is.True, name + " reachable");
            }
            yield return null;
        }

        [UnityTest] public IEnumerator TwoRoutesLinkTheYardToTheInteriorAndTheLevelLoops()
        {
            // Hangar route: yard -> hangar -> lobby. Ravine route: ravine -> service pit -> lab. Loop: machine room ->
            // back passage -> open exterior -> high route -> hangar balcony.
            AssertPath(new Vector3(0f, 0f, -35f), new Vector3(0f, 0f, 25f), 80f, "yard to hangar");
            AssertPath(new Vector3(0f, 0f, 25f), new Vector3(-32f, 0f, 39f), 60f, "hangar to lobby");
            AssertPath(new Vector3(-86f, 0f, 12f), new Vector3(-70f, -3.5f, 66f), 120f, "ravine to service pit");
            AssertPath(new Vector3(-70f, -3.5f, 66f), new Vector3(-50f, 0f, 71f), 40f, "service pit to lab");
            AssertPath(new Vector3(-38f, 0f, 87f), new Vector3(70f, 0f, 60f), 150f, "machine room to open exterior through the back passage");
            AssertPath(new Vector3(40f, 6f, -10f), new Vector3(19f, 6f, 25f), 70f, "high route to the hangar balcony");
            yield return null;
        }

        [UnityTest] public IEnumerator CompanionsFollowThroughADoorAndUpStairs()
        {
            // Door: hangar to lobby.
            Place(new Vector3(-17f, 0.08f, 35.5f), new Vector3(-19f, 0.08f, 33f), new Vector3(-19f, 0.08f, 38f));
            yield return Wait(0.3f);
            Teleport(members[0], new Vector3(-32f, 0.08f, 39f));
            yield return Wait(7f);
            for (int i = 1; i < 3; i++)
            {
                Assert.That(members[i].transform.position.x, Is.LessThan(-22f), members[i].name + " came through the door");
                Assert.That(Vector3.Distance(members[i].transform.position, members[0].transform.position), Is.LessThan(8f));
            }
            // Stairs: up to the high route.
            Place(new Vector3(40f, 0.08f, -66f), new Vector3(38f, 0.08f, -68f), new Vector3(42f, 0.08f, -68f));
            yield return Wait(0.3f);
            Teleport(members[0], new Vector3(40f, 6.08f, -38f));
            yield return Wait(9f);
            for (int i = 1; i < 3; i++) Assert.That(members[i].transform.position.y, Is.GreaterThan(4.5f), members[i].name + " climbed the stairs");
        }

        [UnityTest] public IEnumerator DebugStationsWakeEachCompositionOnce()
        {
            Assert.That(stations.GroupCount, Is.EqualTo(4));
            string[][] archetypes =
            {
                new[] { "MARKSMAN", "PURSUER", "PURSUER" }, new[] { "BULWARK", "PURSUER" },
                new[] { "MARKSMAN", "PURSUER", "PURSUER" }, new[] { "BULWARK", "MARKSMAN", "PURSUER", "PURSUER" },
            };
            for (int i = 0; i < 4; i++)
                Assert.That(stations.GroupEnemies(i).Select(e => e.Archetype).OrderBy(a => a), Is.EqualTo(archetypes[i]), stations.GroupName(i));
            Assert.That(stations.Spawn(0), Is.True);
            yield return Wait(0.2f);
            Assert.That(stations.GroupEnemies(0).All(e => e.gameObject.activeInHierarchy), Is.True);
            Assert.That(stations.Spawn(0), Is.False, "once per load");
            Keys(Key.F6); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(stations.IsActive(1), Is.True, "F6 wakes the tight combat");
            Keys(Key.F9); yield return Wait(0.1f); Keys(); yield return Wait(0.1f);
            Assert.That(stations.ResetRequests, Is.EqualTo(1), "F9 resets the zone");
            var terminal = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(t => t.Prompt.Contains("MIXED"));
            Assert.That(terminal.Interact(), Is.True);
            Assert.That(stations.IsActive(3), Is.True, "a terminal wakes its composition");
        }

        [UnityTest] public IEnumerator FocusAndLockWorkInTheOpenYard()
        {
            Place(new Vector3(2f, 0.08f, -56f), new Vector3(0f, 0.08f, -58f), new Vector3(4f, 0.08f, -58f));
            foreach (var other in members) if (!other.IsPlayerControlled) other.Companion.enabled = false;
            stations.Spawn(0);
            yield return Wait(0.5f);
            Keys(Key.Tab); yield return Wait(0.5f);
            var presentation = Object.FindFirstObjectByType<TacticalFocusPresentationController>();
            var readability = Object.FindFirstObjectByType<TacticalReadability>();
            Assert.That(presentation.IsPresenting, Is.True);
            Assert.That(stations.GroupEnemies(0).Count(e => readability.MarkerOf(e) != TacticalReadability.Marker.None), Is.GreaterThan(0));
            Keys(); yield return Wait(0.4f);
            var targeting = squad.GetComponent<TargetingSystem>();
            var enemy = stations.GroupEnemies(0).First(e => e.Archetype == "PURSUER");
            Assert.That(targeting.LockTarget(enemy.GetComponent<Health>()), Is.True, "lock on a yard enemy");
        }

        private void AssertPath(Vector3 from, Vector3 to, float maxLength, string label)
        {
            Assert.That(NavMesh.SamplePosition(from, out var a, 1.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out var b, 1.5f, NavMesh.AllAreas), Is.True, label);
            NavMesh.SamplePosition(from, out a, 1.5f, NavMesh.AllAreas); NavMesh.SamplePosition(to, out b, 1.5f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, Is.True, label);
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Assert.That(length, Is.LessThan(maxLength), label + " is a direct link, not a detour around the map");
        }

        private void Place(params Vector3[] spots)
        {
            for (int i = 0; i < members.Length; i++) Teleport(members[i], spots[i]);
        }

        private static void Teleport(SquadMember member, Vector3 position)
        {
            var body = member.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            var agent = member.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(position);
            member.transform.position = position;
            if (body != null) body.enabled = true;
            Physics.SyncTransforms();
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
