using System;
using System.Linq;
using TRACE.AI;
using TRACE.Camera;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace TRACE.Editor
{
    public static class PrototypeSwitchSetup
    {
        public const string ScenePath = "Assets/TRACE/Scenes/PrototypeSwitch.unity";

        [MenuItem("TRACE/Create V0.5 Switch Scene (only if missing)")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("V0.5 scene already exists; it will not be overwritten.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.CopyAsset(PrototypeSquadSetup.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy the V0.4 scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            var oldInput = UnityEngine.Object.FindFirstObjectByType<TracePlayerInput>();
            var leader = oldInput.gameObject;
            var sharedInput = squad.gameObject.AddComponent<TracePlayerInput>();
            var oldSettings = new SerializedObject(oldInput);
            Reference(sharedInput, "actions", oldSettings.FindProperty("actions").objectReferenceValue);
            var inputSettings = new SerializedObject(sharedInput);
            inputSettings.FindProperty("captureCursor").boolValue = oldSettings.FindProperty("captureCursor").boolValue;
            inputSettings.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(leader.GetComponent<PlayerHealthFeedback>());
            UnityEngine.Object.DestroyImmediate(oldInput);
            var orbit = UnityEngine.Object.FindFirstObjectByType<ThirdPersonOrbit>();
            Reference(orbit, "input", sharedInput);
            UnityEngine.Object.FindFirstObjectByType<CinemachineThirdPersonFollow>().Damping = new Vector3(0.03f, 0.05f, 0.03f);
            Reference(squad, "input", sharedInput);
            Reference(squad, "orbit", orbit);

            var oldCompanions = UnityEngine.Object.FindObjectsByType<CompanionController>(FindObjectsSortMode.None);
            var actors = new[] { leader, oldCompanions.Single(c => c.Role == CompanionController.CombatRole.Melee).gameObject,
                oldCompanions.Single(c => c.Role == CompanionController.CombatRole.Ranged).gameObject };
            var members = new SquadMember[3];
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                actor.name = "Member " + (i + 1) + (i == 2 ? " - Ranged" : " - Melee");
                var body = actor.GetComponent<CharacterController>();
                if (body == null)
                {
                    UnityEngine.Object.DestroyImmediate(actor.GetComponent<CapsuleCollider>());
                    body = actor.AddComponent<CharacterController>();
                    body.height = 1.8f;
                    body.radius = 0.35f;
                    body.center = Vector3.up * 0.9f;
                    body.slopeLimit = 45f;
                    body.stepOffset = 0.3f;
                    body.skinWidth = 0.035f;
                    body.minMoveDistance = 0f;
                }
                var motor = actor.GetComponent<ThirdPersonMotor>();
                if (motor == null) motor = actor.AddComponent<ThirdPersonMotor>();
                Reference(motor, "input", sharedInput);
                Reference(motor, "movementCamera", UnityEngine.Camera.main.transform);
                Reference(motor, "damageReceiver", actor.GetComponent<DamageReceiver>());
                motor.enabled = i == 0;
                var companion = actor.GetComponent<CompanionController>();
                if (companion == null)
                {
                    companion = actor.AddComponent<CompanionController>();
                    Reference(companion, "squad", squad);
                    var config = new SerializedObject(companion);
                    config.FindProperty("enemyMask").intValue = 1 << LayerMask.NameToLayer("Damageable");
                    config.FindProperty("damage").floatValue = 25f;
                    config.FindProperty("cooldown").floatValue = 0.45f;
                    config.FindProperty("windup").floatValue = 0.08f;
                    config.FindProperty("attackRange").floatValue = 1.8f;
                    config.FindProperty("attackCue").objectReferenceValue = new SerializedObject(actor.GetComponent<PlayerMeleeAttack>())
                        .FindProperty("attackVisual").objectReferenceValue;
                    config.ApplyModifiedPropertiesWithoutUndo();
                    var feedback = actor.AddComponent<CompanionFeedback>();
                    Reference(feedback, "visual", actor.transform.Find("Capsule Visual"));
                    Reference(feedback, "bodyRenderer", actor.transform.Find("Capsule Visual").GetComponent<Renderer>());
                }
                companion.enabled = i != 0;
                actor.AddComponent<TargetedAttack>();
                var agent = actor.GetComponent<NavMeshAgent>();
                agent.radius = 0.45f;
                agent.height = 1.8f;
                agent.acceleration = 14f;
                agent.angularSpeed = 480f;
                agent.stoppingDistance = 0.45f;
                agent.avoidancePriority = 35 + i * 5;
                agent.enabled = i != 0;

                if (i != 2)
                {
                    var attack = actor.GetComponent<PlayerMeleeAttack>();
                    if (attack == null)
                    {
                        attack = actor.AddComponent<PlayerMeleeAttack>();
                        var data = new SerializedObject(attack);
                        data.FindProperty("damage").floatValue = 12f;
                        data.FindProperty("range").floatValue = 1.65f;
                        data.FindProperty("cooldown").floatValue = 1f;
                        data.FindProperty("windup").floatValue = 0.25f;
                        data.FindProperty("targetMask").intValue = 1 << LayerMask.NameToLayer("Damageable");
                        data.FindProperty("attackVisual").objectReferenceValue = new SerializedObject(companion)
                            .FindProperty("attackCue").objectReferenceValue;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    Reference(attack, "input", sharedInput);
                    Reference(attack, "motor", motor);
                    attack.enabled = i == 0;
                }
                else
                {
                    var attack = actor.AddComponent<PlayerRangedAttack>();
                    Reference(attack, "input", sharedInput);
                    Reference(attack, "motor", motor);
                    Reference(attack, "companion", companion);
                    var data = new SerializedObject(attack);
                    data.FindProperty("targetMask").intValue = 1 << LayerMask.NameToLayer("Damageable");
                    data.ApplyModifiedPropertiesWithoutUndo();
                    attack.enabled = false;
                }
                members[i] = actor.AddComponent<SquadMember>();
            }
            var settings = new SerializedObject(squad);
            var roster = settings.FindProperty("members");
            roster.arraySize = 3;
            for (int i = 0; i < 3; i++) roster.GetArrayElementAtIndex(i).objectReferenceValue = members[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            Reference(squad, "activeIndicator", CreateIndicator(squad.transform));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.5 scene created: shared input, three switchable members and active ring.");
        }

        private static Transform CreateIndicator(Transform parent)
        {
            var go = new GameObject("Active Member Ring");
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = 0.055f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(1f, 0.9f, 0.25f));
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/ActiveMember.mat");
            line.sharedMaterial = material;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.65f);
            }
            return go.transform;
        }

        private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(field).objectReferenceValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
