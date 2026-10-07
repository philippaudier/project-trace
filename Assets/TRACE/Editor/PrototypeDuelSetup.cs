using System;
using System.IO;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.Editor
{
    public static class PrototypeDuelSetup
    {
        [MenuItem("TRACE/Add V0.3 Duel To Prototype")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<BasicMeleeEnemy>() != null)
                throw new InvalidOperationException("V0.3 duel already exists; scene was not changed.");
            var input = UnityEngine.Object.FindFirstObjectByType<TracePlayerInput>();
            var motor = input.GetComponent<ThirdPersonMotor>();
            var attack = input.GetComponent<PlayerMeleeAttack>();
            var receiver = Undo.AddComponent<DamageReceiver>(input.gameObject);
            SetReference(motor, "damageReceiver", receiver);
            SetReference(attack, "motor", motor);
            var feedback = Undo.AddComponent<PlayerHealthFeedback>(input.gameObject);
            SetReference(feedback, "input", input);
            SetReference(feedback, "motor", motor);
            SetReference(feedback, "attack", attack);
            SetReference(feedback, "receiver", receiver);
            SetReference(feedback, "bodyRenderer", input.transform.Find("Capsule Visual").GetComponent<Renderer>());

            var area = new GameObject("Duel Area");
            Undo.RegisterCreatedObjectUndo(area, "Add TRACE duel");
            area.transform.position = new Vector3(-11f, 0f, 11f);
            var pad = Primitive("Duel Floor Marking", PrimitiveType.Cube, area.transform,
                new Vector3(-11f, 0.005f, 11f), new Vector3(15f, 0.01f, 15f),
                Material("DuelFloor", new Color(0.24f, 0.30f, 0.38f)));
            UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
            Material obstacles = AssetDatabase.LoadAssetAtPath<Material>("Assets/TRACE/Scenes/Materials/Obstacles.mat");
            Primitive("Duel Obstacle West", PrimitiveType.Cube, area.transform,
                new Vector3(-14f, 1f, 9f), new Vector3(2f, 2f, 3f), obstacles);
            Primitive("Duel Obstacle East", PrimitiveType.Cube, area.transform,
                new Vector3(-8f, 1f, 14f), new Vector3(2f, 2f, 2f), obstacles);
            var surface = area.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = Vector3.up;
            surface.size = new Vector3(16f, 6f, 16f);
            surface.layerMask = 1;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            // Newly authored colliders must reach the physics scene before collection.
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (surface.navMeshData == null || !NavMesh.SamplePosition(area.transform.position, out _, 1f, NavMesh.AllAreas))
                throw new InvalidOperationException("Duel NavMesh bake did not produce a valid spawn.");
            Directory.CreateDirectory("Assets/TRACE/Scenes/Navigation");
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/TRACE/Scenes/Navigation/DuelNavMesh.asset");
            EditorUtility.SetDirty(surface);

            var enemy = new GameObject("BasicMeleeEnemy");
            enemy.SetActive(false);
            enemy.transform.SetParent(area.transform, false);
            enemy.layer = LayerMask.NameToLayer("Damageable");
            enemy.AddComponent<Health>();
            var collider = enemy.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.9f;
            collider.height = 1.8f;
            collider.radius = 0.4f;
            var agent = enemy.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = 2.8f;
            agent.acceleration = 12f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 1.45f;
            var body = Primitive("Enemy Visual", PrimitiveType.Capsule, enemy.transform,
                enemy.transform.position + Vector3.up * 0.9f, new Vector3(0.8f, 0.9f, 0.8f),
                Material("MeleeEnemy", new Color(0.55f, 0.20f, 0.65f)));
            body.layer = enemy.layer;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var impact = enemy.AddComponent<EnemyDummy>();
            SetReference(impact, "visual", body.transform);
            SetReference(impact, "bodyRenderer", body.GetComponent<Renderer>());
            var impactSettings = new SerializedObject(impact);
            var colliders = impactSettings.FindProperty("hitColliders");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider;
            impactSettings.ApplyModifiedPropertiesWithoutUndo();
            var cue = Primitive("Enemy Attack Telegraph", PrimitiveType.Cube, enemy.transform,
                enemy.transform.position, Vector3.one, Material("EnemyTelegraph", new Color(1f, 0.7f, 0.08f)));
            cue.layer = 2;
            UnityEngine.Object.DestroyImmediate(cue.GetComponent<Collider>());
            cue.SetActive(false);
            var brain = enemy.AddComponent<BasicMeleeEnemy>();
            SetReference(brain, "target", receiver);
            SetReference(brain, "targetCollider", input.GetComponent<CharacterController>());
            SetReference(brain, "attackCue", cue);
            SetReference(brain, "cueRenderer", cue.GetComponent<Renderer>());
            enemy.SetActive(true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.3 duel created, NavMesh baked and player wired.");
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/" + name + ".mat");
            return material;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(field).objectReferenceValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
