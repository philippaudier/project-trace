using System;
using System.Linq;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static TRACE.Editor.SquadVisualKit;

namespace TRACE.Editor
{
    // Dresses member 3 as SUPPORT, ORIGIN Field Specialist: softer, stable primitive body with an enveloping
    // off-white coat and hood, long light hair with mint ribbons, mint protective modules, a circular Support
    // Module on the back, a stabilization staff with a wide emitter ring, a discreet ORIGIN mark, profile with the
    // concept portrait and a calm puppet. Pulse Shield halos turn mint on every member. Gameplay untouched.
    public static class SupportSetup
    {
        public const string MaterialFolder = "Assets/TRACE/Scenes/Materials/Support";
        public const string MemberName = "Member 3 - Support";
        public const string PortraitPath = "Assets/TRACE/UI/Portraits/Portrait_Support.png";
        // Soft mint from the concept palette: protection and stabilization.
        public static readonly Color Mint = new Color(0.45f, 0.9f, 0.78f);
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
        };

        [MenuItem("TRACE/Apply Support V0.1")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(MaterialFolder);
            int applied = 0;
            foreach (string path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var member = FindMember(scene, MemberName);
                if (member == null) throw new InvalidOperationException(path + " has no squad member 3.");
                var squad = UnityEngine.Object.FindFirstObjectByType<TRACE.AI.SquadController>();
                Dress(member, squad);
                DressShields(squad);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Support V0.1 applied to {applied} scene(s).");
        }

        private static void Dress(GameObject member, TRACE.AI.SquadController squad)
        {
            ClearChildren(member, "Support Visual", "Body");
            var charcoal = Mat(MaterialFolder, "M_Support_Charcoal", new Color(0.12f, 0.13f, 0.14f), 0.25f);
            var shadow = Mat(MaterialFolder, "M_Support_Shadow", new Color(0.04f, 0.04f, 0.05f), 0.35f);
            var grey = Mat(MaterialFolder, "M_Support_CoolGrey", new Color(0.52f, 0.55f, 0.58f), 0.2f);
            var offWhite = Mat(MaterialFolder, "M_Support_OffWhite", new Color(0.88f, 0.88f, 0.86f), 0.2f);
            var hair = Mat(MaterialFolder, "M_Support_Hair", new Color(0.52f, 0.45f, 0.37f), 0.3f);
            var mint = Mat(MaterialFolder, "M_Support_Mint", new Color(0.5f, 0.86f, 0.76f), 0.35f);
            var amber = Mat(MaterialFolder, "M_Support_OriginAmber", TracewalkerSetup.Amber, 0.3f);
            var module = Mat(MaterialFolder, "M_Support_Module", new Color(0.06f, 0.13f, 0.12f), 0.45f, emission: Mint * 0.5f);
            var staff = Mat(MaterialFolder, "M_Support_Staff", new Color(0.2f, 0.22f, 0.24f), 0.5f, metallic: 0.5f);

            var visual = new GameObject("Support Visual");
            visual.layer = 2;
            visual.transform.SetParent(member.transform, false);
            var root = visual.transform;
            // Stable stance between Control and the Tracewalker, enveloping coat on both sides, hood, soft shoulders.
            var upper = Pivot("Upper Body", root, new Vector3(0f, 0.93f, 0f));
            var headPivot = Pivot("Head", upper, new Vector3(0f, 0.73f, 0f));
            Transform leftHip = null, rightHip = null, leftShoulder = null, rightShoulder = null;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Pivot(side < 0 ? "Hip L" : "Hip R", root, new Vector3(0.12f * side, 0.93f, 0f));
                Part("Leg", PrimitiveType.Cube, hip, new Vector3(0f, -0.42f, 0f), Vector3.zero, new Vector3(0.17f, 0.86f, 0.21f), charcoal);
                Part("Knee Guard", PrimitiveType.Cube, hip, new Vector3(0f, -0.5f, 0.1f), Vector3.zero, new Vector3(0.16f, 0.16f, 0.03f), shadow);
                Part("Boot", PrimitiveType.Cube, hip, new Vector3(0f, -0.86f, 0.02f), Vector3.zero, new Vector3(0.18f, 0.15f, 0.29f), shadow);
                Part("Boot Line", PrimitiveType.Cube, hip, new Vector3(0f, -0.79f, 0.02f), Vector3.zero, new Vector3(0.185f, 0.015f, 0.295f), mint);
                var shoulder = Pivot(side < 0 ? "Shoulder L" : "Shoulder R", upper, new Vector3(0.29f * side, 0.45f, 0f));
                Part("Arm", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.27f, 0f), Vector3.zero, new Vector3(0.12f, 0.52f, 0.14f), charcoal);
                Part("Sleeve", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.12f, 0f), Vector3.zero, new Vector3(0.15f, 0.26f, 0.17f), offWhite);
                Part("Glove", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.59f, 0.01f), Vector3.zero, new Vector3(0.13f, 0.13f, 0.15f), shadow);
                Part("Cuff", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.5f, 0.01f), Vector3.zero, new Vector3(0.135f, 0.02f, 0.155f), mint);
                Part("Shoulder Pad", PrimitiveType.Cube, upper, new Vector3(0.28f * side, 0.52f, 0f), Vector3.zero, new Vector3(0.16f, 0.08f, 0.22f), offWhite);
                Part("Coat Side", PrimitiveType.Cube, upper, new Vector3(0.24f * side, -0.2f, -0.02f), Vector3.zero, new Vector3(0.06f, 1.2f, 0.3f), offWhite);
                Part("Coat Flap", PrimitiveType.Cube, upper, new Vector3(0.11f * side, -0.22f, 0.125f), Vector3.zero, new Vector3(0.14f, 1.1f, 0.018f), offWhite);
                Part("Flap Edge", PrimitiveType.Cube, upper, new Vector3(0.045f * side, -0.22f, 0.13f), Vector3.zero, new Vector3(0.012f, 1.1f, 0.02f), mint);
                if (side < 0) { leftHip = hip; leftShoulder = shoulder; } else { rightHip = hip; rightShoulder = shoulder; }
            }
            var torso = Part("Torso", PrimitiveType.Cube, upper, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.38f, 0.54f, 0.24f), charcoal);
            Part("Chest Plate", PrimitiveType.Cube, upper, new Vector3(0f, 0.32f, 0.125f), Vector3.zero, new Vector3(0.24f, 0.26f, 0.015f), offWhite);
            Part("Chest Module", PrimitiveType.Cube, upper, new Vector3(0f, 0.3f, 0.135f), Vector3.zero, new Vector3(0.09f, 0.09f, 0.02f), charcoal);
            Part("Chest Lens", PrimitiveType.Cube, upper, new Vector3(0f, 0.3f, 0.147f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.006f), module);
            Part("Collar", PrimitiveType.Cube, upper, new Vector3(0f, 0.53f, 0f), Vector3.zero, new Vector3(0.32f, 0.04f, 0.26f), mint);
            Part("Coat Back", PrimitiveType.Cube, upper, new Vector3(0f, -0.12f, -0.14f), Vector3.zero, new Vector3(0.46f, 1.1f, 0.05f), offWhite);
            Part("Hood", PrimitiveType.Cube, upper, new Vector3(0f, 0.6f, -0.15f), Vector3.zero, new Vector3(0.36f, 0.16f, 0.22f), offWhite);
            Part("Skull", PrimitiveType.Sphere, headPivot, Vector3.zero, Vector3.zero, Vector3.one * 0.27f, grey);
            Part("Hair Top", PrimitiveType.Cube, headPivot, new Vector3(0f, 0.1f, -0.03f), Vector3.zero, new Vector3(0.29f, 0.09f, 0.29f), hair);
            Part("Hair Long", PrimitiveType.Cube, headPivot, new Vector3(0f, -0.21f, -0.12f), Vector3.zero, new Vector3(0.28f, 0.62f, 0.11f), hair);
            Part("Ribbon L", PrimitiveType.Cube, headPivot, new Vector3(-0.1f, -0.2f, -0.07f), Vector3.zero, new Vector3(0.012f, 0.45f, 0.02f), mint);
            Part("Ribbon R", PrimitiveType.Cube, headPivot, new Vector3(0.11f, -0.15f, -0.08f), Vector3.zero, new Vector3(0.012f, 0.4f, 0.02f), mint);

            // ORIGIN institutional mark on the left shoulder plate, small and outward.
            OriginMark("Origin Mark", upper, new Vector3(-0.365f, 0.52f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.1f, offWhite, shadow, amber);

            // Support Module: a circular field generator on the back, mint core, segmented ring, thin ORIGIN band.
            var moduleRoot = Pivot("Support Module", upper, new Vector3(0f, 0.2f, -0.22f));
            Part("Housing", PrimitiveType.Cube, moduleRoot, Vector3.zero, Vector3.zero, new Vector3(0.12f, 0.12f, 0.07f), charcoal);
            var core = Part("Module Core", PrimitiveType.Sphere, moduleRoot, new Vector3(0f, 0f, -0.04f), Vector3.zero, Vector3.one * 0.08f, module);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                Vector3 offset = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.15f, 0f);
                Part("Module Segment", PrimitiveType.Cube, moduleRoot, offset + new Vector3(0f, 0f, -0.02f), new Vector3(0f, 0f, angle), new Vector3(0.08f, 0.024f, 0.024f), i % 4 == 0 ? charcoal : module);
            }
            Part("Module Band", PrimitiveType.Cube, moduleRoot, new Vector3(0f, -0.2f, -0.02f), Vector3.zero, new Vector3(0.14f, 0.012f, 0.014f), amber);
            Part("Hip Pouch", PrimitiveType.Cube, upper, new Vector3(-0.2f, -0.04f, 0.05f), Vector3.zero, new Vector3(0.07f, 0.11f, 0.1f), charcoal);
            Part("Pouch Lens", PrimitiveType.Cube, upper, new Vector3(-0.24f, -0.04f, 0.05f), Vector3.zero, new Vector3(0.01f, 0.05f, 0.05f), module);

            // Stabilization staff held close to the body in the right hand: vertical shaft, wide double emitter ring
            // with a mint core at the top, ORIGIN grip band. It swings with the arm.
            var weapon = new GameObject("Stabilization Staff");
            weapon.layer = 2;
            weapon.transform.SetParent(rightShoulder, false);
            weapon.transform.localPosition = new Vector3(0.07f, -0.59f, 0.05f);
            Part("Shaft", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.035f, 1.8f, 0.035f), staff);
            Part("Segment Low", PrimitiveType.Cube, weapon.transform, new Vector3(0f, -0.4f, 0f), Vector3.zero, new Vector3(0.055f, 0.14f, 0.055f), charcoal);
            Part("Grip Band", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.04f, 0.02f, 0.04f), amber);
            Part("Segment High", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(0.06f, 0.16f, 0.06f), charcoal);
            var ring = Pivot("Emitter Ring", weapon.transform, new Vector3(0f, 1.27f, 0f));
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                Vector3 outer = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.18f, 0f);
                Part("Ring Segment", PrimitiveType.Cube, ring, outer, new Vector3(0f, 0f, angle), new Vector3(0.1f, 0.024f, 0.024f), i % 3 == 0 ? charcoal : module);
                Vector3 inner = Quaternion.Euler(0f, 0f, angle + 15f) * new Vector3(0f, 0.1f, 0f);
                Part("Inner Segment", PrimitiveType.Cube, ring, inner, new Vector3(0f, 0f, angle + 15f), new Vector3(0.05f, 0.016f, 0.016f), staff);
            }
            Part("Ring Core", PrimitiveType.Sphere, ring, Vector3.zero, Vector3.zero, Vector3.one * 0.06f, module);
            Part("Ring Stem", PrimitiveType.Cube, ring, Vector3.zero, Vector3.zero, new Vector3(0.012f, 0.36f, 0.012f), staff);

            // Procedural animation: the calmest of the three. Small swings, slow responses, measured, protective.
            var puppet = Puppet(member, root, upper, headPivot, leftHip, rightHip, leftShoulder, rightShoulder);
            Number(puppet, "legSwing", 20f);
            Number(puppet, "armSwing", 8f);
            Number(puppet, "bobHeight", 0.015f);
            Number(puppet, "sprintLean", 4f);
            Number(puppet, "breathing", 0.6f);
            Number(puppet, "attackWindupPitch", 20f);
            Number(puppet, "attackSwingPitch", 45f);
            Number(puppet, "dodgeCrouch", 0.18f);
            Number(puppet, "dodgeLean", 7f);
            Number(puppet, "dashLean", 10f);
            Number(puppet, "hitFlinch", 7f);
            Number(puppet, "poseResponse", 8f);
            Number(puppet, "strikeResponse", 14f);
            Flag(puppet, "focusGesture", false);

            var feedback = member.GetComponent<CompanionFeedback>();
            if (feedback != null)
            {
                Reference(feedback, "visual", root);
                Reference(feedback, "bodyRenderer", torso.GetComponent<Renderer>());
            }
            var profile = Profile(member, "origin_support_01", "Support", "Field Specialist — Support", "ORIGIN", "Defensive Support", false, Mint);
            Reference(profile, "portrait", AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath));
            var supportModule = member.GetComponent<SupportModule>();
            if (supportModule == null) supportModule = member.AddComponent<SupportModule>();
            Reference(supportModule, "emitter", core.GetComponent<Renderer>());
            var shields = squad != null ? squad.Members.Select(m => m.GetComponent<Shield>()).Where(s => s != null).ToArray() : new Shield[0];
            var data = new SerializedObject(supportModule);
            var array = data.FindProperty("shields");
            array.arraySize = shields.Length;
            for (int i = 0; i < shields.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = shields[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // Pulse Shield as ORIGIN protection technology: mint halos on every member with a slow, calm rotation.
        private static void DressShields(TRACE.AI.SquadController squad)
        {
            if (squad == null) return;
            var shieldMaterial = Unlit(MaterialFolder, "M_Support_Shield", new Color(0.5f, 0.95f, 0.82f));
            foreach (var member in squad.Members)
            {
                var shield = member.GetComponent<Shield>();
                if (shield == null) continue;
                var halo = new SerializedObject(shield).FindProperty("halo").objectReferenceValue as GameObject;
                if (halo == null) continue;
                var rings = halo.GetComponentsInChildren<LineRenderer>(true).OrderBy(r => r.name).ToArray();
                foreach (var ring in rings) ring.sharedMaterial = shieldMaterial;
                var pulse = halo.GetComponent<FieldVisualPulse>();
                if (pulse == null) pulse = halo.AddComponent<FieldVisualPulse>();
                var data = new SerializedObject(pulse);
                var ringArray = data.FindProperty("rings");
                var spinArray = data.FindProperty("spinDegreesPerSecond");
                ringArray.arraySize = spinArray.arraySize = rings.Length;
                for (int i = 0; i < rings.Length; i++)
                {
                    ringArray.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
                    spinArray.GetArrayElementAtIndex(i).floatValue = rings[i].name.StartsWith("Lower") ? 12f : -12f;
                }
                data.FindProperty("pulseAmount").floatValue = 0.1f;
                data.FindProperty("pulseFrequency").floatValue = 0.8f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
