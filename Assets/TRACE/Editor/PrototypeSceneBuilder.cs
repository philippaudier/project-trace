using System;
using System.IO;
using System.Linq;
using TRACE.Camera;
using TRACE.Characters;
using TRACE.Input;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TRACE.Editor
{
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/TRACE/Scenes/Prototype.unity";

        [MenuItem("TRACE/Create Prototype Scene (only if missing)")]
        public static void Create()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Prototype already exists; it will not be overwritten.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            Directory.CreateDirectory("Assets/TRACE/Scenes/Materials");
            AssetDatabase.Refresh();
            Material floor = Material("Floor", new Color(0.22f, 0.27f, 0.30f));
            Material wall = Material("Obstacles", new Color(0.48f, 0.55f, 0.60f));
            Material ramp = Material("Traversal", new Color(0.23f, 0.50f, 0.48f));
            Material body = Material("Player", new Color(0.90f, 0.47f, 0.16f));
            Material face = Material("ForwardMarker", new Color(0.14f, 0.19f, 0.23f));

            var environment = new GameObject("Environment").transform;
            Box("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(40f, 0.5f, 40f), floor, environment);
            Box("Obstacle Low", new Vector3(-4f, 0.5f, 3f), new Vector3(2f, 1f, 2f), wall, environment);
            Box("Obstacle Tall", new Vector3(-7f, 1.5f, 6f), new Vector3(2f, 3f, 3f), wall, environment);
            Box("Camera Test Wall", new Vector3(-6f, 2.5f, -4f), new Vector3(10f, 5f, 0.6f), wall, environment);
            Box("Camera Test Corner", new Vector3(-11f, 2.5f, -1f), new Vector3(0.6f, 5f, 6f), wall, environment);
            var slope = Box("Ramp 20 degrees", new Vector3(0f, 0.83812f, 7f), new Vector3(4f, 0.4f, 6f), ramp, environment);
            slope.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            Box("Upper Platform", new Vector3(0f, 1.02606f, 12f), new Vector3(6f, 2.05212f, 4.6f), ramp, environment);
            for (int i = 0; i < 4; i++)
                Box("Step " + (i + 1), new Vector3(7f, (i + 1) * 0.1f, 3f + i),
                    new Vector3(3f, (i + 1) * 0.2f, 1f), ramp, environment);
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2;
                Box("Boundary " + i, new Vector3(alongX ? (i == 0 ? -20f : 20f) : 0f, 1.5f,
                        alongX ? 0f : (i == 2 ? -20f : 20f)),
                    alongX ? new Vector3(0.5f, 3f, 40f) : new Vector3(40f, 3f, 0.5f), wall, environment);
            }

            var player = new GameObject("Player");
            player.SetActive(false);
            player.tag = "Player";
            player.layer = 2; // Ignore Raycast: ground probes and camera filter use only Default.
            player.transform.position = new Vector3(0f, 0.08f, 0f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.035f;
            controller.minMoveDistance = 0f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Capsule Visual";
            visual.layer = 2;
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = body;
            var marker = Box("Facing Marker", Vector3.zero, new Vector3(0.28f, 0.16f, 0.15f), face, player.transform);
            marker.layer = 2;
            marker.transform.localPosition = new Vector3(0f, 1.4f, 0.34f);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            var input = player.AddComponent<TracePlayerInput>();
            SetReference(input, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/TRACE/Input/TRACEInput.inputactions"));

            var main = new GameObject("Main Camera");
            main.tag = "MainCamera";
            var camera = main.AddComponent<UnityEngine.Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.fieldOfView = 60f;
            main.AddComponent<AudioListener>();
            main.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            var brain = main.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var target = new GameObject("Camera Orbit Target").transform;
            target.position = player.transform.position + Vector3.up * 1.45f;
            target.rotation = Quaternion.Euler(15f, 0f, 0f);
            var rig = new GameObject("Player Camera Rig");
            var cm = rig.AddComponent<CinemachineCamera>();
            cm.Follow = target;
            cm.Lens.FieldOfView = 60f;
            cm.Lens.NearClipPlane = 0.1f;
            cm.Lens.FarClipPlane = 150f;
            var follow = rig.AddComponent<CinemachineThirdPersonFollow>();
            follow.CameraDistance = 5.5f;
            follow.ShoulderOffset = new Vector3(0.35f, 0f, 0f);
            follow.VerticalArmLength = 0f;
            follow.CameraSide = 1f;
            follow.Damping = new Vector3(0.08f, 0.15f, 0.08f);
            follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
            {
                Enabled = true, CollisionFilter = 1, IgnoreTag = "Player", CameraRadius = 0.25f,
                DampingIntoCollision = 0f, DampingFromCollision = 0.4f
            };
            var orbit = rig.AddComponent<ThirdPersonOrbit>();
            SetReference(orbit, "input", input);
            SetReference(orbit, "player", player.transform);
            SetReference(orbit, "orbitTarget", target);
            var motor = player.AddComponent<ThirdPersonMotor>();
            SetReference(motor, "input", input);
            SetReference(motor, "movementCamera", main.transform);
            player.SetActive(true);
            main.transform.SetPositionAndRotation(target.position - target.forward * follow.CameraDistance, target.rotation);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            light.gameObject.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.39f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.20f, 0.22f);
            RenderSettings.skybox = null;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.22f, 0.28f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE prototype scene created and wired.");
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/TRACE/Scenes/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetReference(UnityEngine.Object component, string field, UnityEngine.Object value)
        {
            if (value == null)
                throw new InvalidOperationException("Missing reference: " + field);
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
