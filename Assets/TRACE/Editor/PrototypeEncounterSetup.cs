using System;
using System.Linq;
using TRACE.AI;
using TRACE.Combat;
using TRACE.Encounter;
using TRACE.Tactical;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace TRACE.Editor
{
    // One-shot authoring of the V0.9 structured encounter. Prototype.unity stays the V0.6–V0.8 fixture.
    public static class PrototypeEncounterSetup
    {
        public const string ScenePath = "Assets/TRACE/Scenes/PrototypeEncounter.unity";
        private const string SourcePath = "Assets/TRACE/Scenes/Prototype.unity";
        private static readonly Vector3 SquadStart = new Vector3(-11f, 0f, 3f);

        private enum Kind { Pursuer, Marksman, Bulwark }

        [MenuItem("TRACE/Create V0.9 Encounter Scene (only if missing)")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("V0.9 encounter scene already exists; it will not be overwritten.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.CopyAsset(SourcePath, ScenePath))
                throw new InvalidOperationException("Could not copy the prototype scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            var focus = squad.GetComponent<TacticalFocus>();
            var overlay = squad.GetComponent<TacticalOverlay>();
            var existing = UnityEngine.Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            if (existing.Length != 4) throw new InvalidOperationException("Expected the four V0.6 enemies as a template source.");
            var template = existing[0].gameObject;
            for (int i = 1; i < existing.Length; i++) UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
            template.SetActive(false);

            var marksmanMaterial = Lit("EnemyMarksman", new Color(0.78f, 0.84f, 0.18f));
            var bulwarkMaterial = Lit("EnemyBulwark", new Color(0.56f, 0.12f, 0.10f));
            var plateMaterial = Lit("BulwarkPlate", new Color(0.58f, 0.64f, 0.72f));
            var aimMaterial = Unlit("MarksmanAim", new Color(1f, 0.45f, 0.1f));
            var shotMaterial = Unlit("MarksmanShot", new Color(1f, 0.92f, 0.35f));
            var arcMaterial = Unlit("GuardArc", new Color(0.66f, 0.77f, 0.9f));
            var spawnMaterial = Unlit("SpawnMarker", new Color(1f, 0.32f, 0.2f));

            var root = new GameObject("Encounter");
            (string name, (Kind kind, Vector3 position)[] enemies)[] plan =
            {
                ("APPRENTISSAGE", new[] { (Kind.Pursuer, new Vector3(-13.5f, 0f, 16f)), (Kind.Pursuer, new Vector3(-8.5f, 0f, 16f)) }),
                ("COMBINAISON", new[] { (Kind.Pursuer, new Vector3(-16f, 0f, 14f)), (Kind.Pursuer, new Vector3(-6f, 0f, 15.5f)),
                    (Kind.Marksman, new Vector3(-11f, 0f, 19f)) }),
                ("TACTIQUE", new[] { (Kind.Bulwark, new Vector3(-11f, 0f, 15f)), (Kind.Marksman, new Vector3(-17.5f, 0f, 9.5f)),
                    (Kind.Pursuer, new Vector3(-5f, 0f, 13f)), (Kind.Pursuer, new Vector3(-7.5f, 0f, 18f)) }),
            };
            var waves = new EnemyBrain[plan.Length][];
            for (int w = 0; w < plan.Length; w++)
            {
                var waveRoot = new GameObject("Wave " + (w + 1)).transform;
                waveRoot.SetParent(root.transform, false);
                waves[w] = new EnemyBrain[plan[w].enemies.Length];
                int letter = 0;
                for (int e = 0; e < plan[w].enemies.Length; e++)
                {
                    var (kind, position) = plan[w].enemies[e];
                    if (!NavMesh.SamplePosition(position, out NavMeshHit ground, 0.6f, NavMesh.AllAreas))
                        throw new InvalidOperationException($"Spawn {kind} at {position} is not on the baked NavMesh.");
                    var go = UnityEngine.Object.Instantiate(template, waveRoot);
                    string suffix = kind == Kind.Pursuer ? " " + (char)('A' + letter++) : "";
                    go.name = $"W{w + 1} {kind}{suffix}";
                    Vector3 facing = SquadStart - ground.position;
                    facing.y = 0f;
                    go.transform.SetPositionAndRotation(ground.position, Quaternion.LookRotation(facing));
                    waves[w][e] = kind switch
                    {
                        Kind.Marksman => Marksman(go, squad, marksmanMaterial, aimMaterial, shotMaterial),
                        Kind.Bulwark => Bulwark(go, focus, bulwarkMaterial, plateMaterial, arcMaterial),
                        _ => Pursuer(go),
                    };
                    go.SetActive(false);
                }
            }
            UnityEngine.Object.DestroyImmediate(template);
            var all = waves.SelectMany(w => w).ToArray();

            // Spawn warnings: one ring per possible simultaneous enemy.
            var markersRoot = new GameObject("Spawn Markers").transform;
            markersRoot.SetParent(root.transform, false);
            var markers = new Transform[plan.Max(p => p.enemies.Length)];
            for (int i = 0; i < markers.Length; i++)
            {
                var ring = Ring(markersRoot, "Spawn Marker " + (i + 1), 1.1f, 0.02f, 0.1f, spawnMaterial);
                markers[i] = ring.transform;
                ring.gameObject.SetActive(false);
            }

            var encounter = root.AddComponent<EncounterController>();
            var data = new SerializedObject(encounter);
            data.FindProperty("squad").objectReferenceValue = squad;
            data.FindProperty("focus").objectReferenceValue = focus;
            var waveArray = data.FindProperty("waves");
            waveArray.arraySize = waves.Length;
            for (int w = 0; w < waves.Length; w++)
            {
                var wave = waveArray.GetArrayElementAtIndex(w);
                wave.FindPropertyRelative("name").stringValue = plan[w].name;
                var list = wave.FindPropertyRelative("enemies");
                list.arraySize = waves[w].Length;
                for (int e = 0; e < waves[w].Length; e++) list.GetArrayElementAtIndex(e).objectReferenceValue = waves[w][e];
            }
            Array(data.FindProperty("spawnMarkers"), markers);
            data.ApplyModifiedPropertiesWithoutUndo();
            var hud = root.AddComponent<EncounterHud>();
            References(hud, ("encounter", encounter), ("focus", focus));
            var threat = root.AddComponent<ThreatIndicator>();
            var threatData = new SerializedObject(threat);
            threatData.FindProperty("squad").objectReferenceValue = squad;
            Array(threatData.FindProperty("enemies"), all);
            threatData.ApplyModifiedPropertiesWithoutUndo();

            // Tactical overlay: nine cards instead of four, same panel prefab-like object.
            var overlayData = new SerializedObject(overlay);
            var panels = overlayData.FindProperty("enemyPanels");
            var firstPanel = (RectTransform)panels.GetArrayElementAtIndex(0).objectReferenceValue;
            var panelList = Enumerable.Range(0, panels.arraySize).Select(i => (RectTransform)panels.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            while (panelList.Count < all.Length)
            {
                var panel = UnityEngine.Object.Instantiate(firstPanel.gameObject, firstPanel.parent).GetComponent<RectTransform>();
                panel.name = "Enemy " + (panelList.Count + 1);
                panelList.Add(panel);
            }
            foreach (var panel in panelList) panel.sizeDelta = new Vector2(262f, 88f);
            Array(overlayData.FindProperty("enemies"), all);
            Array(overlayData.FindProperty("enemyPanels"), panelList.ToArray());
            Array(overlayData.FindProperty("enemyTexts"), panelList.Select(p => p.GetComponentInChildren<Text>(true)).ToArray());
            overlayData.ApplyModifiedPropertiesWithoutUndo();
            var prompt = new SerializedObject(squad.GetComponent<ComboPrompt>());
            Array(prompt.FindProperty("feedbacks"), UnityEngine.Object.FindObjectsByType<ComboFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            prompt.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.9 authored: three waves, nine enemies of three archetypes, encounter controller.");
        }

        private static EnemyBrain Pursuer(GameObject go)
        {
            var brain = go.GetComponent<BasicMeleeEnemy>();
            Numbers(brain, ("detectionRange", 30f), ("loseTargetRange", 45f), ("damage", 18f));
            Text(brain, "archetype", "PURSUER");
            Numbers(go.GetComponent<Health>(), ("maxHealth", 80f));
            var agent = go.GetComponent<NavMeshAgent>();
            agent.speed = 3.6f;
            agent.acceleration = 16f;
            var visual = go.transform.Find("Enemy Visual");
            visual.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            visual.localPosition = Vector3.up * 0.8f;
            return brain;
        }

        private static EnemyBrain Bulwark(GameObject go, TacticalFocus focus, Material body, Material plateMaterial, Material arcMaterial)
        {
            var brain = go.GetComponent<BasicMeleeEnemy>();
            Numbers(brain, ("detectionRange", 30f), ("loseTargetRange", 45f), ("attackRange", 2.0f), ("hitRange", 2.4f),
                ("hitHalfWidth", 1.1f), ("damage", 30f), ("windup", 0.9f), ("activeDuration", 0.2f), ("recovery", 1.1f));
            Text(brain, "archetype", "BULWARK");
            Numbers(go.GetComponent<Health>(), ("maxHealth", 180f));
            var agent = go.GetComponent<NavMeshAgent>();
            agent.speed = 1.8f;
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;
            agent.radius = 0.6f;
            agent.stoppingDistance = 1.8f;
            var collider = go.GetComponent<CapsuleCollider>();
            collider.radius = 0.6f;
            collider.height = 1.9f;
            collider.center = Vector3.up * 0.95f;
            var visual = go.transform.Find("Enemy Visual");
            visual.localScale = new Vector3(1.35f, 1.0f, 1.35f);
            visual.localPosition = Vector3.up * 1.0f;
            visual.GetComponent<Renderer>().sharedMaterial = body;
            var plate = Primitive("Guard Plate", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.95f, 0.78f), new Vector3(1.5f, 1.5f, 0.12f), plateMaterial);
            var label = Label(go.transform, "Guard Label", 2.45f);
            var arc = Line(go.transform, "Guard Arc", 14, true, arcMaterial, 0.06f);
            arc.gameObject.SetActive(false);
            var guard = go.AddComponent<FrontalGuard>();
            References(guard, ("plate", plate.GetComponent<Renderer>()), ("label", label), ("arc", arc), ("focus", focus));
            return brain;
        }

        private static EnemyBrain Marksman(GameObject go, SquadController squad, Material body, Material aimMaterial, Material shotMaterial)
        {
            UnityEngine.Object.DestroyImmediate(go.GetComponent<TacticalTelegraph>());
            UnityEngine.Object.DestroyImmediate(go.transform.Find("Tactical Telegraph").gameObject);
            UnityEngine.Object.DestroyImmediate(go.transform.Find("Enemy Attack Telegraph").gameObject);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<BasicMeleeEnemy>());
            Numbers(go.GetComponent<Health>(), ("maxHealth", 70f));
            var agent = go.GetComponent<NavMeshAgent>();
            agent.speed = 3.4f;
            agent.acceleration = 14f;
            agent.angularSpeed = 360f;
            agent.radius = 0.4f;
            agent.stoppingDistance = 0.3f;
            var visual = go.transform.Find("Enemy Visual");
            visual.localScale = new Vector3(0.55f, 1.0f, 0.55f);
            visual.localPosition = Vector3.up * 1.0f;
            visual.GetComponent<Renderer>().sharedMaterial = body;
            Primitive("Barrel", PrimitiveType.Cube, go.transform, new Vector3(0.32f, 1.25f, 0.5f), new Vector3(0.14f, 0.14f, 1.0f), body);
            var aim = Line(go.transform, "Aim Line", 2, false, aimMaterial, 0.04f);
            aim.gameObject.SetActive(false);
            var shot = new GameObject("Shot");
            shot.layer = 2;
            shot.transform.SetParent(go.transform, false);
            Primitive("Shot Visual", PrimitiveType.Sphere, shot.transform, Vector3.zero, Vector3.one * 0.36f, shotMaterial);
            var projectile = shot.AddComponent<Projectile>();
            shot.SetActive(false);
            var brain = go.AddComponent<MarksmanEnemy>();
            References(brain, ("squad", squad), ("projectile", projectile), ("aimLine", aim));
            return brain;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static TextMesh Label(Transform parent, string name, float height)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            var label = go.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 44;
            label.characterSize = 0.055f;
            label.text = "BLOQUE";
            go.SetActive(false);
            return label;
        }

        private static LineRenderer Line(Transform parent, string name, int count, bool loop, Material material, float width)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = count;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static LineRenderer Ring(Transform parent, string name, float radius, float height, float width, Material material)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
            return line;
        }

        private static Material Lit(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/" + name + ".mat");
            return material;
        }

        private static Material Unlit(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/" + name + ".mat");
            return material;
        }

        private static void References(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var value in values) data.FindProperty(value.name).objectReferenceValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Numbers(UnityEngine.Object target, params (string name, float value)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var value in values) data.FindProperty(value.name).floatValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Text(UnityEngine.Object target, string name, string value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Array(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
