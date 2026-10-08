using System;
using TRACE.AI;
using TRACE.Combat;
using TRACE.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TRACE.Editor
{
    // V0.10: one TargetingSystem per squad plus a world-space lock marker, in both playable scenes.
    public static class PrototypeTargetLockSetup
    {
        private static readonly string[] Scenes = { "Assets/TRACE/Scenes/Prototype.unity", "Assets/TRACE/Scenes/PrototypeEncounter.unity" };

        [MenuItem("TRACE/Upgrade scenes to V0.10 Target Lock (once)")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/TRACE/Scenes/Materials/TargetLock.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/TargetLock.mat");
            }
            foreach (string path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path);
                var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
                if (squad.GetComponent<TargetingSystem>() != null)
                    throw new InvalidOperationException(path + " already has a TargetingSystem; refusing to overwrite tuning.");
                var marker = new GameObject("Target Lock Marker");
                marker.layer = 2;
                Diamond(marker.transform, "Outer Diamond", 0.62f, 0.05f, material);
                Diamond(marker.transform, "Inner Diamond", 0.18f, 0.035f, material);
                marker.SetActive(false);
                var targeting = squad.gameObject.AddComponent<TargetingSystem>();
                var data = new SerializedObject(targeting);
                data.FindProperty("input").objectReferenceValue = squad.GetComponent<TracePlayerInput>();
                data.FindProperty("squad").objectReferenceValue = squad;
                data.FindProperty("marker").objectReferenceValue = marker.transform;
                data.FindProperty("candidateMask").intValue = 1 << LayerMask.NameToLayer("Damageable");
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.10: TargetingSystem and lock marker added to Prototype and PrototypeEncounter.");
        }

        // Billboarded diamond in the marker's local XY plane; the system sets the marker rotation to the camera's.
        private static void Diamond(Transform parent, string name, float radius, float width, Material material)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.SetPosition(0, new Vector3(0f, radius, 0f));
            line.SetPosition(1, new Vector3(radius, 0f, 0f));
            line.SetPosition(2, new Vector3(0f, -radius, 0f));
            line.SetPosition(3, new Vector3(-radius, 0f, 0f));
        }
    }
}
