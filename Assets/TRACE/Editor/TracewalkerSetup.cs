using System;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Tactical;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static TRACE.Editor.SquadVisualKit;

namespace TRACE.Editor
{
    // Dresses member 1 (the former Assault placeholder) as the Tracewalker in every playable scene: composed
    // primitive body in the concept palette, ORIGIN mark on the left brassard, Trace Module on the chest,
    // short technical blade, identity profile, procedural puppet. Gameplay components and tuning are untouched.
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
            EnsureFolder(MaterialFolder);
            int applied = 0;
            foreach (string path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var member = FindMember(scene, "Member 1 - Assault", MemberName);
                if (member == null) throw new InvalidOperationException(path + " has no squad member 1.");
                Dress(member);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Tracewalker V0.1 applied to {applied} scene(s).");
        }

        private static void Dress(GameObject member)
        {
            member.name = MemberName;
            ClearChildren(member, "Tracewalker Visual", "Capsule Visual", "Facing Marker");
            var charcoal = Mat(MaterialFolder, "M_Tracewalker_Charcoal", new Color(0.13f, 0.14f, 0.15f), 0.25f);
            var shadow = Mat(MaterialFolder, "M_Tracewalker_Shadow", new Color(0.05f, 0.05f, 0.06f), 0.3f);
            var grey = Mat(MaterialFolder, "M_Tracewalker_CoolGrey", new Color(0.55f, 0.58f, 0.62f), 0.2f);
            var offWhite = Mat(MaterialFolder, "M_Tracewalker_OffWhite", new Color(0.86f, 0.86f, 0.83f), 0.2f);
            var amber = Mat(MaterialFolder, "M_Tracewalker_Amber", Amber, 0.3f);
            var module = Mat(MaterialFolder, "M_Tracewalker_Module", new Color(0.12f, 0.11f, 0.08f), 0.4f, emission: Amber * 0.8f);
            var blade = Mat(MaterialFolder, "M_Tracewalker_Blade", new Color(0.3f, 0.32f, 0.35f), 0.55f, metallic: 0.6f);

            var visual = new GameObject("Tracewalker Visual");
            visual.layer = 2;
            visual.transform.SetParent(member.transform, false);
            var root = visual.transform;
            // Joint pivots so the puppet can swing limbs and lean the torso: hips under the root, shoulders and head
            // under an upper-body pivot at hip height. Rest pose matches the unchanged 1.8 m capsule.
            var upper = Pivot("Upper Body", root, new Vector3(0f, 0.92f, 0f));
            var headPivot = Pivot("Head", upper, new Vector3(0f, 0.73f, 0f));
            Transform leftHip = null, rightHip = null, leftShoulder = null, rightShoulder = null;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Pivot(side < 0 ? "Hip L" : "Hip R", root, new Vector3(0.13f * side, 0.92f, 0f));
                Part("Leg", PrimitiveType.Cube, hip, new Vector3(0f, -0.42f, 0f), Vector3.zero, new Vector3(0.2f, 0.84f, 0.24f), charcoal);
                Part("Boot", PrimitiveType.Cube, hip, new Vector3(0f, -0.84f, 0.03f), Vector3.zero, new Vector3(0.22f, 0.16f, 0.32f), shadow);
                Part("Boot Trim", PrimitiveType.Cube, hip, new Vector3(0f, -0.9f, 0.03f), Vector3.zero, new Vector3(0.23f, 0.03f, 0.33f), amber);
                var shoulder = Pivot(side < 0 ? "Shoulder L" : "Shoulder R", upper, new Vector3(0.33f * side, 0.44f, 0f));
                Part("Arm", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.28f, 0f), Vector3.zero, new Vector3(0.14f, 0.52f, 0.16f), charcoal);
                Part("Glove", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.6f, 0.02f), Vector3.zero, new Vector3(0.15f, 0.14f, 0.17f), shadow);
                Part("Shoulder", PrimitiveType.Cube, upper, new Vector3(0.3f * side, 0.5f, 0f), Vector3.zero, new Vector3(0.18f, 0.12f, 0.26f), offWhite);
                Part("Coat Side", PrimitiveType.Cube, upper, new Vector3(0.27f * side, 0f, -0.03f), Vector3.zero, new Vector3(0.06f, 0.95f, 0.3f), offWhite);
                if (side < 0) { leftHip = hip; leftShoulder = shoulder; } else { rightHip = hip; rightShoulder = shoulder; }
            }
            var torso = Part("Torso", PrimitiveType.Cube, upper, new Vector3(0f, 0.23f, 0f), Vector3.zero, new Vector3(0.46f, 0.56f, 0.28f), charcoal);
            Part("Harness", PrimitiveType.Cube, upper, new Vector3(0f, 0.28f, 0.145f), Vector3.zero, new Vector3(0.05f, 0.44f, 0.012f), shadow);
            Part("Coat Back", PrimitiveType.Cube, upper, new Vector3(0f, 0.03f, -0.17f), Vector3.zero, new Vector3(0.52f, 1.0f, 0.06f), offWhite);
            Part("Hood", PrimitiveType.Cube, upper, new Vector3(0f, 0.58f, -0.13f), Vector3.zero, new Vector3(0.32f, 0.14f, 0.2f), grey);
            Part("Skull", PrimitiveType.Sphere, headPivot, Vector3.zero, Vector3.zero, Vector3.one * 0.3f, grey);
            Part("Hair", PrimitiveType.Cube, headPivot, new Vector3(0f, 0.11f, -0.03f), Vector3.zero, new Vector3(0.3f, 0.1f, 0.3f), shadow);

            // ORIGIN mark on the left brassard, outward face; it rides the arm.
            OriginMark("Origin Mark", leftShoulder, new Vector3(-0.08f, -0.16f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.14f, offWhite, shadow, amber);

            // Trace Module: a small housing high on the right chest with an amber lens that answers Tactical Focus.
            var housing = Part("Trace Module", PrimitiveType.Cube, upper, new Vector3(0.12f, 0.41f, 0.16f), Vector3.zero, new Vector3(0.14f, 0.1f, 0.06f), charcoal);
            var lens = Part("Module Lens", PrimitiveType.Cube, housing.transform, new Vector3(0f, 0f, 0.55f), Vector3.zero, new Vector3(0.7f, 0.35f, 0.2f), module);

            // Short technical blade in the right hand, swinging with the arm: forward and slightly down at rest.
            var weapon = new GameObject("Blade");
            weapon.layer = 2;
            weapon.transform.SetParent(rightShoulder, false);
            weapon.transform.localPosition = new Vector3(0.05f, -0.62f, 0.12f);
            weapon.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            Part("Grip", PrimitiveType.Cube, weapon.transform, Vector3.zero, Vector3.zero, new Vector3(0.045f, 0.065f, 0.17f), shadow);
            Part("Guard", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.11f), Vector3.zero, new Vector3(0.085f, 0.09f, 0.025f), amber);
            Part("Edge", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.5f), Vector3.zero, new Vector3(0.028f, 0.055f, 0.72f), blade);
            Part("Edge Line", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.5f), Vector3.zero, new Vector3(0.032f, 0.012f, 0.5f), amber);

            // Procedural animation: mobile, nervous identity (defaults of CharacterPuppet).
            var puppet = Puppet(member, root, upper, headPivot, leftHip, rightHip, leftShoulder, rightShoulder);
            Flag(puppet, "focusGesture", true);

            // Existing feedback keeps working on the torso: hit flash, dodge tint, death pose.
            var feedback = member.GetComponent<CompanionFeedback>();
            if (feedback != null)
            {
                Reference(feedback, "visual", root);
                Reference(feedback, "bodyRenderer", torso.GetComponent<Renderer>());
            }
            Profile(member, "tracewalker_player", "Tracewalker", "Tracewalker", "", "Assault / Mobile Vanguard", true, Amber);
            var trace = member.GetComponent<TraceModule>();
            if (trace == null) trace = member.AddComponent<TraceModule>();
            Reference(trace, "emitter", lens.GetComponent<Renderer>());
            Reference(trace, "focus", UnityEngine.Object.FindFirstObjectByType<TacticalFocus>());
        }
    }
}
