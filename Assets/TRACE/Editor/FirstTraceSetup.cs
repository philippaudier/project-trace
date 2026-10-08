using System;
using System.Collections.Generic;
using System.Linq;
using TRACE.AI;
using TRACE.Encounter;
using TRACE.Input;
using TRACE.Narrative;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TRACE.Editor
{
    // V1: authors the First Trace slice from PrototypeEncounter (squad, camera, encounter, overlay, lock already wired).
    // The prototype environment is replaced by a short semi-linear industrial zone; everything is primitives.
    public static class FirstTraceSetup
    {
        public const string ScenePath = "Assets/TRACE/Scenes/FirstTrace.unity";
        private const string SourcePath = "Assets/TRACE/Scenes/PrototypeEncounter.unity";
        private const string MaterialFolder = "Assets/TRACE/Scenes/Materials/FirstTrace";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Transform level;
        private static Transform doors;
        private static readonly List<GameObject> bakeExclusions = new List<GameObject>();

        [MenuItem("TRACE/Create V1 First Trace Scene (only if missing)")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("First Trace already exists; it will not be overwritten.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.CopyAsset(SourcePath, ScenePath))
                throw new InvalidOperationException("Could not copy the encounter scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Materials.Clear();
            bakeExclusions.Clear();
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/TRACE/Scenes/Materials", "FirstTrace");
            foreach (string name in new[] { "Environment", "Duel Area", "Combat Practice" })
            {
                var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            var input = squad.GetComponent<TracePlayerInput>();
            var encounter = UnityEngine.Object.FindFirstObjectByType<EncounterController>();
            var camera = UnityEngine.Camera.main;

            level = new GameObject("Level").transform;
            doors = new GameObject("Doors").transform;
            doors.SetParent(level, false);
            BuildShell();
            BuildDock();
            var badge = BuildPassage();
            var terminalA = BuildClueTerminal(out var clueDoor);
            BuildHall(out var eventDoor, out var eventLight, out var eventScreen);
            var tensionLights = BuildTensionCorridor();
            BuildArena(out var gates, out var terminalScreen, out var conclusionLight);
            PlaceSquad(squad, camera);
            PlaceEnemies(encounter);
            Lighting(camera);
            var navMesh = BakeNavMesh();

            // Story systems.
            var story = new GameObject("Story");
            var dialogue = story.AddComponent<DialogueRunner>();
            References(dialogue, ("input", input));
            var ambience = new GameObject("Ambience").AddComponent<AmbienceController>();
            ambience.transform.SetParent(story.transform, false);
            var interaction = squad.gameObject.AddComponent<InteractionController>();
            References(interaction, ("input", input), ("squad", squad), ("dialogue", dialogue));
            var director = story.AddComponent<StoryDirector>();
            var data = new SerializedObject(director);
            Set(data, "squad", squad); Set(data, "encounter", encounter); Set(data, "dialogue", dialogue); Set(data, "ambience", ambience);
            Set(data, "clueTerminal", terminalA); Set(data, "badge", badge); Set(data, "clueDoor", clueDoor); Set(data, "eventDoor", eventDoor);
            Array(data.FindProperty("arenaGates"), gates.Cast<UnityEngine.Object>().ToArray());
            Set(data, "eventLight", eventLight); Set(data, "eventScreen", eventScreen);
            Array(data.FindProperty("tensionLights"), tensionLights.Cast<UnityEngine.Object>().ToArray());
            Set(data, "conclusionLight", conclusionLight); Set(data, "conclusionScreen", terminalScreen); Set(data, "conclusionScreenOn", Mat("ScreenOn"));
            Set(data, "hallZone", Zone("Zone Hall", new Vector3(0f, 0f, 47f)));
            Set(data, "tensionZone", Zone("Zone Tension", new Vector3(0f, 0f, 67f)));
            Set(data, "arenaZone", Zone("Zone Arena", new Vector3(0f, 0f, 76f)));
            Lines(data, "arrivalLines",
                ("CONTROL", "Pas un bruit. Même pas les machines.", 3f),
                ("ASSAULT", "On avance. Ouvrez l'oeil.", 2.6f));
            Lines(data, "clueLines",
                ("TERMINAL", "ARCHIVE ENTRY // INCOMPLETE", 2.4f),
                ("TERMINAL", "Occupancy records: 143", 2.4f),
                ("TERMINAL", "Registered residents: 0", 2.8f),
                ("ASSAULT", "Ça ne colle pas.", 2.2f),
                ("CONTROL", "Quoi ?", 1.6f),
                ("ASSAULT", "Le bâtiment. Il est trop grand pour les plans.", 3.2f),
                ("SUPPORT", "...Je pensais être la seule à l'avoir remarqué.", 3.4f));
            Lines(data, "badgeLines",
                ("SUPPORT", "Un badge d'accès. OKAFOR, maintenance. Validé il y a trois jours.", 3.6f),
                ("CONTROL", "Trois jours. Le site est fermé depuis dix ans.", 3.2f));
            Lines(data, "eventLines",
                ("CONTROL", "Quelqu'un vient d'ouvrir cette porte.", 2.8f),
                ("SUPPORT", "Il n'y a personne d'enregistré ici.", 2.8f),
                ("ASSAULT", "Alors on va voir qui n'est pas enregistré.", 3f));
            Lines(data, "tensionLines",
                ("ASSAULT", "Restez groupés. Ils arrivent.", 2.6f),
                ("CONTROL", "Tab pour lire, molette pour verrouiller. On se couvre.", 3.2f));
            Lines(data, "conclusionLines",
                ("TERMINAL", "IDENTITY MATCH FOUND", 2.6f),
                ("TERMINAL", "STATUS: DECEASED", 2.6f),
                ("TERMINAL", "LAST ACCESS: 12 MINUTES AGO", 3.2f),
                ("SUPPORT", "...Douze minutes.", 2.4f),
                ("ASSAULT", "On n'est pas seuls ici.", 3f));
            data.ApplyModifiedPropertiesWithoutUndo();
            var encounterData = new SerializedObject(encounter);
            data.FindProperty("tensionLightIntensity").floatValue = 12f;
            var hudData = new SerializedObject(encounter.GetComponent<EncounterHud>());
            hudData.FindProperty("showCompletion").boolValue = false;
            hudData.ApplyModifiedPropertiesWithoutUndo();
            encounterData.FindProperty("autoStart").boolValue = false;
            encounterData.FindProperty("startDelay").floatValue = 2.5f;
            encounterData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V1: First Trace authored (" + navMesh + ").");
        }

        // ------------------------------------------------------------------ level

        private static void BuildShell()
        {
            Box("Floor", new Vector3(0f, -0.25f, 50f), new Vector3(46f, 0.5f, 126f), "ConcreteDark");
            Box("Outer West", new Vector3(-22.5f, 4f, 50f), new Vector3(1f, 8f, 126f), "Concrete");
            Box("Outer East", new Vector3(22.5f, 4f, 50f), new Vector3(1f, 8f, 126f), "Concrete");
            Box("Outer South", new Vector3(0f, 4f, -12.5f), new Vector3(46f, 8f, 1f), "Concrete");
            Box("Outer North", new Vector3(0f, 4f, 112.5f), new Vector3(46f, 8f, 1f), "Concrete");
        }

        private static void BuildDock()
        {
            var dock = Group("Dock");
            Box("Dock West", new Vector3(-10.5f, 3f, 1f), new Vector3(1f, 6f, 24f), "Concrete", dock);
            Box("Dock East", new Vector3(10.5f, 3f, 1f), new Vector3(1f, 6f, 24f), "Concrete", dock);
            Box("Funnel West", new Vector3(-7.5f, 3f, 12.5f), new Vector3(7f, 6f, 1f), "Concrete", dock);
            Box("Funnel East", new Vector3(7.5f, 3f, 12.5f), new Vector3(7f, 6f, 1f), "Concrete", dock);
            Box("Loading Ledge", new Vector3(-8.5f, 0.3f, -6f), new Vector3(3f, 0.6f, 10f), "ConcreteDark", dock);
            Box("Crate 1", new Vector3(-6f, 0.6f, 3f), Vector3.one * 1.2f, "Crate", dock);
            Box("Crate 2", new Vector3(-4.7f, 0.6f, 3.4f), Vector3.one * 1.2f, "Crate", dock);
            Box("Crate 3", new Vector3(-5.4f, 1.8f, 3.2f), Vector3.one * 1.1f, "Crate", dock);
            Box("Truck Body", new Vector3(5.8f, 1.4f, 4.5f), new Vector3(2.4f, 2.6f, 6f), "Rust", dock);
            Box("Truck Cab", new Vector3(5.8f, 1.2f, 8.6f), new Vector3(2.4f, 2.3f, 2.2f), "Metal", dock);
            Box("Pipe Low", new Vector3(9.8f, 3.4f, 1f), new Vector3(0.35f, 0.35f, 24f), "Rust", dock);
            Box("Pipe High", new Vector3(9.8f, 4.2f, 1f), new Vector3(0.25f, 0.25f, 24f), "Rust", dock);
            Decal("Puddle Dock", new Vector3(-2f, 0.015f, 8f), new Vector3(3.8f, 0.03f, 5f), "Water", dock);
            Decal("Moss Dock", new Vector3(-7f, 0.012f, -3f), new Vector3(3f, 0.025f, 4f), "Moss", dock);
            Box("Lamp Pole", new Vector3(-8f, 2f, 7f), new Vector3(0.2f, 4f, 0.2f), "Metal", dock);
            Decal("Lamp Head", new Vector3(-8f, 4.05f, 7.4f), new Vector3(0.5f, 0.18f, 0.6f), "LampWarm", dock);
            PointLight("Dock Lamp", new Vector3(-7.8f, 3.85f, 7.6f), new Color(1f, 0.72f, 0.42f), 12f, 16f, dock, flicker: true);
        }

        private static Interactable BuildPassage()
        {
            var passage = Group("Passage");
            Box("Corridor West A", new Vector3(-4.5f, 3f, 15f), new Vector3(1f, 6f, 6f), "Concrete", passage);
            Box("Corridor West B", new Vector3(-4.5f, 3f, 27f), new Vector3(1f, 6f, 9f), "Concrete", passage);
            Box("Corridor East", new Vector3(4.5f, 3f, 21.5f), new Vector3(1f, 6f, 19f), "Concrete", passage);
            // Side alcove: an optional look-around with a left-behind object.
            Box("Alcove Back", new Vector3(-9.5f, 3f, 20f), new Vector3(1f, 6f, 6f), "Concrete", passage);
            Box("Alcove South", new Vector3(-7f, 3f, 17.5f), new Vector3(5f, 6f, 1f), "Concrete", passage);
            Box("Alcove North", new Vector3(-7f, 3f, 22.5f), new Vector3(5f, 6f, 1f), "Concrete", passage);
            Box("Alcove Crate", new Vector3(-7.2f, 0.5f, 20f), Vector3.one, "Crate", passage);
            Box("Fallen Chair", new Vector3(-8.3f, 0.25f, 18.8f), new Vector3(0.5f, 0.5f, 0.9f), "Metal", passage).transform.rotation = Quaternion.Euler(0f, 35f, 90f);
            var card = Decal("Badge Card", new Vector3(-7.2f, 1.03f, 20f), new Vector3(0.3f, 0.02f, 0.2f), "ScreenOn", passage);
            var badge = card.AddComponent<Interactable>();
            var badgeData = new SerializedObject(badge);
            badgeData.FindProperty("prompt").stringValue = "Ramasser le badge";
            badgeData.FindProperty("range").floatValue = 2.4f;
            badgeData.FindProperty("highlight").objectReferenceValue = card.GetComponent<Renderer>();
            badgeData.ApplyModifiedPropertiesWithoutUndo();
            Box("Cable 1", new Vector3(1.5f, 4.4f, 16f), new Vector3(0.06f, 3.2f, 0.06f), "Rust", passage).transform.rotation = Quaternion.Euler(8f, 0f, 4f);
            Box("Cable 2", new Vector3(-1f, 4.1f, 25f), new Vector3(0.06f, 3.8f, 0.06f), "Rust", passage).transform.rotation = Quaternion.Euler(-6f, 0f, -9f);
            Decal("Puddle Passage", new Vector3(0.5f, 0.015f, 15f), new Vector3(2.6f, 0.03f, 3.2f), "Water", passage);
            PointLight("Passage Lamp", new Vector3(0f, 5.2f, 22f), new Color(0.75f, 0.86f, 1f), 8f, 14f, passage, flicker: true);
            return badge;
        }

        private static Interactable BuildClueTerminal(out GateDoor clueDoor)
        {
            var clue = Group("Clue");
            Box("Terminal A Body", new Vector3(3.6f, 0.8f, 28.5f), new Vector3(0.7f, 1.6f, 0.9f), "Metal", clue);
            var screen = Decal("Terminal A Screen", new Vector3(3.2f, 1.35f, 28.5f), new Vector3(0.05f, 0.5f, 0.62f), "ScreenDim", clue);
            var terminal = screen.AddComponent<Interactable>();
            var terminalData = new SerializedObject(terminal);
            terminalData.FindProperty("prompt").stringValue = "Consulter le terminal";
            terminalData.FindProperty("range").floatValue = 2.8f;
            terminalData.FindProperty("highlight").objectReferenceValue = screen.GetComponent<Renderer>();
            terminalData.ApplyModifiedPropertiesWithoutUndo();
            PointLight("Terminal A Glow", new Vector3(2.6f, 1.6f, 28.5f), new Color(0.35f, 0.9f, 0.85f), 2.5f, 6f, clue);
            Box("Door Clue Header", new Vector3(0f, 5f, 31f), new Vector3(9f, 2f, 0.6f), "Concrete", clue);
            clueDoor = Door("Door Clue", new Vector3(0f, 2f, 31f), new Vector3(9f, 4f, 0.5f));
            // Small room number stencilled above the left side of the clue door, off-centre like a maintenance mark
            // (about 0.25 m high), read from the corridor before the archive explains it.
            Label("Door Mark 143", new Vector3(-2.6f, 4.7f, 30.68f), Quaternion.identity, "143", new Color(0.62f, 0.66f, 0.72f), 0.012f, clue);
            return terminal;
        }

        private static void BuildHall(out GateDoor eventDoor, out Light eventLight, out GameObject eventScreen)
        {
            var hall = Group("Hall");
            Box("Hall Back West", new Vector3(-10f, 3.5f, 31f), new Vector3(11f, 7f, 1f), "Concrete", hall);
            Box("Hall Back East", new Vector3(10f, 3.5f, 31f), new Vector3(11f, 7f, 1f), "Concrete", hall);
            Box("Hall West A", new Vector3(-15.5f, 3.5f, 40f), new Vector3(1f, 7f, 18f), "Concrete", hall);
            Box("Hall West B", new Vector3(-15.5f, 3.5f, 58f), new Vector3(1f, 7f, 6f), "Concrete", hall);
            Box("Hall East", new Vector3(15.5f, 3.5f, 46f), new Vector3(1f, 7f, 30f), "Concrete", hall);
            Box("Hall Front West", new Vector3(-10f, 3.5f, 61f), new Vector3(11f, 7f, 1f), "Concrete", hall);
            Box("Hall Front East", new Vector3(10f, 3.5f, 61f), new Vector3(11f, 7f, 1f), "Concrete", hall);
            Box("Door Event Header", new Vector3(0f, 5.5f, 61f), new Vector3(9f, 3f, 1f), "Concrete", hall);
            eventDoor = Door("Door Event", new Vector3(0f, 2f, 61f), new Vector3(9f, 4f, 0.5f));
            foreach (float z in new[] { 37f, 45f, 53f })
                foreach (float x in new[] { -7f, 7f })
                    Box($"Pillar {x} {z}", new Vector3(x, 3.5f, z), new Vector3(1f, 7f, 1f), "Concrete", hall);
            Decal("Puddle Hall", new Vector3(3f, 0.015f, 40f), new Vector3(6f, 0.03f, 8f), "Water", hall);
            Decal("Moss Hall", new Vector3(-11f, 0.012f, 36f), new Vector3(4f, 0.025f, 5f), "Moss", hall);
            Box("Collapsed Shelf", new Vector3(12f, 0.6f, 36f), new Vector3(4f, 1.2f, 1f), "Rust", hall).transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            PointLight("Hall Lamp SW", new Vector3(-7f, 5.4f, 41f), new Color(0.7f, 0.84f, 1f), 16f, 22f, hall);
            PointLight("Hall Lamp NE", new Vector3(7f, 5.4f, 53f), new Color(0.7f, 0.84f, 1f), 16f, 22f, hall);
            // Side room: a circle of chairs, nothing to do, something to notice.
            Box("Room West", new Vector3(-21f, 3f, 52f), new Vector3(1f, 6f, 7f), "Concrete", hall);
            Box("Room South", new Vector3(-18.5f, 3f, 48.5f), new Vector3(6f, 6f, 1f), "Concrete", hall);
            Box("Room North", new Vector3(-18.5f, 3f, 55.5f), new Vector3(6f, 6f, 1f), "Concrete", hall);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                var chair = Box("Chair " + (i + 1), new Vector3(-18.5f + Mathf.Cos(angle) * 1.6f, 0.45f, 52f + Mathf.Sin(angle) * 1.6f), new Vector3(0.5f, 0.9f, 0.5f), "Metal", hall);
                chair.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
            }
            PointLight("Room Lamp", new Vector3(-18.5f, 4.5f, 52f), new Color(1f, 0.68f, 0.4f), 6f, 10f, hall, flicker: true);
            // Painted on the far wall beside the exit door: a quiet detail the event light later reveals.
            Label("Wall Mark 143", new Vector3(-8f, 2.6f, 60.4f), Quaternion.identity, "143", new Color(0.62f, 0.66f, 0.72f), 0.045f, hall);
            // The strange event: a cold light above the far door and a screen that briefly wakes up.
            eventLight = PointLight("Event Light", new Vector3(0f, 6.2f, 59f), new Color(0.6f, 0.9f, 1f), 10f, 22f, hall);
            eventScreen = new GameObject("Event Screen");
            eventScreen.transform.SetParent(hall, false);
            Decal("Event Panel", new Vector3(14.95f, 3f, 50f), new Vector3(0.06f, 1.4f, 2.4f), "ScreenOn", eventScreen.transform);
            // Readable from the west side of the hall: the text plane faces +x.
            Label("Event Text", new Vector3(14.85f, 3f, 50f), Quaternion.Euler(0f, 90f, 0f), "OCCUPANCY 143\nRESIDENTS 0", new Color(0.3f, 0.95f, 0.85f), 0.028f, eventScreen.transform);
            eventScreen.SetActive(false);
        }

        private static Light[] BuildTensionCorridor()
        {
            var corridor = Group("Tension Corridor");
            Box("Tension West", new Vector3(-5f, 3f, 67f), new Vector3(1f, 6f, 12f), "Concrete", corridor);
            Box("Tension East", new Vector3(5f, 3f, 67f), new Vector3(1f, 6f, 12f), "Concrete", corridor);
            Box("Tension Fill West", new Vector3(-10.25f, 3.5f, 73f), new Vector3(10.5f, 7f, 1f), "Concrete", corridor);
            Box("Tension Fill East", new Vector3(10.25f, 3.5f, 73f), new Vector3(10.5f, 7f, 1f), "Concrete", corridor);
            Box("Tension Header", new Vector3(0f, 5.5f, 73f), new Vector3(10f, 3f, 1f), "Concrete", corridor);
            Decal("Warning Strip West", new Vector3(-4.45f, 1.1f, 67f), new Vector3(0.06f, 0.15f, 11f), "Warning", corridor);
            Decal("Warning Strip East", new Vector3(4.45f, 1.1f, 67f), new Vector3(0.06f, 0.15f, 11f), "Warning", corridor);
            var lights = new List<Light>();
            foreach (float z in new[] { 63.5f, 67f, 70.5f })
            {
                var light = PointLight("Tension Light " + z, new Vector3(z == 67f ? 0f : (z < 67f ? -3.5f : 3.5f), 4.6f, z), new Color(1f, 0.16f, 0.1f), 1.2f, 10f, corridor);
                lights.Add(light);
            }
            return lights.ToArray();
        }

        private static void BuildArena(out GateDoor[] gates, out Renderer terminalScreen, out Light conclusionLight)
        {
            var arena = Group("Arena");
            Box("Arena West", new Vector3(-16.5f, 3.5f, 86.5f), new Vector3(1f, 7f, 27f), "Concrete", arena);
            Box("Arena East A", new Vector3(16.5f, 3.5f, 78.25f), new Vector3(1f, 7f, 10.5f), "Concrete", arena);
            Box("Arena East B", new Vector3(16.5f, 3.5f, 94.25f), new Vector3(1f, 7f, 11.5f), "Concrete", arena);
            Box("Arena North West", new Vector3(-13.25f, 3.5f, 100f), new Vector3(5.5f, 7f, 1f), "Concrete", arena);
            Box("Arena North Mid", new Vector3(0f, 3.5f, 100f), new Vector3(11f, 7f, 1f), "Concrete", arena);
            Box("Arena North East", new Vector3(13.25f, 3.5f, 100f), new Vector3(5.5f, 7f, 1f), "Concrete", arena);
            Box("Gate A Header", new Vector3(-8f, 5.5f, 100f), new Vector3(5f, 3f, 1f), "Concrete", arena);
            Box("Gate B Header", new Vector3(8f, 5.5f, 100f), new Vector3(5f, 3f, 1f), "Concrete", arena);
            Box("Gate C Header", new Vector3(16.5f, 5.5f, 86f), new Vector3(1f, 3f, 5f), "Concrete", arena);
            // Pockets behind the gates: dark, enclosed, walked out of.
            Box("Pocket North Back", new Vector3(0f, 3f, 106.5f), new Vector3(24f, 6f, 1f), "ConcreteDark", arena);
            Box("Pocket North West", new Vector3(-12f, 3f, 103f), new Vector3(1f, 6f, 6f), "ConcreteDark", arena);
            Box("Pocket North East", new Vector3(12f, 3f, 103f), new Vector3(1f, 6f, 6f), "ConcreteDark", arena);
            Box("Pocket North Split", new Vector3(0f, 3f, 103f), new Vector3(1f, 6f, 6f), "ConcreteDark", arena);
            Box("Pocket East South", new Vector3(19.25f, 3f, 83f), new Vector3(5.5f, 6f, 1f), "ConcreteDark", arena);
            Box("Pocket East North", new Vector3(19.25f, 3f, 89f), new Vector3(5.5f, 6f, 1f), "ConcreteDark", arena);
            Box("Cover 1", new Vector3(-5f, 0.75f, 85f), new Vector3(1.6f, 1.5f, 1.6f), "Crate", arena);
            Box("Cover 2", new Vector3(6f, 0.75f, 90f), new Vector3(1.6f, 1.5f, 1.6f), "Crate", arena);
            Box("Cover 3", new Vector3(1f, 0.75f, 80f), new Vector3(2.2f, 1.5f, 1.2f), "Rust", arena);
            Box("Broken Pillar", new Vector3(-9f, 2f, 92f), new Vector3(1.2f, 4f, 1.2f), "Concrete", arena);
            Decal("Puddle Arena", new Vector3(8f, 0.015f, 82f), new Vector3(7f, 0.03f, 6f), "Water", arena);
            PointLight("Arena Lamp SW", new Vector3(-8f, 5.4f, 80f), new Color(0.7f, 0.84f, 1f), 16f, 22f, arena);
            PointLight("Arena Lamp SE", new Vector3(8f, 5.4f, 80f), new Color(0.7f, 0.84f, 1f), 16f, 22f, arena);
            PointLight("Arena Lamp NW", new Vector3(-8f, 5.4f, 94f), new Color(0.7f, 0.84f, 1f), 16f, 22f, arena);
            PointLight("Arena Lamp NE", new Vector3(8f, 5.4f, 94f), new Color(0.7f, 0.84f, 1f), 16f, 22f, arena);
            gates = new[]
            {
                Door("Gate A", new Vector3(-8f, 2f, 100f), new Vector3(5f, 4f, 0.5f)),
                Door("Gate B", new Vector3(8f, 2f, 100f), new Vector3(5f, 4f, 0.5f)),
                Door("Gate C", new Vector3(16.5f, 2f, 86f), new Vector3(0.5f, 4f, 5f)),
            };
            // The conclusion terminal: dead until the fight ends.
            Box("Terminal B Body", new Vector3(-15.5f, 0.8f, 78f), new Vector3(0.7f, 1.6f, 0.9f), "Metal", arena);
            terminalScreen = Decal("Terminal B Screen", new Vector3(-15.1f, 1.35f, 78f), new Vector3(0.05f, 0.5f, 0.62f), "ScreenOff", arena).GetComponent<Renderer>();
            conclusionLight = PointLight("Terminal B Glow", new Vector3(-14.3f, 1.8f, 78f), new Color(0.35f, 0.9f, 0.85f), 8f, 10f, arena);
        }

        private static void PlaceSquad(SquadController squad, UnityEngine.Camera camera)
        {
            var members = squad.Members;
            Vector3[] spots = { new Vector3(0f, 0.08f, 0f), new Vector3(-1.6f, 0.08f, -2.2f), new Vector3(1.9f, 0.08f, -3f) };
            for (int i = 0; i < members.Count; i++) members[i].transform.SetPositionAndRotation(spots[i], Quaternion.identity);
            var target = GameObject.Find("Camera Orbit Target").transform;
            target.SetPositionAndRotation(spots[0] + Vector3.up * 1.45f, Quaternion.Euler(15f, 0f, 0f));
            camera.transform.SetPositionAndRotation(target.position - target.forward * 5.5f + Vector3.right * 0.35f, target.rotation);
        }

        private static void PlaceEnemies(EncounterController encounter)
        {
            var data = new SerializedObject(encounter);
            var waves = data.FindProperty("waves");
            Vector3[][] spots =
            {
                new[] { new Vector3(-9f, 0f, 103f), new Vector3(-7f, 0f, 103f) },
                new[] { new Vector3(7f, 0f, 103f), new Vector3(9f, 0f, 103f), new Vector3(19f, 0f, 86f) },
                new[] { new Vector3(-8f, 0f, 103.5f), new Vector3(8f, 0f, 103.5f), new Vector3(19f, 0f, 85f), new Vector3(19.5f, 0f, 87.5f) },
            };
            for (int w = 0; w < waves.arraySize && w < spots.Length; w++)
            {
                var enemies = waves.GetArrayElementAtIndex(w).FindPropertyRelative("enemies");
                for (int e = 0; e < enemies.arraySize && e < spots[w].Length; e++)
                {
                    var enemy = (EnemyBrain)enemies.GetArrayElementAtIndex(e).objectReferenceValue;
                    Vector3 facing = new Vector3(0f, 0f, 86f) - spots[w][e];
                    facing.y = 0f;
                    enemy.transform.SetPositionAndRotation(spots[w][e], Quaternion.LookRotation(facing));
                }
            }
        }

        private static void Lighting(UnityEngine.Camera camera)
        {
            var sun = GameObject.Find("Sun").GetComponent<Light>();
            sun.color = new Color(0.55f, 0.66f, 0.82f);
            sun.intensity = 0.62f;
            sun.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.12f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 90f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.44f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.24f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.09f, 0.1f);
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.09f);
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, "Assets/TRACE/Scenes/FirstTraceVolume.asset");
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-12f);
            color.contrast.Override(8f);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.1f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            var volume = new GameObject("Post Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static string BakeNavMesh()
        {
            var surface = level.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 1f, 50f);
            surface.size = new Vector3(46f, 8f, 126f);
            surface.layerMask = 1;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.14f;
            // Doors are closed geometry at bake time; bake them away so later-opened paths exist.
            foreach (var door in bakeExclusions) door.SetActive(false);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            foreach (var door in bakeExclusions) door.SetActive(true);
            foreach (var probe in new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 47f), new Vector3(0f, 0f, 86f), new Vector3(-8f, 0f, 103f), new Vector3(19f, 0f, 86f) })
                if (surface.navMeshData == null || !NavMesh.SamplePosition(probe, out _, 1f, NavMesh.AllAreas))
                    throw new InvalidOperationException("First Trace NavMesh does not cover " + probe);
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/TRACE/Scenes/Navigation/FirstTraceNavMesh.asset");
            EditorUtility.SetDirty(surface);
            return "NavMesh baked";
        }

        // ------------------------------------------------------------------ helpers

        private static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(level, false);
            return go.transform;
        }

        private static Transform Zone(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(level, false);
            go.transform.position = position;
            return go.transform;
        }

        private static GameObject Box(string name, Vector3 center, Vector3 size, string material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : level, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            go.isStatic = true;
            return go;
        }

        // Thin visual with no collider: puddles, moss, emissive panels.
        private static GameObject Decal(string name, Vector3 center, Vector3 size, string material, Transform parent)
        {
            var go = Box(name, center, size, material, parent);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = false;
            go.layer = 2;
            return go;
        }

        private static GateDoor Door(string name, Vector3 center, Vector3 size)
        {
            var go = Box(name, center, size, "Door", doors);
            go.isStatic = false;
            bakeExclusions.Add(go);
            return go.AddComponent<GateDoor>();
        }

        private static Light PointLight(string name, Vector3 position, Color color, float intensity, float range, Transform parent, bool flicker = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            go.AddComponent<UniversalAdditionalLightData>();
            if (flicker) go.AddComponent<FlickerLight>();
            return light;
        }

        // 3D text on a wall: the built-in font shader draws over everything, so the label uses TRACE/WallText
        // (same look, real depth test) and WallText keeps it bound to the dynamic font atlas.
        private static TextMesh Label(string name, Vector3 position, Quaternion rotation, string text, Color color, float characterSize, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var label = go.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = WallTextMaterial(label.font);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            // Rasterise large, scale small: world height = fontSize * characterSize / 10, sharp even up close.
            label.fontSize = 180;
            label.characterSize = characterSize;
            label.text = text;
            label.color = color;
            go.AddComponent<WallText>();
            return label;
        }

        private static Material WallTextMaterial(Font font)
        {
            string path = MaterialFolder + "/FT_WallText.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("TRACE/WallText");
            if (shader == null) throw new InvalidOperationException("TRACE/WallText shader is missing.");
            material = new Material(shader);
            material.mainTexture = font.material.mainTexture;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material Mat(string name)
        {
            if (Materials.TryGetValue(name, out var cached)) return cached;
            string path = MaterialFolder + "/FT_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Smoothness", 0.25f);
                switch (name)
                {
                    case "Concrete": material.SetColor("_BaseColor", new Color(0.36f, 0.39f, 0.43f)); break;
                    case "ConcreteDark": material.SetColor("_BaseColor", new Color(0.22f, 0.24f, 0.27f)); break;
                    case "Rust": material.SetColor("_BaseColor", new Color(0.42f, 0.25f, 0.17f)); material.SetFloat("_Smoothness", 0.15f); break;
                    case "Metal": material.SetColor("_BaseColor", new Color(0.34f, 0.37f, 0.41f)); material.SetFloat("_Metallic", 0.6f); material.SetFloat("_Smoothness", 0.55f); break;
                    case "Crate": material.SetColor("_BaseColor", new Color(0.46f, 0.38f, 0.26f)); break;
                    case "Moss": material.SetColor("_BaseColor", new Color(0.2f, 0.31f, 0.18f)); material.SetFloat("_Smoothness", 0.1f); break;
                    case "Door": material.SetColor("_BaseColor", new Color(0.28f, 0.31f, 0.35f)); material.SetFloat("_Metallic", 0.4f); material.SetFloat("_Smoothness", 0.45f); break;
                    case "Water": material.SetColor("_BaseColor", new Color(0.05f, 0.08f, 0.1f)); material.SetFloat("_Smoothness", 0.95f); material.SetFloat("_Metallic", 0.15f); break;
                    case "ScreenOff": material.SetColor("_BaseColor", new Color(0.04f, 0.05f, 0.06f)); material.SetFloat("_Smoothness", 0.7f); break;
                    case "ScreenDim": Emissive(material, new Color(0.05f, 0.09f, 0.1f), new Color(0.12f, 0.42f, 0.4f)); break;
                    case "ScreenOn": Emissive(material, new Color(0.08f, 0.2f, 0.2f), new Color(0.35f, 1.1f, 1f)); break;
                    case "LampWarm": Emissive(material, new Color(0.6f, 0.45f, 0.3f), new Color(2.2f, 1.5f, 0.9f)); break;
                    case "Warning": Emissive(material, new Color(0.3f, 0.05f, 0.04f), new Color(1.3f, 0.18f, 0.12f)); break;
                }
                AssetDatabase.CreateAsset(material, path);
            }
            Materials[name] = material;
            return material;
        }

        private static void Emissive(Material material, Color baseColor, Color emission)
        {
            material.SetColor("_BaseColor", baseColor);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private static void References(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var value in values) data.FindProperty(value.name).objectReferenceValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedObject data, string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue = value;

        private static void Array(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void Lines(SerializedObject data, string name, params (string speaker, string text, float duration)[] lines)
        {
            var array = data.FindProperty(name);
            array.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = array.GetArrayElementAtIndex(i);
                line.FindPropertyRelative("speaker").stringValue = lines[i].speaker;
                line.FindPropertyRelative("text").stringValue = lines[i].text;
                line.FindPropertyRelative("duration").floatValue = lines[i].duration;
            }
        }
    }
}
