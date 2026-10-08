using System;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TRACE.Editor
{
    public static class PrototypeComboSetup
    {
        [MenuItem("TRACE/Upgrade Prototype to V0.7 Combos (once)")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/TRACE/Scenes/Prototype.unity");
            if (UnityEngine.Object.FindFirstObjectByType<ComboOpportunity>() != null)
                throw new InvalidOperationException("Prototype already has combo opportunities; refusing to overwrite tuning.");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(1f, 0.8f, 0.15f));
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/Combo.mat");
            foreach (var member in UnityEngine.Object.FindObjectsByType<SquadMember>(FindObjectsSortMode.None))
                AddOpportunity(member.gameObject, material);
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None))
                AddOpportunity(enemy.gameObject, material);
            ConfigurePrompt();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.7: combo opportunities and world labels added to three members and four enemies.");
        }

        // Narrow upgrade for prototypes authored before the prompt overlap fix.
        public static void UpdatePrompt()
        {
            var scene = EditorSceneManager.OpenScene("Assets/TRACE/Scenes/Prototype.unity");
            ConfigurePrompt();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigurePrompt()
        {
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            var prompt = squad.GetComponent<ComboPrompt>();
            if (prompt == null) prompt = squad.gameObject.AddComponent<ComboPrompt>();
            var settings = new SerializedObject(prompt);
            settings.FindProperty("squad").objectReferenceValue = squad;
            var feedbacks = UnityEngine.Object.FindObjectsByType<ComboFeedback>(FindObjectsSortMode.None);
            var array = settings.FindProperty("feedbacks");
            array.arraySize = feedbacks.Length;
            for (int i = 0; i < feedbacks.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = feedbacks[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            var roster = new SerializedObject(squad).FindProperty("members");
            for (int i = 0; i < roster.arraySize; i++)
            {
                var member = (SquadMember)roster.GetArrayElementAtIndex(i).objectReferenceValue;
                var data = new SerializedObject(member.GetComponent<ComboFeedback>());
                data.FindProperty("protectedInstruction").stringValue = (i + 1) + " puis clic gauche";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AddOpportunity(GameObject actor, Material material)
        {
            actor.AddComponent<ComboOpportunity>();
            var feedback = actor.AddComponent<ComboFeedback>();
            var ready = Ring(actor.transform, "Combo Ready", 0.45f, 2.15f, 0.07f, material);
            var impact = Ring(actor.transform, "Combo Impact", 1f, 0.08f, 0.12f, material);
            var textObject = new GameObject("Combo Instruction");
            textObject.layer = 2;
            textObject.transform.SetParent(actor.transform, false);
            textObject.transform.localPosition = Vector3.up * 2.8f;
            var label = textObject.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.06f;
            label.text = "COMBO";
            var data = new SerializedObject(feedback);
            data.FindProperty("readyMarker").objectReferenceValue = ready.gameObject;
            data.FindProperty("label").objectReferenceValue = label;
            data.FindProperty("impactRing").objectReferenceValue = impact.transform;
            data.FindProperty("impactRenderer").objectReferenceValue = impact;
            data.ApplyModifiedPropertiesWithoutUndo();
            ready.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
            textObject.SetActive(false);
        }

        private static LineRenderer Ring(Transform parent, string name, float radius, float height, float width, Material material)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
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
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
            return line;
        }
    }
}
