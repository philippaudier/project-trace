using System;
using System.Collections.Generic;
using System.Linq;
using TRACE.AI;
using TRACE.Combat;
using TRACE.Encounter;
using TRACE.Input;
using TRACE.Narrative;
using TRACE.Tactical;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TRACE.Editor
{
    // FieldTest V0.1 (Pass A, greybox): a compact semi-open ORIGIN facility in a rocky valley (~240 x 240 m) to test
    // exploration and combat in varied spaces. Authored from PrototypeEncounter (squad, camera, HUD, audio, Focus,
    // lock already wired); its environment is replaced. Prototype and FirstTrace are never opened. Regenerates the
    // scene on every run (it is generated content), then applies HUD, Audio and Tactical Focus to FieldTest only.
    public static partial class FieldTestSetup
    {
        public const string ScenePath = "Assets/TRACE/Scenes/FieldTest.unity";
        private const string SourcePath = "Assets/TRACE/Scenes/PrototypeEncounter.unity";
        private const string MaterialFolder = "Assets/TRACE/Scenes/Materials/FieldTest";
        private const string NavMeshPath = "Assets/TRACE/Scenes/Navigation/FieldTestNavMesh.asset";
        private const string VolumePath = "Assets/TRACE/Scenes/FieldTestVolume.asset";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Transform environment, architecture, exterior, interior, vertical, traceTest, terrain, props, vegetation, water, decals,
            lighting, volumes, gameplay, encounters, debug, audio, navigation;

        public static readonly Vector3 Spawn = new Vector3(10f, 5f, -98f);
        // Key places, used for the NavMesh connectivity checks and documented in FieldTest-V0.1.md.
        public static readonly (string name, Vector3 position)[] Landmarks =
        {
            ("Spawn Overlook", new Vector3(10f, 5f, -96f)),
            ("Exterior Yard", new Vector3(0f, 0f, -35f)),
            ("Hangar Floor", new Vector3(0f, 0f, 25f)),
            ("Hangar Balcony", new Vector3(19f, 6f, 25f)),
            ("Lobby", new Vector3(-32f, 0f, 39f)),
            ("Office", new Vector3(-47f, 0f, 34f)),
            ("Junction", new Vector3(-40f, 0f, 62f)),
            ("Lab", new Vector3(-50f, 0f, 71f)),
            ("Service Corridor", new Vector3(-26.5f, 0f, 71f)),
            ("Machine Room", new Vector3(-38f, 0f, 87f)),
            ("Back Passage", new Vector3(4f, 0f, 88f)),
            ("Ravine Mid", new Vector3(-86f, 0f, 12f)),
            ("Service Pit", new Vector3(-70f, -3.5f, 66f)),
            ("High Route", new Vector3(40f, 6f, -10f)),
            ("Observation Deck", new Vector3(40f, 6f, 29f)),
            ("Open Exterior", new Vector3(70f, 0f, 20f)),
            ("Relay Platform", new Vector3(70f, 5f, 40f)),
            ("Observation Tower", new Vector3(88f, 12f, 99f)),
            ("Trace Chamber", new Vector3(60f, 0f, 115f)),
        };

        [MenuItem("TRACE/Build FieldTest V0.1 (regenerates the scene)")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourcePath, ScenePath)) throw new InvalidOperationException("Could not copy PrototypeEncounter.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Materials.Clear();
            SquadVisualKit.EnsureFolder(MaterialFolder);
            foreach (string name in new[] { "Environment", "Duel Area", "Combat Practice" })
                foreach (var stale in scene.GetRootGameObjects().Where(g => g.name == name).ToArray()) Object.DestroyImmediate(stale);

            var squad = Object.FindFirstObjectByType<SquadController>();
            var input = squad.GetComponent<TracePlayerInput>();
            var encounter = Object.FindFirstObjectByType<EncounterController>();
            var camera = UnityEngine.Camera.main;

            var root = new GameObject("FieldTest").transform;
            environment = Child(root, "Environment");
            terrain = Child(environment, "Terrain");
            architecture = Child(environment, "Architecture");
            exterior = Child(architecture, "Exterior");
            interior = Child(architecture, "Interior");
            vertical = Child(architecture, "Vertical");
            traceTest = Child(architecture, "TraceTest");
            props = Child(environment, "Props");
            vegetation = Child(environment, "Vegetation");
            water = Child(environment, "Water");
            decals = Child(environment, "Decals");
            gameplay = Child(root, "Gameplay");
            encounters = Child(gameplay, "Encounters");
            debug = Child(root, "Debug");
            lighting = Child(root, "Lighting");
            volumes = Child(root, "Volumes");
            audio = Child(root, "Audio");
            navigation = Child(root, "Navigation");

            BuildTerrain();
            BuildSpawn();
            BuildYard();
            BuildHighRoute();
            BuildHangar();
            BuildInterior();
            BuildServicePit();
            BuildRavine();
            BuildOpenExterior();
            BuildTraceTest();
            Lighting(camera);
            BuildVisualStructures();
            PlaceSquad(squad, camera);
            string bake = BakeNavMesh();

            var groups = BuildEncounters(squad, encounter);
            BuildDebug(squad, input, encounter, groups);
            BuildAmbience();
            Landmark("PlayerSpawn", Spawn, FieldMarker.Kind.Spawn, 2.5f, gameplay.Find("PlayerSpawn") ?? Child(gameplay, "PlayerSpawn"));
            DressVisualPassB(camera);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (EditorBuildSettings.scenes.All(s => s.path != ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE FieldTest V0.1 built (" + bake + ").");

            // Existing passes, on FieldTest only.
            HudSetup.ApplyTo(ScenePath);
            AudioSetup.ApplyTo(ScenePath);
            TacticalFocusPresentationSetup.ApplyTo(ScenePath);
        }

        // ------------------------------------------------------------------ terrain and boundaries

        private static void BuildTerrain()
        {
            // Valley floor in tiles around the sunken service pit (x -97..-56, z 55..85).
            Box("Floor South", terrain, new Vector3(0f, -2f, -32.5f), new Vector3(244f, 4f, 175f), "Ground");
            Box("Floor North", terrain, new Vector3(0f, -2f, 103.5f), new Vector3(244f, 4f, 37f), "Ground");
            Box("Floor West Strip", terrain, new Vector3(-109.5f, -2f, 70f), new Vector3(25f, 4f, 30f), "Ground");
            Box("Floor East Strip", terrain, new Vector3(33f, -2f, 70f), new Vector3(178f, 4f, 30f), "Ground");
            // Perimeter cliffs.
            Box("Cliff North", terrain, new Vector3(0f, 12f, 124f), new Vector3(250f, 28f, 10f), "Rock");
            Box("Cliff South", terrain, new Vector3(0f, 12f, -124f), new Vector3(250f, 28f, 10f), "Rock");
            Box("Cliff West", terrain, new Vector3(-124f, 12f, 0f), new Vector3(10f, 28f, 250f), "Rock");
            Box("Cliff East", terrain, new Vector3(124f, 12f, 0f), new Vector3(10f, 28f, 250f), "Rock");
            // Rock masses that shape the valley and close the gaps between routes.
            Box("Rock Mass West Yard", terrain, new Vector3(-46.25f, 5f, 14f), new Vector3(47.5f, 10f, 32f), "Rock");
            Box("Rock Mass West Interior", terrain, new Vector3(-68f, 5f, 41.5f), new Vector3(24f, 10f, 27f), "Rock");
            Box("Rock Mass Central South", terrain, new Vector3(4f, 6f, 65f), new Vector3(52f, 12f, 42f), "Rock");
            Box("Rock Mass Central North", terrain, new Vector3(4f, 6f, 95f), new Vector3(52f, 12f, 10f), "Rock");
            Box("Rock Mass South East", terrain, new Vector3(82f, 7f, -82f), new Vector3(76f, 14f, 76f), "Rock");
            Box("Rock Mass South West", terrain, new Vector3(-80f, 6f, -82f), new Vector3(76f, 12f, 76f), "Rock");
            Box("Rock Mass East", terrain, new Vector3(110f, 6f, 30f), new Vector3(20f, 12f, 120f), "Rock");
            Box("Rock Mass North West", terrain, new Vector3(-89f, 6f, 102.25f), new Vector3(66f, 12f, 33.5f), "Rock");
            Box("Rock Mass North Interior", terrain, new Vector3(-39f, 6f, 107.6f), new Vector3(34f, 12f, 22.8f), "Rock");
            // Gentle relief: low mounds and rocks; nothing steeper than the locomotion and the agents accept.
            Mound("Mound Yard South", new Vector3(-20f, 0f, -72f), new Vector2(18f, 10f), 1.2f, 0f);
            Mound("Mound Open East", new Vector3(85f, 0f, 10f), new Vector2(16f, 20f), 1.5f, 20f);
            Mound("Mound Open North", new Vector3(92f, 0f, 55f), new Vector2(16f, 12f), 1.6f, -15f);
            Rock("Outcrop Open 1", new Vector3(56f, 1.5f, -5f), new Vector3(5f, 3f, 4f), 25f);
            Rock("Outcrop Open 2", new Vector3(92f, 2f, 48f), new Vector3(7f, 4f, 5f), -35f);
            Rock("Outcrop Open 3", new Vector3(48f, 1.2f, 70f), new Vector3(4f, 2.4f, 6f), 10f);
            Rock("Outcrop South", new Vector3(-16f, 2f, -102f), new Vector3(10f, 4f, 8f), 15f);
            Rock("Outcrop South 2", new Vector3(34f, 2.5f, -100f), new Vector3(9f, 5f, 7f), -20f);
        }

        // ------------------------------------------------------------------ spawn overlook (viewpoint 1)

        private static void BuildSpawn()
        {
            var g = Child(exterior, "Spawn Overlook");
            Box("Overlook Rock", g, new Vector3(10f, 2.5f, -97f), new Vector3(24f, 5f, 16f), "Rock");
            Box("Overlook Deck", g, new Vector3(10f, 5.05f, -95f), new Vector3(14f, 0.1f, 8f), "Concrete");
            Box("Overlook Rail West", g, new Vector3(3f, 5.5f, -95f), new Vector3(0.15f, 1f, 8f), "Metal");
            Box("Overlook Rail East", g, new Vector3(17f, 5.5f, -95f), new Vector3(0.15f, 1f, 8f), "Metal");
            Box("Overlook Sign", g, new Vector3(14f, 6.2f, -99f), new Vector3(3f, 1.2f, 0.15f), "ORIGIN");
            Box("Overlook Sign Band", g, new Vector3(14f, 5.75f, -98.9f), new Vector3(3f, 0.12f, 0.17f), "Hazard");
            Ramp("Overlook Ramp", g, new Vector3(10f, 5f, -89f), new Vector3(10f, 0f, -72f), 6f, "Concrete");
            Landmark("Viewpoint Spawn", new Vector3(10f, 5f, -92f), FieldMarker.Kind.Viewpoint, 3f, g);
        }

        // ------------------------------------------------------------------ zone A: exterior yard

        private static void BuildYard()
        {
            var g = Child(exterior, "A Exterior Yard");
            // Boundaries: west wall with the ravine entrance, east wall open toward the open exterior at its north end.
            Box("Yard Wall West S", g, new Vector3(-36f, 2f, -49.5f), new Vector3(1f, 4f, 31f), "Concrete");
            Box("Yard Wall West N", g, new Vector3(-36f, 2f, -14f), new Vector3(1f, 4f, 24f), "Concrete");
            Box("Yard Wall East", g, new Vector3(36f, 2f, -40.5f), new Vector3(1f, 4f, 49f), "Concrete");
            Box("Yard Wall Cap West", g, new Vector3(-36f, 4.1f, -49.5f), new Vector3(1.1f, 0.2f, 31f), "Hazard");
            // Buildings.
            Box("Warehouse", g, new Vector3(-22f, 4f, -50f), new Vector3(16f, 8f, 12f), "Concrete");
            Box("Warehouse Roof Band", g, new Vector3(-22f, 8.1f, -50f), new Vector3(16.2f, 0.2f, 12.2f), "Dark");
            Box("Warehouse ORIGIN Panel", g, new Vector3(-22f, 5.5f, -43.9f), new Vector3(4f, 2f, 0.1f), "ORIGIN");
            Box("Site Office", g, new Vector3(25f, 3f, -22f), new Vector3(12f, 6f, 10f), "Concrete");
            Box("Site Office Stripe", g, new Vector3(25f, 4.8f, -16.95f), new Vector3(12f, 0.3f, 0.1f), "Hazard");
            // Containers, one stack the Marksman uses.
            Container("Container 1", g, new Vector3(-6f, 0f, -45f), 0f, "Metal");
            Container("Container 2", g, new Vector3(5f, 0f, -30f), 90f, "ORIGIN");
            Container("Container 3 (stacked)", g, new Vector3(5f, 2.6f, -30f), 90f, "Metal");
            Container("Container 4", g, new Vector3(16f, 0f, -52f), 90f, "Dark");
            Container("Container 5", g, new Vector3(-14f, 0f, -24f), 15f, "Metal");
            // Truck (cab + bed), pipe rack, low cover, broken walls, a small platform.
            Box("Truck Cab", g, new Vector3(20f, 1.4f, -38f), new Vector3(2.6f, 2.8f, 3f), "ORIGIN");
            Box("Truck Bed", g, new Vector3(20f, 1.1f, -42.5f), new Vector3(2.6f, 1.6f, 6f), "Dark");
            for (int i = 0; i < 4; i++) Box("Pipe Rack Leg " + i, g, new Vector3(-30f, 1.5f, -36f + i * 7f), new Vector3(0.4f, 3f, 0.4f), "Metal");
            Pipe("Pipe Rack Pipe A", g, new Vector3(-30f, 3f, -37f), new Vector3(-30f, 3f, -14f), 0.35f, "Metal");
            Pipe("Pipe Rack Pipe B", g, new Vector3(-30.8f, 2.4f, -37f), new Vector3(-30.8f, 2.4f, -14f), 0.25f, "Hazard");
            Box("Broken Wall 1", g, new Vector3(0f, 1.5f, -14f), new Vector3(10f, 3f, 0.5f), "Concrete");
            Box("Broken Wall 2", g, new Vector3(-12f, 0.9f, -34f), new Vector3(6f, 1.8f, 0.5f), "Concrete");
            Box("Broken Wall 3", g, new Vector3(14f, 0.6f, -12f), new Vector3(5f, 1.2f, 0.5f), "Concrete");
            for (int i = 0; i < 5; i++) Box("Crate " + i, g, new Vector3(-2f + i * 2.2f, 0.5f, -58f + (i % 2) * 1.3f), new Vector3(1.2f, 1f, 1.2f), "Dark");
            Box("Yard Platform", g, new Vector3(-16f, 0.75f, -6f), new Vector3(6f, 1.5f, 6f), "Concrete");
            Ramp("Yard Platform Ramp", g, new Vector3(-16f, 0f, -13f), new Vector3(-16f, 1.5f, -9f), 3f, "Metal");
            // Apron between yard and hangar.
            Box("Apron Line", g, new Vector3(0f, 0.01f, 4f), new Vector3(16f, 0.02f, 0.3f), "Hazard");
            Landmark("Route Yard to Ravine", new Vector3(-37f, 0f, -30f), FieldMarker.Kind.Route, 2f, g);
            Landmark("Route Yard to Open Exterior", new Vector3(36f, 0f, -8f), FieldMarker.Kind.Route, 2f, g);
        }

        // ------------------------------------------------------------------ zone D: high route (viewpoint 2, loop)

        private static void BuildHighRoute()
        {
            var g = Child(vertical, "D High Route");
            Box("Catwalk", g, new Vector3(40f, 5.85f, -11f), new Vector3(3f, 0.3f, 72f), "Metal");
            // The west rail opens where the hangar bridge joins (z 20.5-23.5).
            Box("Catwalk Rail West S", g, new Vector3(38.55f, 6.5f, -13.35f), new Vector3(0.1f, 1f, 67.3f), "Hazard");
            Box("Catwalk Rail West N", g, new Vector3(38.55f, 6.5f, 24.35f), new Vector3(0.1f, 1f, 1.3f), "Hazard");
            Box("Catwalk Rail East", g, new Vector3(41.45f, 6.5f, -11f), new Vector3(0.1f, 1f, 72f), "Hazard");
            for (int i = 0; i < 8; i++) Box("Catwalk Pillar " + i, g, new Vector3(40f, 2.85f, -44f + i * 9.5f), new Vector3(0.5f, 5.7f, 0.5f), "Dark");
            Stairs("Catwalk Stairs South", g, new Vector3(40f, 0f, -63f), new Vector3(40f, 6f, -47f), 3f, "Metal");
            Box("Observation Deck", g, new Vector3(40f, 5.85f, 29f), new Vector3(8f, 0.3f, 8f), "Metal");
            Box("Deck Rail North", g, new Vector3(40f, 6.5f, 32.95f), new Vector3(8f, 1f, 0.1f), "Hazard");
            for (int i = 0; i < 3; i++) Box("Deck Pillar " + i, g, new Vector3(37f + i * 3f, 2.85f, 32f), new Vector3(0.5f, 5.7f, 0.5f), "Dark");
            Stairs("Deck Stairs East", g, new Vector3(57f, 0f, 29f), new Vector3(44f, 6f, 29f), 3f, "Metal");
            // Bridge into the hangar balcony: the shortcut that closes the yard / hangar / high route loop.
            Box("Hangar Bridge", g, new Vector3(31f, 5.85f, 22f), new Vector3(18f, 0.3f, 3f), "Metal");
            Box("Hangar Bridge Rail S", g, new Vector3(31f, 6.5f, 20.55f), new Vector3(18f, 1f, 0.1f), "Hazard");
            Box("Hangar Bridge Rail N", g, new Vector3(31f, 6.5f, 23.45f), new Vector3(18f, 1f, 0.1f), "Hazard");
            Landmark("Viewpoint Observation Deck", new Vector3(40f, 6f, 29f), FieldMarker.Kind.Viewpoint, 3f, g);
            Landmark("Link Bridge to Hangar Balcony", new Vector3(22f, 6f, 22f), FieldMarker.Kind.NavigationLink, 1.5f, g);
        }

        // ------------------------------------------------------------------ zone E: hangar (dark interior / bright yard)

        private static void BuildHangar()
        {
            var g = Child(interior, "E Hangar");
            Room("Hangar", g, -22f, 22f, 8f, 42f, 14f, "Metal", "Dark", new[]
            {
                Door('S', 0f, 16f, 10f), Door('W', 35.5f, 3f, 3f), Door('E', 22f, 3f, 3f, 6f),
            });
            Box("Hangar Balcony", g, new Vector3(19f, 5.8f, 25f), new Vector3(6f, 0.4f, 30f), "Metal");
            // Open where the stairs arrive (z 36.5-39.5).
            Box("Balcony Rail", g, new Vector3(16.05f, 6.5f, 23f), new Vector3(0.1f, 1f, 26f), "Hazard");
            Stairs("Balcony Stairs", g, new Vector3(4f, 0f, 38f), new Vector3(16f, 6f, 38f), 3f, "Metal");
            for (int i = 0; i < 4; i++) Box("Hangar Pillar " + i, g, new Vector3(i % 2 == 0 ? -10f : 8f, 7f, 16f + (i / 2) * 14f), new Vector3(0.8f, 14f, 0.8f), "Dark");
            Box("Hangar Machine", g, new Vector3(-12f, 1.5f, 34f), new Vector3(6f, 3f, 4f), "ORIGIN");
            Box("Hangar Machine Band", g, new Vector3(-12f, 2.2f, 31.95f), new Vector3(6f, 0.3f, 0.1f), "Hazard");
            Container("Hangar Container", g, new Vector3(12f, 0f, 26f), 0f, "Dark");
            for (int i = 0; i < 3; i++) Box("Hangar Crate " + i, g, new Vector3(-4f + i * 1.4f, 0.6f, 12f), new Vector3(1.2f, 1.2f, 1.2f), "Metal");
            Light("Hangar Light 1", new Vector3(-10f, 12f, 18f), new Color(0.85f, 0.9f, 1f), 15f, 18f);
            Light("Hangar Light 2", new Vector3(10f, 12f, 32f), new Color(0.85f, 0.9f, 1f), 15f, 18f);
            Light("Hangar Balcony Light", new Vector3(19f, 9f, 25f), new Color(1f, 0.78f, 0.5f), 10f, 9f);
            // Lower work lights: the hangar stays darker than the yard, but the fight on its floor stays readable.
            Light("Hangar Work Light W", new Vector3(-12f, 7f, 30f), new Color(0.9f, 0.92f, 1f), 12f, 16f);
            Light("Hangar Work Light E", new Vector3(6f, 7f, 18f), new Color(0.9f, 0.92f, 1f), 12f, 16f);
        }

        // ------------------------------------------------------------------ zone C: main interior

        private static void BuildInterior()
        {
            var g = Child(interior, "C Main Interior");
            // Lobby (pillars for the camera), shares its east wall with the hangar.
            Room("Lobby", g, -42f, -22f, 30f, 48f, 6f, "Concrete", "Dark", new[]
            {
                Door('W', 34.5f, 2.4f, 2.6f), Door('W', 43.5f, 2.4f, 2.6f), Door('N', -32f, 4f, 3.2f), Skip('E'),
            });
            Box("Lobby Desk", g, new Vector3(-30f, 0.6f, 34f), new Vector3(5f, 1.2f, 1.2f), "ORIGIN");
            for (int i = 0; i < 4; i++) Box("Lobby Pillar " + i, g, new Vector3(i % 2 == 0 ? -37f : -27f, 3f, 36f + (i / 2) * 7f), new Vector3(0.9f, 6f, 0.9f), "Concrete");
            Room("Office South", g, -52f, -42f, 30f, 39f, 3.5f, "Concrete", "Dark", new[] { Skip('E') });
            Room("Office North", g, -52f, -42f, 39f, 48f, 3.5f, "Concrete", "Dark", new[] { Skip('E'), Skip('S') });
            Box("Office Desk", g, new Vector3(-48f, 0.4f, 34f), new Vector3(2f, 0.8f, 1f), "Dark");
            // Corridor to the T-junction, then lab (west), service corridor (low ceiling) and machine room.
            Room("Corridor", g, -34f, -30f, 48f, 60f, 3.5f, "Concrete", "Dark", new[] { Skip('S'), Skip('N') });
            Room("Junction", g, -56f, -22f, 60f, 64f, 3.5f, "Concrete", "Dark", new[]
            {
                Door('S', -32f, 4f, 3.2f), Door('N', -50f, 2.4f, 2.6f), Door('N', -26.5f, 3f, 2.8f),
            });
            Room("Lab", g, -56f, -44f, 64f, 78f, 4f, "ORIGIN", "Dark", new[] { Door('W', 71.5f, 3f, 2.8f), Skip('S'), Skip('N') });
            Box("Lab Bench 1", g, new Vector3(-50f, 0.5f, 68f), new Vector3(6f, 1f, 1.2f), "Metal");
            Box("Lab Bench 2", g, new Vector3(-50f, 0.5f, 74f), new Vector3(6f, 1f, 1.2f), "Metal");
            Room("Service Corridor", g, -28f, -25f, 64f, 78f, 3f, "Concrete", "Dark", new[] { Skip('S'), Skip('N') });
            Pipe("Service Pipe", g, new Vector3(-27.6f, 2.6f, 64.5f), new Vector3(-27.6f, 2.6f, 77.5f), 0.18f, "Hazard");
            Room("Machine Room", g, -56f, -22f, 78f, 96f, 9f, "Metal", "Dark", new[]
            {
                Door('S', -50f, 2.4f, 2.6f), Door('S', -26.5f, 3f, 2.8f), Door('E', 88f, 4f, 4f),
            });
            Box("Generator A", g, new Vector3(-46f, 2f, 88f), new Vector3(6f, 4f, 4f), "ORIGIN");
            Box("Generator B", g, new Vector3(-34f, 1.5f, 91f), new Vector3(5f, 3f, 3f), "Dark");
            Box("Generator Band", g, new Vector3(-46f, 3.2f, 85.95f), new Vector3(6f, 0.3f, 0.1f), "Hazard");
            Pipe("Machine Pipe", g, new Vector3(-55f, 6f, 83f), new Vector3(-23f, 6f, 83f), 0.4f, "Metal");
            Light("Lobby Light 1", new Vector3(-36f, 5.4f, 35f), new Color(0.8f, 0.88f, 1f), 11f, 12f);
            Light("Lobby Light 2", new Vector3(-28f, 5.4f, 44f), new Color(0.8f, 0.88f, 1f), 11f, 12f);
            Light("Office Light", new Vector3(-47f, 3f, 39f), new Color(1f, 0.8f, 0.55f), 6f, 8f);
            Light("Corridor Light", new Vector3(-32f, 3f, 54f), new Color(0.8f, 0.88f, 1f), 7f, 9f);
            Light("Junction Light", new Vector3(-40f, 3f, 62f), new Color(0.8f, 0.88f, 1f), 7f, 12f);
            Light("Lab Light", new Vector3(-50f, 3.5f, 71f), new Color(0.6f, 0.95f, 0.9f), 9f, 11f);
            Light("Service Light", new Vector3(-26.5f, 2.6f, 71f), new Color(1f, 0.6f, 0.35f), 5f, 8f);
            Light("Machine Light 1", new Vector3(-44f, 8f, 84f), new Color(0.85f, 0.9f, 1f), 12f, 16f);
            Light("Machine Light 2", new Vector3(-30f, 8f, 92f), new Color(0.85f, 0.9f, 1f), 12f, 16f);
            // Back passage: a narrow canyon from the machine room to the open exterior (the second loop).
            var passage = Child(exterior, "Back Passage");
            Box("Back Passage Shutter Frame", passage, new Vector3(-21.6f, 4.5f, 88f), new Vector3(0.3f, 1f, 5f), "Hazard");
            Landmark("Link Machine Room to Open Exterior", new Vector3(4f, 0f, 88f), FieldMarker.Kind.Route, 2f, passage);
        }

        // ------------------------------------------------------------------ zone F: lower service pit

        private static void BuildServicePit()
        {
            var g = Child(interior, "F Lower Service Pit");
            const float floorY = -3.5f;
            Box("Pit Floor", g, new Vector3(-69f, floorY - 0.5f, 70f), new Vector3(22f, 1f, 24f), "Dark");
            // Rims (ground level, deep enough to be the pit walls) and the trench of the ravine ramp.
            Box("Pit Rim South", g, new Vector3(-76.5f, -2f, 56.5f), new Vector3(41f, 4f, 3f), "Concrete");
            Box("Pit Rim North", g, new Vector3(-76.5f, -2f, 83.5f), new Vector3(41f, 4f, 3f), "Concrete");
            Box("Pit Rim East", g, new Vector3(-57f, -2f, 70f), new Vector3(2f, 4f, 30f), "Concrete");
            Box("Ravine End Floor", g, new Vector3(-94.5f, -2f, 70f), new Vector3(5f, 4f, 30f), "Ground");
            Box("Trench Fill South", g, new Vector3(-86f, -2f, 62.5f), new Vector3(12f, 4f, 9f), "Concrete");
            Box("Trench Fill North", g, new Vector3(-86f, -2f, 77.5f), new Vector3(12f, 4f, 9f), "Concrete");
            Ramp("Pit Ramp From Ravine", g, new Vector3(-92f, 0f, 70f), new Vector3(-80f, floorY, 70f), 6f, "Concrete");
            Stairs("Pit Stairs To Lab", g, new Vector3(-70f, floorY, 71.5f), new Vector3(-58f, 0f, 71.5f), 3f, "Metal");
            // Enclosure above ground: the pit is reached only from the ravine and the lab.
            // Stops short of the ravine channel (x < -92), which runs past the pit's south-west corner.
            Box("Pit Enclosure South", g, new Vector3(-74f, 5f, 55f), new Vector3(36f, 10f, 1f), "Rock");
            Box("Pit Enclosure North", g, new Vector3(-74f, 5f, 85f), new Vector3(36f, 10f, 1f), "Rock");
            Box("Pit Rail East", g, new Vector3(-58.2f, 0.5f, 62f), new Vector3(0.1f, 1f, 8f), "Hazard");
            Box("Pit Rail East N", g, new Vector3(-58.2f, 0.5f, 78f), new Vector3(0.1f, 1f, 8f), "Hazard");
            // Low canopy over the power room half: a low ceiling to test the camera.
            Box("Power Room Canopy", g, new Vector3(-74f, -0.4f, 74f), new Vector3(12f, 0.4f, 16f), "Metal");
            for (int i = 0; i < 2; i++) Box("Canopy Pillar " + i, g, new Vector3(-68.5f, -2f, 67f + i * 14f), new Vector3(0.4f, 2.8f, 0.4f), "Dark");
            Box("Power Cabinet", g, new Vector3(-78f, floorY + 1.1f, 78f), new Vector3(1.5f, 2.2f, 4f), "ORIGIN");
            // Along the north wall: across the ramp landing it sat under agent height and closed the ravine route.
            Pipe("Pit Pipe 1", g, new Vector3(-79f, floorY + 2.2f, 81.5f), new Vector3(-60f, floorY + 2.2f, 81.5f), 0.25f, "Metal");
            Pipe("Pit Pipe 2", g, new Vector3(-59f, floorY + 0.4f, 59f), new Vector3(-59f, floorY + 0.4f, 66f), 0.3f, "Hazard");
            Light("Pit Light 1", new Vector3(-74f, -1f, 66f), new Color(1f, 0.7f, 0.45f), 8f, 10f);
            Light("Pit Light 2", new Vector3(-74f, -1f, 79f), new Color(1f, 0.7f, 0.45f), 8f, 10f);
        }

        // ------------------------------------------------------------------ zone B: narrow ravine (second route)

        private static void BuildRavine()
        {
            var g = Child(exterior, "B Ravine");
            // Polyline from the yard's west gate to the pit ramp; widths 4-10 m, bends and two side pockets.
            Vector3[] points =
            {
                new Vector3(-36f, 0f, -30f), new Vector3(-55f, 0f, -28f), new Vector3(-72f, 0f, -20f), new Vector3(-80f, 0f, 0f),
                new Vector3(-90f, 0f, 20f), new Vector3(-86f, 0f, 40f), new Vector3(-96f, 0f, 52f), new Vector3(-97f, 0f, 70f),
                new Vector3(-80f, 0f, 70f),
            };
            float[] widths = { 8f, 6f, 5f, 9f, 6f, 4.5f, 7f, 6f };
            int[] pockets = { 3, 5 };
            int count = points.Length - 1;
            Vector3 Dir(int i) => (points[i + 1] - points[i]).normalized;
            Vector3 Normal(int i) => Vector3.Cross(Vector3.up, Dir(i));
            float Half(int i) => widths[i] * 0.5f + 2f;
            // Wall centre lines of two consecutive segments on one side meet at a single point: ending both walls
            // there closes the outside of the bend and keeps the inside of the bend out of the next channel.
            Vector3 Corner(int v, int side)
            {
                Vector3 p1 = points[v] + Normal(v - 1) * Half(v - 1) * side, d1 = Dir(v - 1);
                Vector3 p2 = points[v] + Normal(v) * Half(v) * side, d2 = Dir(v);
                float denom = d1.x * d2.z - d1.z * d2.x;
                if (Mathf.Abs(denom) < 0.01f) return (p1 + p2) * 0.5f;
                float t = ((p2.x - p1.x) * d2.z - (p2.z - p1.z) * d2.x) / denom;
                return p1 + d1 * t;
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Dir(i), normal = Normal(i);
                float half = Half(i);
                for (int side = -1; side <= 1; side += 2)
                {
                    // Ends: the corner points (1 m overlap for the wall thickness), or 1 m past the open ends.
                    Vector3 s0 = (i == 0 ? points[0] + normal * half * side : Corner(i, side)) - dir;
                    Vector3 s1 = (i == count - 1 ? points[count] + normal * half * side : Corner(i + 1, side)) + dir;
                    float length = Vector3.Dot(s1 - s0, dir);
                    if (length <= 0.5f) continue;
                    bool pocket = pockets.Contains(i) && side > 0;
                    if (!pocket) { RockWall($"Ravine Wall {i} {(side < 0 ? "L" : "R")}", g, (s0 + s1) * 0.5f, dir, length, 10f + (i % 3) * 2f); continue; }
                    // A recess on one side: the wall steps out for the middle third.
                    float third = length / 3f;
                    RockWall($"Ravine Wall {i} R1", g, s0 + dir * third * 0.5f, dir, third + 0.5f, 11f);
                    RockWall($"Ravine Wall {i} R3", g, s1 - dir * third * 0.5f, dir, third + 0.5f, 11f);
                    RockWall($"Ravine Pocket {i}", g, (s0 + s1) * 0.5f + normal * 5f, dir, third + 1f, 11f);
                    RockWall($"Ravine Pocket {i} Cap A", g, s0 + dir * third + normal * 2.5f, normal, 6f, 11f);
                    RockWall($"Ravine Pocket {i} Cap B", g, s1 - dir * third + normal * 2.5f, normal, 6f, 11f);
                }
            }
            // The strip between the ravine's last bend and the north rock stays solid: no way around the ravine.
            Box("Rock Mass Ravine North", g, new Vector3(-101f, 5f, 81.25f), new Vector3(42f, 10f, 8.5f), "Rock");
            Rock("Ravine Boulder 1", new Vector3(-82f, 0.8f, 6f), new Vector3(2f, 1.6f, 1.8f), 30f, g);
            Rock("Ravine Boulder 2", new Vector3(-91f, 0.6f, 28f), new Vector3(1.6f, 1.2f, 2f), -20f, g);
            Landmark("Route Ravine to Pit", new Vector3(-92f, 0f, 70f), FieldMarker.Kind.Route, 2f, g);
        }

        // ------------------------------------------------------------------ open exterior, relay platform, tower

        private static void BuildOpenExterior()
        {
            var g = Child(exterior, "Open Exterior");
            var v = Child(vertical, "Relay Platform and Tower");
            Box("Relay Platform", v, new Vector3(70f, 2.5f, 40f), new Vector3(12f, 5f, 8f), "Concrete");
            Box("Relay Platform Edge", v, new Vector3(70f, 5.05f, 36.1f), new Vector3(12f, 0.1f, 0.2f), "Hazard");
            Box("Relay Mast", v, new Vector3(74f, 9f, 42f), new Vector3(0.6f, 8f, 0.6f), "Metal");
            Stairs("Relay Stairs", v, new Vector3(70f, 0f, 55f), new Vector3(70f, 5f, 44f), 3f, "Metal");
            // Observation tower (viewpoint 3): two flights and a landing.
            Box("Tower Core", v, new Vector3(92f, 5.85f, 99f), new Vector3(12f, 11.7f, 10f), "Concrete");
            Box("Tower Top", v, new Vector3(86f, 11.85f, 99f), new Vector3(24f, 0.3f, 10f), "Metal");
            Box("Tower Rail S", v, new Vector3(89.5f, 12.5f, 94.05f), new Vector3(17f, 1f, 0.1f), "Hazard");
            Box("Tower Landing", v, new Vector3(80f, 5.85f, 78f), new Vector3(4f, 0.3f, 4f), "Metal");
            Box("Tower Landing Post", v, new Vector3(80f, 2.85f, 78f), new Vector3(0.5f, 5.7f, 0.5f), "Dark");
            Box("Tower Top Post", v, new Vector3(76f, 5.85f, 99f), new Vector3(0.6f, 11.7f, 0.6f), "Dark");
            Stairs("Tower Flight 1", v, new Vector3(80f, 0f, 62f), new Vector3(80f, 6f, 76f), 3f, "Metal");
            Stairs("Tower Flight 2", v, new Vector3(80f, 6f, 80f), new Vector3(80f, 12f, 94f), 3f, "Metal");
            Landmark("Viewpoint Tower", new Vector3(88f, 12f, 99f), FieldMarker.Kind.Viewpoint, 3f, v);
            // Field props: a pylon, a pipeline on supports, a cable spool, scattered blocks.
            Box("Pylon", g, new Vector3(56f, 6f, 54f), new Vector3(1f, 12f, 1f), "Metal");
            Pipe("Field Pipeline", g, new Vector3(46f, 1.2f, 0f), new Vector3(46f, 1.2f, 50f), 0.5f, "Metal");
            for (int i = 0; i < 5; i++) Box("Pipeline Support " + i, g, new Vector3(46f, 0.5f, 2f + i * 11f), new Vector3(1.6f, 1f, 0.6f), "Concrete");
            Box("Field Block 1", g, new Vector3(64f, 1f, 12f), new Vector3(4f, 2f, 3f), "Concrete");
            Box("Field Block 2", g, new Vector3(80f, 0.75f, 28f), new Vector3(3f, 1.5f, 5f), "Dark");
            Box("Field Block 3", g, new Vector3(58f, 1.2f, 84f), new Vector3(6f, 2.4f, 3f), "Concrete");
            Box("Field Marker ORIGIN", g, new Vector3(66f, 2f, -2f), new Vector3(0.3f, 4f, 2f), "ORIGIN");
        }

        // ------------------------------------------------------------------ trace anomaly prototype

        private static void BuildTraceTest()
        {
            // A compact hut against the north rock: inside, a corridor and a chamber far deeper than the hut looks.
            var g = traceTest;
            Room("Trace Hut", g, 57f, 63f, 100f, 104f, 3.2f, "Concrete", "Dark", new[] { Door('S', 60f, 2f, 2.6f), Door('N', 60f, 3f, 2.8f) });
            Room("Trace Corridor", g, 58.5f, 61.5f, 104f, 112f, 3f, "Concrete", "Dark", new[] { Skip('S'), Skip('N') });
            Room("Trace Chamber", g, 56f, 64f, 112f, 119f, 4f, "Dark", "Dark", new[] { Door('S', 60f, 3f, 2.8f) });
            Box("Trace Rock West", g, new Vector3(53f, 6f, 109.5f), new Vector3(6f, 12f, 19f), "Rock");
            Box("Trace Rock East", g, new Vector3(67f, 6f, 109.5f), new Vector3(6f, 12f, 19f), "Rock");
            Box("Trace Rock Cap", g, new Vector3(60f, 8f, 110f), new Vector3(8f, 8f, 18f), "Rock");
            Box("Trace Plinth", g, new Vector3(60f, 0.4f, 116f), new Vector3(1.2f, 0.8f, 1.2f), "ORIGIN");
            Light("Trace Glow", new Vector3(60f, 2.5f, 116f), new Color(0.55f, 0.85f, 1f), 3.6f, 7f);
            Landmark("Trace Anomaly (inside deeper than outside)", new Vector3(60f, 0f, 108f), FieldMarker.Kind.Zone, 3f, g);
        }

        // ------------------------------------------------------------------ lighting

        private static void Lighting(UnityEngine.Camera camera)
        {
            var sun = GameObject.Find("Sun").GetComponent<Light>();
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.transform.SetParent(lighting, true);
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.66f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.34f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.12f);
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 400f);
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
                var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.18f); vignette.smoothness.Override(0.45f);
                var color = profile.Add<ColorAdjustments>(true); color.saturation.Override(-8f); color.contrast.Override(6f);
                var tonemap = profile.Add<Tonemapping>(true); tonemap.mode.Override(TonemappingMode.ACES);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            }
            var volume = new GameObject("Post Volume").AddComponent<Volume>();
            volume.transform.SetParent(volumes, false);
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static string BakeNavMesh()
        {
            // Physics colliders of the Default layer inside the map volume (characters and enemies are elsewhere or
            // dormant; trigger volumes are not walkable surfaces).
            var surface = navigation.gameObject.AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 3f, 0f);
            surface.size = new Vector3(244f, 22f, 244f);
            surface.layerMask = 1;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("FieldTest NavMesh failed to bake.");
            SquadVisualKit.EnsureFolder("Assets/TRACE/Scenes/Navigation");
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null) AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
            // Every landmark is on the mesh and reachable from the spawn.
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(Spawn, out var start, 1.5f, NavMesh.AllAreas)) throw new InvalidOperationException("Spawn is off the NavMesh.");
            var unreachable = new List<string>();
            foreach (var (name, position) in Landmarks)
            {
                if (!NavMesh.SamplePosition(position, out var hit, 1.5f, NavMesh.AllAreas)) { unreachable.Add(name + " (no mesh)"); continue; }
                if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    unreachable.Add(name + " (no path)");
            }
            if (unreachable.Count > 0) throw new InvalidOperationException("FieldTest NavMesh: unreachable " + string.Join(", ", unreachable));
            return "NavMesh baked, " + Landmarks.Length + " landmarks reachable";
        }

        // ------------------------------------------------------------------ squad, encounters, debug, audio

        private static void PlaceSquad(SquadController squad, UnityEngine.Camera camera)
        {
            Vector3[] spots = { Spawn + new Vector3(0f, 0.08f, 0f), Spawn + new Vector3(-1.6f, 0.08f, -2.2f), Spawn + new Vector3(1.9f, 0.08f, -3f) };
            for (int i = 0; i < squad.Members.Count; i++) squad.Members[i].transform.SetPositionAndRotation(spots[i], Quaternion.identity);
            var target = GameObject.Find("Camera Orbit Target").transform;
            target.SetPositionAndRotation(spots[0] + Vector3.up * 1.45f, Quaternion.Euler(12f, 0f, 0f));
            camera.transform.SetPositionAndRotation(target.position - target.forward * 5.5f + Vector3.right * 0.35f, target.rotation);
        }

        private enum Kind { Pursuer, Marksman, Bulwark }

        // Four compositions, each a dormant wave of the existing EncounterController (it never auto-starts here),
        // woken by the debug stations. Extra enemies are cloned from the encounter's own, so every wiring
        // (telegraphs, guard, aim line, combo feedback) comes along.
        private static (string name, EnemyBrain[] enemies, Vector3 area, float radius)[] BuildEncounters(SquadController squad, EncounterController encounter)
        {
            var existing = encounter.Waves.SelectMany(w => w.enemies).Where(e => e != null).ToList();
            var pool = new Dictionary<Kind, Queue<EnemyBrain>>
            {
                [Kind.Pursuer] = new Queue<EnemyBrain>(existing.Where(e => e.Archetype == "PURSUER")),
                [Kind.Marksman] = new Queue<EnemyBrain>(existing.Where(e => e.Archetype == "MARKSMAN")),
                [Kind.Bulwark] = new Queue<EnemyBrain>(existing.Where(e => e.Archetype == "BULWARK")),
            };
            var templates = pool.ToDictionary(p => p.Key, p => p.Value.Peek());
            (string name, Vector3 area, float radius, (Kind kind, Vector3 position)[] enemies)[] plan =
            {
                ("OPEN COMBAT", new Vector3(2f, 0f, -38f), 22f, new[] { (Kind.Pursuer, new Vector3(-6f, 0f, -40f)), (Kind.Pursuer, new Vector3(10f, 0f, -48f)), (Kind.Marksman, new Vector3(5f, 5.2f, -30f)) }),
                ("TIGHT COMBAT", new Vector3(-86f, 0f, 18f), 14f, new[] { (Kind.Pursuer, new Vector3(-85f, 0f, 10f)), (Kind.Bulwark, new Vector3(-88f, 0f, 30f)) }),
                ("VERTICAL COMBAT", new Vector3(70f, 0f, 36f), 16f, new[] { (Kind.Marksman, new Vector3(70f, 5f, 40f)), (Kind.Pursuer, new Vector3(62f, 0f, 30f)), (Kind.Pursuer, new Vector3(78f, 0f, 30f)) }),
                ("MIXED COMBAT", new Vector3(0f, 0f, 25f), 18f, new[] { (Kind.Bulwark, new Vector3(0f, 0f, 32f)), (Kind.Marksman, new Vector3(19f, 6f, 25f)), (Kind.Pursuer, new Vector3(-8f, 0f, 22f)), (Kind.Pursuer, new Vector3(4f, 0f, 16f)) }),
            };
            var result = new List<(string, EnemyBrain[], Vector3, float)>();
            foreach (var group in plan)
            {
                var parent = Child(encounters, group.name);
                var brains = new List<EnemyBrain>();
                int n = 0;
                foreach (var (kind, position) in group.enemies)
                {
                    EnemyBrain brain = pool[kind].Count > 0 ? pool[kind].Dequeue() : Object.Instantiate(templates[kind].gameObject).GetComponent<EnemyBrain>();
                    brain.transform.SetParent(parent, true);
                    brain.name = $"{group.name.Split(' ')[0]} {kind} {++n}";
                    if (!NavMesh.SamplePosition(position, out var hit, 0.8f, NavMesh.AllAreas))
                        throw new InvalidOperationException($"{brain.name} at {position} is off the NavMesh.");
                    Vector3 facing = group.area - hit.position; facing.y = 0f;
                    if (facing.sqrMagnitude < 0.01f) facing = Vector3.back;
                    brain.transform.SetPositionAndRotation(hit.position, Quaternion.LookRotation(facing));
                    brain.gameObject.SetActive(false);
                    brains.Add(brain);
                }
                result.Add((group.name, brains.ToArray(), group.area, group.radius));
            }
            foreach (var leftover in pool.Values.SelectMany(q => q)) Object.DestroyImmediate(leftover.gameObject);
            foreach (Transform child in encounter.transform.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Wave ")) Object.DestroyImmediate(child.gameObject);
            var all = result.SelectMany(r => r.Item2).ToArray();

            // Encounter: the four groups as dormant waves, never auto-started (restart = reload).
            var data = new SerializedObject(encounter);
            data.FindProperty("autoStart").boolValue = false;
            var waves = data.FindProperty("waves");
            waves.arraySize = result.Count;
            for (int w = 0; w < result.Count; w++)
            {
                var wave = waves.GetArrayElementAtIndex(w);
                wave.FindPropertyRelative("name").stringValue = result[w].Item1;
                Objects(wave.FindPropertyRelative("enemies"), result[w].Item2);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var threat = encounter.GetComponent<ThreatIndicator>();
            if (threat != null) { var t = new SerializedObject(threat); Objects(t.FindProperty("enemies"), all); t.ApplyModifiedPropertiesWithoutUndo(); }

            // Tactical overlay cards: one per enemy.
            var overlay = squad.GetComponent<TacticalOverlay>();
            var overlayData = new SerializedObject(overlay);
            var panels = overlayData.FindProperty("enemyPanels");
            var panelList = Enumerable.Range(0, panels.arraySize).Select(i => (RectTransform)panels.GetArrayElementAtIndex(i).objectReferenceValue).Where(p => p != null).ToList();
            var first = panelList[0];
            while (panelList.Count < all.Length)
            {
                var panel = Object.Instantiate(first.gameObject, first.parent).GetComponent<RectTransform>();
                panel.name = "Enemy " + (panelList.Count + 1);
                panelList.Add(panel);
            }
            while (panelList.Count > all.Length) { Object.DestroyImmediate(panelList[panelList.Count - 1].gameObject); panelList.RemoveAt(panelList.Count - 1); }
            Objects(overlayData.FindProperty("enemies"), all);
            Objects(overlayData.FindProperty("enemyPanels"), panelList.ToArray());
            Objects(overlayData.FindProperty("enemyTexts"), panelList.Select(p => p.GetComponentInChildren<Text>(true)).ToArray());
            overlayData.ApplyModifiedPropertiesWithoutUndo();
            var prompt = squad.GetComponent<ComboPrompt>();
            if (prompt != null)
            {
                var p = new SerializedObject(prompt);
                Objects(p.FindProperty("feedbacks"), Object.FindObjectsByType<ComboFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                p.ApplyModifiedPropertiesWithoutUndo();
            }
            return result.ToArray();
        }

        private static void BuildDebug(SquadController squad, TracePlayerInput input, EncounterController encounter, (string name, EnemyBrain[] enemies, Vector3 area, float radius)[] groups)
        {
            // Interactions for the terminals (no dialogue in this scene).
            var interaction = squad.GetComponent<InteractionController>();
            if (interaction == null) interaction = squad.gameObject.AddComponent<InteractionController>();
            SquadVisualKit.Reference(interaction, "input", input);
            SquadVisualKit.Reference(interaction, "squad", squad);
            Vector3[] terminals = { new Vector3(22f, 0f, -62f), new Vector3(-30f, 0f, -40f), new Vector3(50f, 0f, 12f), new Vector3(-12f, 0f, 2f) };
            Key[] keys = { Key.F5, Key.F6, Key.F7, Key.F8 };
            var stations = new GameObject("Encounter Debug Stations").AddComponent<EncounterDebugStations>();
            stations.transform.SetParent(debug, false);
            var data = new SerializedObject(stations);
            var array = data.FindProperty("groups");
            array.arraySize = groups.Length;
            for (int i = 0; i < groups.Length; i++)
            {
                var terminal = Terminal($"DEBUG // {groups[i].name}  [{keys[i]}]", terminals[i], groups[i].area);
                var entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("name").stringValue = groups[i].name;
                Objects(entry.FindPropertyRelative("enemies"), groups[i].enemies);
                entry.FindPropertyRelative("terminal").objectReferenceValue = terminal;
                entry.FindPropertyRelative("hotkey").enumValueFlag = (int)keys[i];
                entry.FindPropertyRelative("area").vector3Value = groups[i].area;
                entry.FindPropertyRelative("radius").floatValue = groups[i].radius;
            }
            data.FindProperty("resetTerminal").objectReferenceValue = Terminal("DEBUG // RESET ZONE  [F9 / Backspace]", new Vector3(16f, 5f, -93f), Spawn);
            data.FindProperty("encounter").objectReferenceValue = encounter;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // A small dev terminal: dark body, hazard band, emissive screen highlighted by the Interactable, a label.
        private static Interactable Terminal(string label, Vector3 position, Vector3 lookAt)
        {
            var go = new GameObject(label.Split('[')[0].Trim());
            go.transform.SetParent(debug, false);
            go.transform.position = position;
            Vector3 facing = lookAt - position; facing.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.forward);
            var body = Primitive("Body", go.transform, new Vector3(0f, 0.65f, 0f), new Vector3(0.6f, 1.3f, 0.4f), "Dark");
            Primitive("Band", go.transform, new Vector3(0f, 1.05f, 0.205f), new Vector3(0.62f, 0.08f, 0.02f), "Hazard");
            var screen = Primitive("Screen", go.transform, new Vector3(0f, 1.25f, 0.21f), new Vector3(0.5f, 0.3f, 0.02f), "Screen");
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            // Readable from both sides: players reach the terminals from any direction.
            for (int side = 0; side < 2; side++)
            {
                var text = new GameObject(side == 0 ? "Label Front" : "Label Back").AddComponent<TextMesh>();
                text.transform.SetParent(go.transform, false);
                text.transform.localPosition = new Vector3(0f, 1.75f, side == 0 ? 0.1f : -0.1f);
                text.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 180f : 0f, 0f);
                text.text = label; text.characterSize = 0.05f; text.fontSize = 48; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(1f, 0.78f, 0.16f);
            }
            var interactable = go.AddComponent<Interactable>();
            var data = new SerializedObject(interactable);
            data.FindProperty("prompt").stringValue = label;
            data.FindProperty("oneShot").boolValue = false;
            data.FindProperty("highlight").objectReferenceValue = screen.GetComponent<Renderer>();
            data.ApplyModifiedPropertiesWithoutUndo();
            return interactable;
        }

        // Light test ambience: base and air layers, a few placed loops where machines are, rare details indoors.
        private static void BuildAmbience()
        {
            var ambience = new GameObject("Ambience").AddComponent<AmbienceController>();
            ambience.transform.SetParent(audio, false);
            var data = new SerializedObject(ambience);
            data.FindProperty("baseLoop").objectReferenceValue = Clip("AMB_FirstTrace_Base_LOOP.ogg");
            data.FindProperty("baseVolume").floatValue = 0.24f;
            data.FindProperty("environmentLoop").objectReferenceValue = Clip("AMB_FirstTrace_Environment_LOOP.ogg");
            data.FindProperty("environmentVolume").floatValue = 0.16f;
            var details = data.FindProperty("detailClips");
            Objects(details, new Object[] { Clip("AMB_Detail_MetalImpact_01.wav"), Clip("AMB_Detail_Relay_01.wav") });
            data.FindProperty("detailInterval").vector2Value = new Vector2(12f, 30f);
            var points = new[] { new Vector3(0f, 10f, 30f), new Vector3(-40f, 7f, 88f), new Vector3(-70f, 0f, 74f) }.Select((p, i) =>
            {
                var t = new GameObject("Detail Point " + (i + 1)).transform; t.SetParent(audio, false); t.position = p; return t;
            }).ToArray();
            Objects(data.FindProperty("detailPoints"), points);
            data.FindProperty("ambienceGroup").objectReferenceValue = TacticalFocusPresentationSetup.MixerGroup("Ambience");
            data.FindProperty("musicGroup").objectReferenceValue = TacticalFocusPresentationSetup.MixerGroup("Music");
            data.ApplyModifiedPropertiesWithoutUndo();
            Emitter("Hangar Machine", "AMB_Emitter_Machinery_LOOP.ogg", new Vector3(-12f, 2f, 34f), 0.4f, 14f);
            Emitter("Machine Room", "AMB_Emitter_Machinery_LOOP.ogg", new Vector3(-46f, 2f, 88f), 0.45f, 14f);
            Emitter("Pit Pipes", "AMB_Emitter_Pipeline_LOOP.ogg", new Vector3(-79f, -1.3f, 70f), 0.45f, 11f);
            Emitter("Lab Terminal", "AMB_Emitter_Terminal_LOOP.ogg", new Vector3(-50f, 1f, 74f), 0.3f, 6f);
        }

        private static void Emitter(string name, string clip, Vector3 position, float volume, float range)
        {
            var go = new GameObject("Emitter " + name);
            go.transform.SetParent(audio, false);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = Clip(clip); source.loop = true; source.playOnAwake = false; source.volume = 0f;
            source.spatialBlend = 1f; source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 1.5f; source.maxDistance = range;
            source.dopplerLevel = 0f; source.spread = 60f; source.priority = 160;
            source.outputAudioMixerGroup = TacticalFocusPresentationSetup.MixerGroup("Ambience");
            var emitter = go.AddComponent<AmbienceEmitter>();
            SquadVisualKit.Number(emitter, "volume", volume);
        }

        private static AudioClip Clip(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TRACE/Audio/Ambience/" + file);
            if (clip == null) throw new InvalidOperationException("Missing ambience clip " + file);
            return clip;
        }

        // ------------------------------------------------------------------ geometry helpers

        private struct DoorSpec { public char side; public float at, width, height, sill; public bool skip; }
        private static DoorSpec Door(char side, float at, float width, float height, float sill = 0f) =>
            new DoorSpec { side = side, at = at, width = width, height = height, sill = sill };
        private static DoorSpec Skip(char side) => new DoorSpec { side = side, skip = true };

        // Four walls (0.4 m) with door gaps and lintels, plus a roof slab. A skipped side is a wall shared with a
        // neighbouring room (it owns that wall and its doors) or an open end.
        private static void Room(string name, Transform parent, float x0, float x1, float z0, float z1, float height, string wall, string roof, DoorSpec[] doors)
        {
            var g = Child(parent, name);
            const float t = 0.4f;
            foreach (char side in "NSEW")
            {
                if (doors.Any(d => d.side == side && d.skip)) continue;
                bool alongX = side == 'N' || side == 'S';
                float fixedCoord = side == 'N' ? z1 : side == 'S' ? z0 : side == 'E' ? x1 : x0;
                float start = alongX ? x0 : z0, end = alongX ? x1 : z1;
                var gaps = doors.Where(d => d.side == side && !d.skip).OrderBy(d => d.at).ToList();
                float cursor = start;
                void Segment(float a, float b, float y0, float y1)
                {
                    if (b - a < 0.05f || y1 - y0 < 0.05f) return;
                    float mid = (a + b) * 0.5f, len = b - a, h = y1 - y0;
                    Vector3 center = alongX ? new Vector3(mid, y0 + h * 0.5f, fixedCoord) : new Vector3(fixedCoord, y0 + h * 0.5f, mid);
                    Vector3 size = alongX ? new Vector3(len + (a == start || b == end ? t : 0f), h, t) : new Vector3(t, h, len);
                    Box($"{side} Wall", g, center, size, wall);
                }
                foreach (var door in gaps)
                {
                    float a = door.at - door.width * 0.5f, b = door.at + door.width * 0.5f;
                    Segment(cursor, a, 0f, height);
                    Segment(a, b, door.sill + door.height, height);
                    if (door.sill > 0f) Segment(a, b, 0f, door.sill);
                    cursor = b;
                }
                Segment(cursor, end, 0f, height);
            }
            Box("Roof", g, new Vector3((x0 + x1) * 0.5f, height + 0.2f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.4f, 0.4f, z1 - z0 + 0.4f), roof);
            // Interior floor slab (Visual Pass B): visual only, 3 cm above the valley floor it covers.
            Object.DestroyImmediate(Box("Floor", g, new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.06f, z1 - z0), "Concrete").GetComponent<Collider>());
        }

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string material, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            go.isStatic = true;
            return go;
        }

        private static GameObject Primitive(string name, Transform parent, Vector3 local, Vector3 size, string material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            return go;
        }

        private static void RockWall(string name, Transform parent, Vector3 center, Vector3 along, float length, float height)
        {
            float yaw = Quaternion.LookRotation(along).eulerAngles.y;
            Box(name, parent, center + Vector3.up * height * 0.5f, new Vector3(4f, height, length), "Rock", yaw);
        }

        private static void Rock(string name, Vector3 center, Vector3 size, float yaw, Transform parent = null) =>
            Box(name, parent ?? terrain, center, size, "Rock", yaw);

        // A low wedge-shaped mound, slopes under ~15 degrees.
        private static void Mound(string name, Vector3 center, Vector2 footprint, float height, float yaw)
        {
            var go = Box(name, terrain, center + Vector3.up * (height * 0.5f - 0.4f), new Vector3(footprint.x, height, footprint.y), "Ground", yaw);
            go.transform.rotation = Quaternion.Euler(Mathf.Atan2(height, footprint.y) * Mathf.Rad2Deg * 0.5f, yaw, 0f);
        }

        // A walkable ramp: a slab whose top surface runs from bottom to top.
        private static GameObject Ramp(string name, Transform parent, Vector3 bottom, Vector3 top, float width, string material)
        {
            Vector3 along = top - bottom;
            var rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((bottom + top) * 0.5f - rotation * Vector3.up * 0.15f, rotation);
            go.transform.localScale = new Vector3(width, 0.3f, along.magnitude + 0.3f);
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            go.isStatic = true;
            return go;
        }

        // Stairs: an invisible ramp for locomotion and agents, visible steps without colliders, side stringers.
        private static void Stairs(string name, Transform parent, Vector3 bottom, Vector3 top, float width, string material)
        {
            var g = Child(parent, name);
            var ramp = Ramp("Walk Surface", g, bottom, top, width, material);
            ramp.GetComponent<Renderer>().enabled = false;
            Vector3 flat = top - bottom; flat.y = 0f;
            float rise = top.y - bottom.y;
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(rise) / 0.22f));
            var facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
            for (int i = 0; i < steps; i++)
            {
                float t0 = (float)i / steps, t1 = (float)(i + 1) / steps;
                float y = bottom.y + rise * t1;
                Vector3 center = bottom + flat * (t0 + t1) * 0.5f;
                float lowest = Mathf.Min(bottom.y, top.y);
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Step " + i;
                step.transform.SetParent(g, false);
                step.transform.SetPositionAndRotation(new Vector3(center.x, (y + lowest) * 0.5f, center.z), facing);
                step.transform.localScale = new Vector3(width, Mathf.Max(0.05f, y - lowest), flat.magnitude / steps + 0.02f);
                step.GetComponent<Renderer>().sharedMaterial = Mat(material);
                Object.DestroyImmediate(step.GetComponent<Collider>());
                step.isStatic = true;
            }
        }

        private static void Container(string name, Transform parent, Vector3 basePosition, float yaw, string material)
        {
            Box(name, parent, basePosition + Vector3.up * 1.3f, new Vector3(2.44f, 2.6f, 12.2f), material, yaw);
        }

        private static void Pipe(string name, Transform parent, Vector3 from, Vector3 to, float radius, string material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, (to - from).normalized));
            go.transform.localScale = new Vector3(radius * 2f, Vector3.Distance(from, to) * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            go.isStatic = true;
        }

        private static void Light(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(lighting, false);
            go.transform.position = position;
            var light = go.AddComponent<UnityEngine.Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void Landmark(string name, Vector3 position, FieldMarker.Kind kind, float radius, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var marker = go.AddComponent<FieldMarker>();
            var data = new SerializedObject(marker);
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.FindProperty("radius").floatValue = radius;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Objects(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // Pass A materials: sober URP/Lit, ORIGIN white and yellow used sparingly.
        private static Material Mat(string key)
        {
            if (Materials.TryGetValue(key, out var cached)) return cached;
            (Color color, float smoothness, float metallic, Color emission) spec = key switch
            {
                "Concrete" => (new Color(0.52f, 0.52f, 0.5f), 0.12f, 0f, Color.black),
                "Metal" => (new Color(0.36f, 0.38f, 0.41f), 0.45f, 0.55f, Color.black),
                "Dark" => (new Color(0.12f, 0.13f, 0.14f), 0.25f, 0.2f, Color.black),
                "Ground" => (new Color(0.33f, 0.31f, 0.28f), 0.06f, 0f, Color.black),
                "Rock" => (new Color(0.29f, 0.29f, 0.3f), 0.1f, 0f, Color.black),
                "ORIGIN" => (new Color(0.84f, 0.84f, 0.81f), 0.3f, 0f, Color.black),
                "Hazard" => (new Color(0.93f, 0.68f, 0.15f), 0.3f, 0f, Color.black),
                "Screen" => (new Color(0.05f, 0.12f, 0.12f), 0.6f, 0f, new Color(0.25f, 0.75f, 0.7f)),
                _ => throw new ArgumentException(key),
            };
            string file = key == "Screen" ? "M_Proto_Screen" : "M_Proto_" + key;
            string path = $"{MaterialFolder}/{file}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", spec.color);
            material.SetFloat("_Smoothness", spec.smoothness);
            material.SetFloat("_Metallic", spec.metallic);
            if (spec.emission != Color.black)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", spec.emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            Materials[key] = material;
            return material;
        }
    }
}
