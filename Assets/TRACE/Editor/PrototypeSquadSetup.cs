using System;
using System.Linq;
using TRACE.AI;
using TRACE.Camera;
using TRACE.Combat;
using TRACE.Input;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.Editor
{
    public static class PrototypeSquadSetup
    {
        public const string ScenePath = "Assets/TRACE/Scenes/PrototypeSquad.unity";

        [MenuItem("TRACE/Create V0.4 Squad Scene (only if missing)")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("V0.4 scene already exists; it will not be overwritten.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.CopyAsset(PrototypeSceneBuilder.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy the prototype scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var input = UnityEngine.Object.FindFirstObjectByType<TracePlayerInput>();
            input.gameObject.name = "PlayerLeader";
            input.transform.position = new Vector3(3f, 0.08f, 0f);
            // The same orbit/occlusion behavior, framed for three characters in this scene only.
            UnityEngine.Object.FindFirstObjectByType<CinemachineThirdPersonFollow>().CameraDistance = 7.5f;
            var orbit = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<ThirdPersonOrbit>());
            orbit.FindProperty("targetHeight").floatValue = 1.8f;
            orbit.FindProperty("initialPitch").floatValue = 25f;
            orbit.ApplyModifiedPropertiesWithoutUndo();
            var root = new GameObject("Squad");
            var squad = root.AddComponent<SquadController>();
            Reference(squad, "leader", input.GetComponent<DamageReceiver>());

            // Replace only the COPY's navigation data. Preserve the V0.3 duel and its bake.
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            surface.RemoveData();
            surface.navMeshData = null;
            surface.center = new Vector3(11f, 1f, -11f); // World centre, surface is at (-11,0,11).
            surface.size = new Vector3(40f, 10f, 40f);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (surface.navMeshData == null || !NavMesh.SamplePosition(input.transform.position, out _, 1f, NavMesh.AllAreas))
                throw new InvalidOperationException("Squad NavMesh bake did not cover the player spawn.");
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/TRACE/Scenes/Navigation/SquadNavMesh.asset");
            EditorUtility.SetDirty(surface);

            CompanionController melee = Companion("Companion A - Melee", squad, root.transform, false);
            CompanionController ranged = Companion("Companion B - Ranged", squad, root.transform, true);
            var settings = new SerializedObject(squad);
            var members = settings.FindProperty("companions");
            members.arraySize = 2;
            members.GetArrayElementAtIndex(0).objectReferenceValue = melee;
            members.GetArrayElementAtIndex(1).objectReferenceValue = ranged;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var original = UnityEngine.Object.FindFirstObjectByType<BasicMeleeEnemy>();
            original.name = "Squad Enemy 1";
            Reference(original, "squad", squad);
            // Three readable opponents inside the existing 15x15m duel zone.
            for (int i = 0; i < 2; i++)
            {
                var enemy = UnityEngine.Object.Instantiate(original.gameObject, original.transform.parent);
                enemy.name = "Squad Enemy " + (i + 2);
                enemy.transform.position = i == 0 ? new Vector3(-16f, 0f, 15f) : new Vector3(-5f, 0f, 9f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.4 squad scene created with three enemies and baked navigation.");
        }

        private static CompanionController Companion(string name, SquadController squad, Transform parent, bool ranged)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.layer = 2; // Same non-camera/non-player-hit layer as the leader.
            go.transform.SetParent(parent, false);
            Vector3 offset = ranged ? new Vector3(1.9f, 0f, -3f) : new Vector3(-1.6f, 0f, -2.2f);
            NavMesh.SamplePosition(squad.Leader.transform.position + offset, out NavMeshHit spawn, 2f, NavMesh.AllAreas);
            go.transform.position = spawn.position;
            go.AddComponent<Health>();
            go.AddComponent<DamageReceiver>();
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.9f;
            collider.height = 1.8f;
            collider.radius = 0.35f;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f;
            agent.height = 1.8f;
            agent.speed = 4.2f;
            agent.acceleration = 14f;
            agent.angularSpeed = 480f;
            agent.stoppingDistance = 0.45f;
            agent.avoidancePriority = ranged ? 45 : 40;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            Color color = ranged ? new Color(0.18f, 0.65f, 1f) : new Color(0.25f, 0.85f, 0.45f);
            Material material = MakeMaterial(ranged ? "CompanionRanged" : "CompanionMelee", color);
            var visual = Primitive("Body", PrimitiveType.Capsule, go.transform, Vector3.up * 0.9f,
                new Vector3(0.7f, 0.9f, 0.7f), material);
            var feedback = go.AddComponent<CompanionFeedback>();
            Reference(feedback, "visual", visual.transform);
            Reference(feedback, "bodyRenderer", visual.GetComponent<Renderer>());
            Primitive(ranged ? "Ranged Marker" : "Melee Marker", ranged ? PrimitiveType.Sphere : PrimitiveType.Cube,
                visual.transform, Vector3.up * 1.35f, Vector3.one * 0.28f, material);
            var cue = Primitive("Attack Cue", PrimitiveType.Cube, go.transform, Vector3.zero, Vector3.one,
                MakeMaterial(ranged ? "CompanionShot" : "CompanionStrike", color * 1.3f));
            cue.SetActive(false);
            var brain = go.AddComponent<CompanionController>();
            ConfigureCompanion(brain, squad, offset, ranged, cue);
            go.SetActive(true);
            return brain;
        }

        private static void ConfigureCompanion(CompanionController brain, SquadController squad, Vector3 offset, bool ranged, GameObject cue)
        {
            var data = new SerializedObject(brain);
            data.FindProperty("squad").objectReferenceValue = squad;
            data.FindProperty("formationOffset").vector3Value = offset;
            data.FindProperty("role").enumValueIndex = ranged ? 1 : 0;
            data.FindProperty("enemyMask").intValue = 1 << LayerMask.NameToLayer("Damageable");
            data.FindProperty("attackRange").floatValue = ranged ? 6f : 1.65f;
            data.FindProperty("preferredRange").floatValue = ranged ? 4.5f : 1.25f;
            data.FindProperty("damage").floatValue = ranged ? 8f : 12f;
            data.FindProperty("cooldown").floatValue = ranged ? 1.4f : 1f;
            data.FindProperty("windup").floatValue = ranged ? 0.35f : 0.25f;
            data.FindProperty("attackCue").objectReferenceValue = cue;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/" + name + ".mat");
            return material;
        }

        private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
