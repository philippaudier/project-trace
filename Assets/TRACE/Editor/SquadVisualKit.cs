using System.Linq;
using TRACE.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TRACE.Editor
{
    // Shared helpers for the primitive squad bodies: parts, pivots, the ORIGIN mark, named materials, profiles.
    internal static class SquadVisualKit
    {
        public static GameObject FindMember(Scene scene, params string[] names)
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SquadMember>(true))
                .Select(m => m.gameObject).FirstOrDefault(g => names.Contains(g.name));
        }

        public static void ClearChildren(GameObject member, params string[] names)
        {
            foreach (string name in names)
            {
                var stale = member.transform.Find(name);
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }
        }

        public static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        public static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ORIGIN institutional mark. Local +z is the outward normal: two leaning bars and one stroke rising across them.
        public static GameObject OriginMark(string name, Transform parent, Vector3 position, Quaternion rotation, float size,
            Material plate, Material bars, Material stroke)
        {
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
            return mark;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }

        // Load-or-create: an existing asset keeps its values, so delete it to re-tune.
        public static Material Mat(string folder, string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            string path = folder + "/" + name + ".mat";
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

        public static Material Unlit(string folder, string name, Color color)
        {
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void Reference(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Number(Object target, string field, float value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Flag(Object target, string field, bool value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static CharacterProfile Profile(GameObject member, string id, string displayName, string designation,
            string affiliation, string archetype, bool isTracewalker, Color accent)
        {
            var profile = member.GetComponent<CharacterProfile>();
            if (profile == null) profile = member.AddComponent<CharacterProfile>();
            var data = new SerializedObject(profile);
            data.FindProperty("characterId").stringValue = id;
            data.FindProperty("displayName").stringValue = displayName;
            data.FindProperty("designation").stringValue = designation;
            data.FindProperty("affiliation").stringValue = affiliation;
            data.FindProperty("archetype").stringValue = archetype;
            data.FindProperty("isTracewalker").boolValue = isTracewalker;
            data.FindProperty("accentColor").colorValue = accent;
            data.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        public static CharacterPuppet Puppet(GameObject member, Transform body, Transform upper, Transform head,
            Transform leftHip, Transform rightHip, Transform leftShoulder, Transform rightShoulder)
        {
            var puppet = member.GetComponent<CharacterPuppet>();
            if (puppet == null) puppet = member.AddComponent<CharacterPuppet>();
            Reference(puppet, "body", body);
            Reference(puppet, "upperBody", upper);
            Reference(puppet, "head", head);
            Reference(puppet, "leftHip", leftHip);
            Reference(puppet, "rightHip", rightHip);
            Reference(puppet, "leftShoulder", leftShoulder);
            Reference(puppet, "rightShoulder", rightShoulder);
            Reference(puppet, "motor", member.GetComponent<ThirdPersonMotor>());
            Reference(puppet, "melee", member.GetComponent<TRACE.Combat.PlayerMeleeAttack>());
            Reference(puppet, "companion", member.GetComponent<TRACE.AI.CompanionController>());
            Reference(puppet, "health", member.GetComponent<TRACE.Combat.Health>());
            Reference(puppet, "focus", Object.FindFirstObjectByType<TRACE.Tactical.TacticalFocus>());
            return puppet;
        }
    }
}
