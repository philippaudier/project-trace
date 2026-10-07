using System;
using TRACE.Combat;
using TRACE.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TRACE.Editor
{
    public static class PrototypeCombatSetup
    {
        [MenuItem("TRACE/Add V0.2 Combat To Prototype")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<PlayerMeleeAttack>() != null)
                throw new InvalidOperationException("Prototype combat already exists; no objects were replaced.");
            int layer = EnsureDamageableLayer();
            var input = UnityEngine.Object.FindFirstObjectByType<TracePlayerInput>();
            if (input == null)
                throw new InvalidOperationException("Prototype player input not found.");

            var attack = Undo.AddComponent<PlayerMeleeAttack>(input.gameObject);
            SetReference(attack, "input", input);
            var attackSettings = new SerializedObject(attack);
            attackSettings.FindProperty("targetMask").intValue = 1 << layer;
            attackSettings.ApplyModifiedPropertiesWithoutUndo();
            Material dummyMaterial = CreateMaterial("EnemyDummy", new Color(0.65f, 0.18f, 0.22f));
            Material attackMaterial = CreateMaterial("AttackCue", new Color(1f, 0.77f, 0.18f));
            Material padMaterial = CreateMaterial("CombatArea", new Color(0.31f, 0.32f, 0.35f));
            var cue = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cue.name = "Attack Cue";
            cue.layer = 2;
            cue.transform.SetParent(input.transform, false);
            UnityEngine.Object.DestroyImmediate(cue.GetComponent<Collider>());
            cue.GetComponent<Renderer>().sharedMaterial = attackMaterial;
            cue.SetActive(false);
            SetReference(attack, "attackVisual", cue);

            var area = new GameObject("Combat Practice");
            Undo.RegisterCreatedObjectUndo(area, "Add TRACE combat area");
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Practice Pad";
            pad.transform.SetParent(area.transform, false);
            pad.transform.position = new Vector3(10f, 0.005f, -5f);
            pad.transform.localScale = new Vector3(13f, 0.01f, 6f);
            pad.GetComponent<Renderer>().sharedMaterial = padMaterial;
            // A visual floor marking, not a second ground surface.
            UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
            for (int i = 0; i < 3; i++)
            {
                var dummy = new GameObject("EnemyDummy " + (i + 1));
                dummy.SetActive(false);
                dummy.layer = layer;
                dummy.transform.SetParent(area.transform, false);
                dummy.transform.position = new Vector3(6f + i * 4f, 0f, -5f);
                dummy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var collider = dummy.AddComponent<CapsuleCollider>();
                collider.center = Vector3.up * 0.9f;
                collider.height = 1.8f;
                collider.radius = 0.4f;
                dummy.AddComponent<Health>();
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Dummy Visual";
                body.layer = layer;
                body.transform.SetParent(dummy.transform, false);
                body.transform.localPosition = Vector3.up * 0.9f;
                body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                var renderer = body.GetComponent<Renderer>();
                renderer.sharedMaterial = dummyMaterial;
                var feedback = dummy.AddComponent<EnemyDummy>();
                SetReference(feedback, "visual", body.transform);
                SetReference(feedback, "bodyRenderer", renderer);
                var settings = new SerializedObject(feedback);
                var colliders = settings.FindProperty("hitColliders");
                colliders.arraySize = 1;
                colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider;
                settings.ApplyModifiedPropertiesWithoutUndo();
                dummy.SetActive(true);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.2: player attack and three stationary dummies added.");
        }

        private static int EnsureDamageableLayer()
        {
            int existing = LayerMask.NameToLayer("Damageable");
            if (existing >= 0) return existing;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = "Damageable";
                    settings.ApplyModifiedProperties();
                    return i;
                }
            throw new InvalidOperationException("No free layer for Damageable.");
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = "Assets/TRACE/Scenes/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
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
