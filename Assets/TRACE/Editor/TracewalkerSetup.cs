using System;
using System.Linq;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Tactical;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TRACE.Editor
{
    // Dresses member 1 (the former Assault placeholder) as the Tracewalker in every playable scene: composed
    // primitive body in the concept palette, ORIGIN mark on the left brassard, Trace Module on the chest,
    // short technical blade, identity profile. Gameplay components and tuning are untouched.
    public static class TracewalkerSetup
    {
        public const string MaterialFolder = "Assets/TRACE/Scenes/Materials/Tracewalker";
        public const string MemberName = "Member 1 - Tracewalker";
        public static readonly Color Amber = new Color(0.95f, 0.7f, 0.16f);
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
        };

        [MenuItem("TRACE/Apply Tracewalker V0.1")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/TRACE/Scenes/Materials", "Tracewalker");
            int applied = 0;
            foreach (string path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var member = FindMember(scene);
                if (member == null) throw new InvalidOperationException(path + " has no squad member 1.");
                Dress(member);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Tracewalker V0.1 applied to {applied} scene(s).");
        }

        private static GameObject FindMember(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SquadMember>(true))
                .Select(m => m.gameObject).FirstOrDefault(g => g.name == "Member 1 - Assault" || g.name == MemberName);
        }

        private static void Dress(GameObject member)
        {
            member.name = MemberName;
            foreach (string old in new[] { "Tracewalker Visual", "Capsule Visual", "Facing Marker" })
            {
                var stale = member.transform.Find(old);
                if (stale != null) UnityEngine.Object.DestroyImmediate(stale.gameObject);
            }
            var charcoal = Mat("M_Tracewalker_Charcoal", new Color(0.13f, 0.14f, 0.15f), 0.25f);
            var shadow = Mat("M_Tracewalker_Shadow", new Color(0.05f, 0.05f, 0.06f), 0.3f);
            var grey = Mat("M_Tracewalker_CoolGrey", new Color(0.55f, 0.58f, 0.62f), 0.2f);
            var offWhite = Mat("M_Tracewalker_OffWhite", new Color(0.86f, 0.86f, 0.83f), 0.2f);
            var amber = Mat("M_Tracewalker_Amber", Amber, 0.3f);
            var module = Mat("M_Tracewalker_Module", new Color(0.12f, 0.11f, 0.08f), 0.4f, emission: Amber * 0.8f);
            var blade = Mat("M_Tracewalker_Blade", new Color(0.3f, 0.32f, 0.35f), 0.55f, metallic: 0.6f);

            var visual = new GameObject("Tracewalker Visual");
            visual.layer = 2;
            visual.transform.SetParent(member.transform, false);
            var root = visual.transform;
            // Lean silhouette inside the unchanged 1.8 m capsule: legs, boots, torso, arms, coat panels, hood, head.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.13f * side;
                Part("Leg", PrimitiveType.Cube, root, new Vector3(x, 0.5f, 0f), Vector3.zero, new Vector3(0.2f, 0.84f, 0.24f), charcoal);
                Part("Boot", PrimitiveType.Cube, root, new Vector3(x, 0.08f, 0.03f), Vector3.zero, new Vector3(0.22f, 0.16f, 0.32f), shadow);
                Part("Boot Trim", PrimitiveType.Cube, root, new Vector3(x, 0.02f, 0.03f), Vector3.zero, new Vector3(0.23f, 0.03f, 0.33f), amber);
                float ax = 0.33f * side;
                Part("Arm", PrimitiveType.Cube, root, new Vector3(ax, 1.08f, 0f), Vector3.zero, new Vector3(0.14f, 0.52f, 0.16f), charcoal);
                Part("Glove", PrimitiveType.Cube, root, new Vector3(ax, 0.76f, 0.02f), Vector3.zero, new Vector3(0.15f, 0.14f, 0.17f), shadow);
                Part("Shoulder", PrimitiveType.Cube, root, new Vector3(0.3f * side, 1.42f, 0f), Vector3.zero, new Vector3(0.18f, 0.12f, 0.26f), offWhite);
                Part("Coat Side", PrimitiveType.Cube, root, new Vector3(0.27f * side, 0.92f, -0.03f), Vector3.zero, new Vector3(0.06f, 0.95f, 0.3f), offWhite);
            }
            var torso = Part("Torso", PrimitiveType.Cube, root, new Vector3(0f, 1.15f, 0f), Vector3.zero, new Vector3(0.46f, 0.56f, 0.28f), charcoal);
            Part("Harness", PrimitiveType.Cube, root, new Vector3(0f, 1.2f, 0.145f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.012f), shadow);
            Part("Coat Back", PrimitiveType.Cube, root, new Vector3(0f, 0.95f, -0.17f), Vector3.zero, new Vector3(0.52f, 1.0f, 0.06f), offWhite);
            Part("Hood", PrimitiveType.Cube, root, new Vector3(0f, 1.5f, -0.13f), Vector3.zero, new Vector3(0.32f, 0.14f, 0.2f), grey);
            Part("Head", PrimitiveType.Sphere, root, new Vector3(0f, 1.65f, 0f), Vector3.zero, Vector3.one * 0.3f, grey);
            Part("Hair", PrimitiveType.Cube, root, new Vector3(0f, 1.76f, -0.03f), Vector3.zero, new Vector3(0.3f, 0.1f, 0.3f), shadow);

            // ORIGIN mark: left brassard, outward face.
            OriginMark("Origin Mark", root, new Vector3(-0.41f, 1.2f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.14f, offWhite, shadow, amber);

            // Trace Module: a small housing high on the right chest with an amber lens that answers Tactical Focus.
            var housing = Part("Trace Module", PrimitiveType.Cube, root, new Vector3(0.12f, 1.33f, 0.16f), Vector3.zero, new Vector3(0.14f, 0.1f, 0.06f), charcoal);
            var lens = Part("Module Lens", PrimitiveType.Cube, housing.transform, new Vector3(0f, 0f, 0.55f), Vector3.zero, new Vector3(0.7f, 0.35f, 0.2f), module);

            // Short technical blade in the right hand, pointing forward and slightly down for quick strikes and the dash.
            var weapon = new GameObject("Blade");
            weapon.layer = 2;
            weapon.transform.SetParent(root, false);
            weapon.transform.localPosition = new Vector3(0.38f, 0.74f, 0.12f);
            weapon.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            Part("Grip", PrimitiveType.Cube, weapon.transform, Vector3.zero, Vector3.zero, new Vector3(0.045f, 0.065f, 0.17f), shadow);
            Part("Guard", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.11f), Vector3.zero, new Vector3(0.085f, 0.09f, 0.025f), amber);
            Part("Edge", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.5f), Vector3.zero, new Vector3(0.028f, 0.055f, 0.72f), blade);
            Part("Edge Line", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.5f), Vector3.zero, new Vector3(0.032f, 0.012f, 0.5f), amber);

            // Existing feedback keeps working on the torso: hit flash, dodge tint, death pose.
            var feedback = member.GetComponent<CompanionFeedback>();
            if (feedback != null)
            {
                Reference(feedback, "visual", root);
                Reference(feedback, "bodyRenderer", torso.GetComponent<Renderer>());
            }
            var profile = member.GetComponent<CharacterProfile>();
            if (profile == null) profile = member.AddComponent<CharacterProfile>();
            var data = new SerializedObject(profile);
            data.FindProperty("characterId").stringValue = "tracewalker_player";
            data.FindProperty("displayName").stringValue = "Tracewalker";
            data.FindProperty("designation").stringValue = "Tracewalker";
            data.FindProperty("archetype").stringValue = "Assault / Mobile Vanguard";
            data.FindProperty("accentColor").colorValue = Amber;
            data.ApplyModifiedPropertiesWithoutUndo();
            var trace = member.GetComponent<TraceModule>();
            if (trace == null) trace = member.AddComponent<TraceModule>();
            Reference(trace, "emitter", lens.GetComponent<Renderer>());
            Reference(trace, "focus", UnityEngine.Object.FindFirstObjectByType<TacticalFocus>());
        }

        private static void OriginMark(string name, Transform parent, Vector3 position, Quaternion rotation, float size,
            Material plate, Material bars, Material stroke)
        {
            // Local +z is the outward normal. Two leaning charcoal bars, one amber stroke rising across them.
            var mark = new GameObject(name);
            mark.layer = 2;
            mark.transform.SetParent(parent, false);
            mark.transform.localPosition = position;
            mark.transform.localRotation = rotation;
            const float t = 0.006f;
            Part("Plate", PrimitiveType.Cube, mark.transform, new Vector3(0f, 0f, -t), Vector3.zero, new Vector3(1.15f * size, 1.15f * size, t), plate);
            Part("Bar A", PrimitiveType.Cube, mark.transform, new Vector3(-0.2f * size, 0f, 0f), new Vector3(0f, 0f, -16f), new Vector3(0.16f * size, 0.9f * size, t), bars);
            Part("Bar B", PrimitiveType.Cube, mark.transform, new Vector3(0.12f * size, 0f, 0f), new Vector3(0f, 0f, -16f), new Vector3(0.16f * size, 0.9f * size, t), bars);
            Part("Stroke", PrimitiveType.Cube, mark.transform, new Vector3(0.1f * size, 0.02f * size, t), new Vector3(0f, 0f, 42f), new Vector3(1.15f * size, 0.1f * size, t), stroke);
        }

        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
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
