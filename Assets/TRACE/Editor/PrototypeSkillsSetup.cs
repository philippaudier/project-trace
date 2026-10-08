using System;
using System.Linq;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Skills;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TRACE.Editor
{
    public static class PrototypeSkillsSetup
    {
        private const string ScenePath = "Assets/TRACE/Scenes/Prototype.unity";
        private const string LegacyPath = "Assets/TRACE/Scenes/PrototypeLegacy.unity";

        [MenuItem("TRACE/Upgrade Prototype to V0.6 Skills (once)")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<CharacterSkill>() != null)
                throw new InvalidOperationException("Prototype already has skills; refusing to overwrite its tuning.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyPath) != null || !AssetDatabase.CopyAsset(ScenePath, LegacyPath))
                throw new InvalidOperationException("Legacy snapshot already exists or could not be created.");
            var oldPlayer = UnityEngine.Object.FindFirstObjectByType<TracePlayerInput>().gameObject;
            var camera = UnityEngine.Camera.main;
            var originalEnemy = UnityEngine.Object.FindFirstObjectByType<BasicMeleeEnemy>();
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            foreach (string root in new[] { "Player Camera Rig", "Camera Orbit Target" })
                UnityEngine.Object.DestroyImmediate(scene.GetRootGameObjects().Single(g => g.name == root));
            UnityEngine.Object.DestroyImmediate(oldPlayer);

            // Move only the proven V0.5 actors and camera rig into the existing playground.
            var source = EditorSceneManager.OpenScene(PrototypeSwitchSetup.ScenePath, OpenSceneMode.Additive);
            foreach (string root in new[] { "Squad", "Member 1 - Melee", "Player Camera Rig", "Camera Orbit Target" })
                SceneManager.MoveGameObjectToScene(source.GetRootGameObjects().Single(g => g.name == root), scene);
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(scene);
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            var input = squad.GetComponent<TracePlayerInput>();
            var roster = new SerializedObject(squad).FindProperty("members");
            var members = Enumerable.Range(0, 3).Select(i => (SquadMember)roster.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            string[] roles = { "Assault", "Control", "Support" };
            var skills = new CharacterSkill[3];
            var shieldMaterial = Material("SkillShield", new Color(0.15f, 0.85f, 1f));
            var gravityMaterial = Material("SkillGravity", new Color(0.75f, 0.25f, 1f));
            var dashMaterial = Material("SkillDash", new Color(1f, 0.4f, 0.08f));
            for (int i = 0; i < members.Length; i++)
            {
                var actor = members[i].gameObject;
                actor.name = "Member " + (i + 1) + " - " + roles[i];
                actor.transform.position = new Vector3(-11f + (i == 0 ? 0f : i == 1 ? -1.6f : 1.9f), 0.08f, i == 0 ? 3f : 0.5f);
                actor.transform.rotation = Quaternion.identity;
                Reference(actor.GetComponent<ThirdPersonMotor>(), "movementCamera", camera.transform);
                var shield = actor.AddComponent<Shield>();
                var halo = new GameObject("Shield Halo");
                halo.transform.SetParent(actor.transform, false);
                Ring(halo.transform, "Lower Halo", 0.72f, 0.35f, shieldMaterial);
                Ring(halo.transform, "Upper Halo", 0.72f, 1.5f, shieldMaterial);
                halo.SetActive(false);
                Reference(shield, "halo", halo);
                skills[i] = i == 0 ? actor.AddComponent<DashStrike>() : i == 1 ? actor.AddComponent<GravityFieldSkill>() : actor.AddComponent<PulseShield>();
                Reference(skills[i], "input", input);
                Number(skills[i], "cooldown", i == 0 ? 5f : i == 1 ? 8f : 10f);
            }
            var dash = new GameObject("Dash Orange Burst");
            dash.transform.SetParent(members[0].transform, false);
            Ring(dash.transform, "Dash Ring", 0.9f, 0.5f, dashMaterial);
            dash.SetActive(false);
            Reference(skills[0], "dashVisual", dash);
            Mask(skills[0], "targetMask");
            var zone = new GameObject("Gravity Field");
            var field = zone.AddComponent<GravityField>();
            var zoneVisual = new GameObject("Gravity Visual");
            zoneVisual.transform.SetParent(zone.transform, false);
            Ring(zoneVisual.transform, "Outer Radius", 1f, 0.055f, gravityMaterial);
            Ring(zoneVisual.transform, "Inner Radius", 0.66f, 0.055f, gravityMaterial);
            Ring(zoneVisual.transform, "Core", 0.15f, 0.055f, gravityMaterial);
            Reference(field, "visual", zoneVisual.transform);
            Mask(field, "enemyMask");
            zone.SetActive(false);
            Reference(skills[1], "field", field);
            Mask(skills[1], "targetMask");
            Reference(skills[2], "squad", squad);
            var hud = squad.gameObject.AddComponent<SkillHud>();
            Reference(hud, "squad", squad);
            var hudData = new SerializedObject(hud);
            var skillArray = hudData.FindProperty("skills");
            skillArray.arraySize = 3;
            for (int i = 0; i < 3; i++) skillArray.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
            hudData.ApplyModifiedPropertiesWithoutUndo();

            Reference(originalEnemy, "squad", squad);
            Reference(originalEnemy, "target", members[0].GetComponent<DamageReceiver>());
            Reference(originalEnemy, "targetCollider", members[0].GetComponent<CharacterController>());
            originalEnemy.gameObject.AddComponent<EnemyGravityResponse>();
            Number(originalEnemy, "detectionRange", 7f);
            Vector3[] positions = { new Vector3(-12f, 0f, 10f), new Vector3(-9.5f, 0f, 10.5f),
                new Vector3(-13f, 0f, 13f), new Vector3(-10f, 0f, 13.5f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var enemy = i == 0 ? originalEnemy.gameObject : UnityEngine.Object.Instantiate(originalEnemy.gameObject, originalEnemy.transform.parent);
                enemy.name = "Skill Encounter Enemy " + (i + 1);
                enemy.transform.position = positions[i];
            }
            // Bake a separate navigation asset; previous scenes keep their own navigation untouched.
            surface.RemoveData();
            surface.navMeshData = null;
            surface.center = new Vector3(11f, 1f, -11f);
            surface.size = new Vector3(40f, 10f, 40f);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/TRACE/Scenes/Navigation/SkillsNavMesh.asset");
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(LegacyPath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != LegacyPath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.6 authored: three skills, four enemies, legacy scene preserved.");
        }

        private static void Ring(Transform parent, string name, float radius, float height, Material material)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.widthMultiplier = 0.055f;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
        }
        private static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/" + name + ".mat");
            return material;
        }
        private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Number(UnityEngine.Object target, string field, float value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Mask(UnityEngine.Object target, string field)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).intValue = 1 << LayerMask.NameToLayer("Damageable");
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
