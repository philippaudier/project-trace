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
    // Dresses member 2 as CONTROL, ORIGIN Field Specialist: slim vertical primitive body, asymmetric layered
    // coat, long dark hair with a pale cyan strand, cyan field modules, Field Control Module on the back,
    // Gravity Staff with a ring head, ORIGIN mark on the right shoulder plate, profile and a calmer puppet.
    // Gravity Field rings turn cyan with a measured pulse. Gameplay components and tuning are untouched.
    public static class ControlSetup
    {
        public const string MaterialFolder = "Assets/TRACE/Scenes/Materials/Control";
        public const string MemberName = "Member 2 - Control";
        // Pale cold cyan from the concept palette: her personal colour, more visible than the ORIGIN amber.
        public static readonly Color Cyan = new Color(0.5f, 0.85f, 1f);
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
            "Assets/TRACE/Scenes/FieldTest.unity",
        };

        [MenuItem("TRACE/Apply Control V0.1")]
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
                if (member == null) throw new InvalidOperationException(path + " has no squad member 2.");
                Dress(member);
                DressField(member);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Control V0.1 applied to {applied} scene(s).");
        }

        private static void Dress(GameObject member)
        {
            ClearChildren(member, "Control Visual", "Body");
            var charcoal = Mat(MaterialFolder, "M_Control_Charcoal", new Color(0.12f, 0.13f, 0.15f), 0.25f);
            var shadow = Mat(MaterialFolder, "M_Control_Shadow", new Color(0.04f, 0.04f, 0.05f), 0.35f);
            var grey = Mat(MaterialFolder, "M_Control_CoolGrey", new Color(0.5f, 0.54f, 0.6f), 0.2f);
            var offWhite = Mat(MaterialFolder, "M_Control_OffWhite", new Color(0.84f, 0.85f, 0.84f), 0.2f);
            var cyan = Mat(MaterialFolder, "M_Control_Cyan", new Color(0.55f, 0.82f, 0.92f), 0.35f);
            var amber = Mat(MaterialFolder, "M_Control_OriginAmber", TracewalkerSetup.Amber, 0.3f);
            var fieldModule = Mat(MaterialFolder, "M_Control_FieldModule", new Color(0.08f, 0.12f, 0.15f), 0.45f, emission: Cyan * 0.5f);
            var staff = Mat(MaterialFolder, "M_Control_Staff", new Color(0.22f, 0.24f, 0.27f), 0.5f, metallic: 0.5f);

            var visual = new GameObject("Control Visual");
            visual.layer = 2;
            visual.transform.SetParent(member.transform, false);
            var root = visual.transform;
            // Slimmer and more vertical than the Tracewalker: narrower stance, longer coat line, no bulky coat panels.
            var upper = Pivot("Upper Body", root, new Vector3(0f, 0.94f, 0f));
            var headPivot = Pivot("Head", upper, new Vector3(0f, 0.74f, 0f));
            Transform leftHip = null, rightHip = null, leftShoulder = null, rightShoulder = null;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Pivot(side < 0 ? "Hip L" : "Hip R", root, new Vector3(0.1f * side, 0.94f, 0f));
                Part("Leg", PrimitiveType.Cube, hip, new Vector3(0f, -0.43f, 0f), Vector3.zero, new Vector3(0.16f, 0.86f, 0.2f), charcoal);
                Part("Boot", PrimitiveType.Cube, hip, new Vector3(0f, -0.87f, 0.02f), Vector3.zero, new Vector3(0.17f, 0.14f, 0.28f), shadow);
                Part("Boot Line", PrimitiveType.Cube, hip, new Vector3(0f, -0.81f, 0.02f), Vector3.zero, new Vector3(0.175f, 0.015f, 0.285f), cyan);
                var shoulder = Pivot(side < 0 ? "Shoulder L" : "Shoulder R", upper, new Vector3(0.27f * side, 0.46f, 0f));
                Part("Arm", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.27f, 0f), Vector3.zero, new Vector3(0.11f, 0.52f, 0.13f), charcoal);
                Part("Glove", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.59f, 0.01f), Vector3.zero, new Vector3(0.12f, 0.13f, 0.14f), shadow);
                Part("Cuff", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.5f, 0.01f), Vector3.zero, new Vector3(0.125f, 0.02f, 0.145f), cyan);
                Part("Shoulder Pad", PrimitiveType.Cube, upper, new Vector3(0.26f * side, 0.52f, 0f), Vector3.zero, new Vector3(0.14f, 0.07f, 0.2f), offWhite);
                if (side < 0) { leftHip = hip; leftShoulder = shoulder; } else { rightHip = hip; rightShoulder = shoulder; }
            }
            var torso = Part("Torso", PrimitiveType.Cube, upper, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.36f, 0.54f, 0.22f), charcoal);
            Part("Chest Plate", PrimitiveType.Cube, upper, new Vector3(-0.06f, 0.3f, 0.115f), Vector3.zero, new Vector3(0.16f, 0.26f, 0.015f), offWhite);
            Part("Chest Module", PrimitiveType.Cube, upper, new Vector3(0.1f, 0.33f, 0.115f), Vector3.zero, new Vector3(0.08f, 0.1f, 0.02f), charcoal);
            Part("Chest Lens", PrimitiveType.Cube, upper, new Vector3(0.1f, 0.33f, 0.127f), Vector3.zero, new Vector3(0.05f, 0.03f, 0.006f), fieldModule);
            Part("Collar", PrimitiveType.Cube, upper, new Vector3(0f, 0.53f, 0f), Vector3.zero, new Vector3(0.3f, 0.035f, 0.24f), cyan);
            // Asymmetric layered coat: long off-white panel on the left down to the shin with a cyan edge, short
            // charcoal panel on the right, back panel with a cyan line.
            Part("Coat Long", PrimitiveType.Cube, upper, new Vector3(-0.21f, -0.28f, -0.02f), Vector3.zero, new Vector3(0.05f, 1.32f, 0.28f), offWhite);
            Part("Coat Edge", PrimitiveType.Cube, upper, new Vector3(-0.21f, -0.28f, 0.125f), Vector3.zero, new Vector3(0.052f, 1.32f, 0.012f), cyan);
            Part("Coat Short", PrimitiveType.Cube, upper, new Vector3(0.21f, 0.05f, -0.03f), Vector3.zero, new Vector3(0.05f, 0.66f, 0.26f), charcoal);
            // Front lapel of the long panel so the off-white reads from the front too, as on the concept.
            Part("Coat Lapel", PrimitiveType.Cube, upper, new Vector3(-0.12f, -0.22f, 0.115f), Vector3.zero, new Vector3(0.14f, 1.18f, 0.018f), offWhite);
            Part("Lapel Edge", PrimitiveType.Cube, upper, new Vector3(-0.045f, -0.22f, 0.12f), Vector3.zero, new Vector3(0.012f, 1.18f, 0.02f), cyan);
            Part("Coat Back", PrimitiveType.Cube, upper, new Vector3(0f, -0.15f, -0.14f), Vector3.zero, new Vector3(0.42f, 1.08f, 0.05f), offWhite);
            Part("Back Line", PrimitiveType.Cube, upper, new Vector3(-0.19f, -0.15f, -0.17f), Vector3.zero, new Vector3(0.02f, 1.08f, 0.012f), cyan);
            Part("Skull", PrimitiveType.Sphere, headPivot, Vector3.zero, Vector3.zero, Vector3.one * 0.27f, grey);
            Part("Hair Top", PrimitiveType.Cube, headPivot, new Vector3(0f, 0.1f, -0.03f), Vector3.zero, new Vector3(0.27f, 0.08f, 0.27f), shadow);
            Part("Hair Long", PrimitiveType.Cube, headPivot, new Vector3(0f, -0.2f, -0.13f), Vector3.zero, new Vector3(0.26f, 0.6f, 0.1f), shadow);
            Part("Hair Strand", PrimitiveType.Cube, headPivot, new Vector3(-0.12f, -0.18f, -0.08f), Vector3.zero, new Vector3(0.015f, 0.5f, 0.02f), cyan);

            // ORIGIN institutional mark on the right shoulder plate, outward face.
            OriginMark("Origin Mark", upper, new Vector3(0.335f, 0.52f, 0f), Quaternion.Euler(0f, 90f, 0f), 0.11f, offWhite, shadow, amber);

            // Field Control Module: a light back unit with a cyan core and a thin ORIGIN amber band.
            var module = Part("Field Control Module", PrimitiveType.Cube, upper, new Vector3(0.02f, 0.22f, -0.2f), Vector3.zero, new Vector3(0.24f, 0.32f, 0.1f), charcoal);
            var core = Part("Module Core", PrimitiveType.Cube, module.transform, new Vector3(0f, 0.05f, -0.55f), Vector3.zero, new Vector3(0.45f, 0.55f, 0.2f), fieldModule);
            Part("Module Band", PrimitiveType.Cube, module.transform, new Vector3(0f, -0.4f, -0.52f), Vector3.zero, new Vector3(0.7f, 0.05f, 0.12f), amber);
            Part("Hip Module", PrimitiveType.Cube, upper, new Vector3(0.2f, -0.02f, 0.06f), Vector3.zero, new Vector3(0.07f, 0.12f, 0.1f), charcoal);
            Part("Hip Lens", PrimitiveType.Cube, upper, new Vector3(0.24f, -0.02f, 0.06f), Vector3.zero, new Vector3(0.01f, 0.06f, 0.06f), fieldModule);

            // Gravity Staff in the right hand: held vertically and planted at rest, ring head with a cyan core,
            // charcoal segments, a small ORIGIN band on the grip. It swings with the arm.
            var weapon = new GameObject("Gravity Staff");
            weapon.layer = 2;
            weapon.transform.SetParent(rightShoulder, false);
            weapon.transform.localPosition = new Vector3(0.09f, -0.59f, 0.09f);
            Part("Shaft", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(0.035f, 1.9f, 0.035f), staff);
            Part("Segment Low", PrimitiveType.Cube, weapon.transform, new Vector3(0f, -0.45f, 0f), Vector3.zero, new Vector3(0.055f, 0.14f, 0.055f), charcoal);
            Part("Grip Band", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.04f, 0.02f, 0.04f), amber);
            Part("Segment High", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 1.02f, 0f), Vector3.zero, new Vector3(0.06f, 0.16f, 0.06f), charcoal);
            // Ring head: twelve short segments in the staff's x/y plane around a small cyan core.
            var ring = Pivot("Field Ring", weapon.transform, new Vector3(0f, 1.3f, 0f));
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                Vector3 offset = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.13f, 0f);
                Part("Ring Segment", PrimitiveType.Cube, ring, offset, new Vector3(0f, 0f, angle), new Vector3(0.072f, 0.022f, 0.022f), i % 3 == 0 ? charcoal : fieldModule);
            }
            Part("Ring Core", PrimitiveType.Sphere, weapon.transform, new Vector3(0f, 1.3f, 0f), Vector3.zero, Vector3.one * 0.05f, fieldModule);
            Part("Ring Stem", PrimitiveType.Cube, weapon.transform, new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(0.012f, 0.26f, 0.012f), staff);

            // Procedural animation: calmer, more precise than the Tracewalker. Smaller swings, slower response,
            // measured strike, no Tactical Focus gesture (she is not a Tracewalker).
            var puppet = Puppet(member, root, upper, headPivot, leftHip, rightHip, leftShoulder, rightShoulder);
            Number(puppet, "legSwing", 22f);
            Number(puppet, "armSwing", 10f);
            Number(puppet, "bobHeight", 0.018f);
            Number(puppet, "sprintLean", 5f);
            Number(puppet, "breathing", 0.5f);
            Number(puppet, "attackWindupPitch", 30f);
            Number(puppet, "attackSwingPitch", 60f);
            Number(puppet, "dodgeCrouch", 0.2f);
            Number(puppet, "dodgeLean", 8f);
            Number(puppet, "dashLean", 12f);
            Number(puppet, "hitFlinch", 9f);
            Number(puppet, "poseResponse", 9f);
            Number(puppet, "strikeResponse", 18f);
            Flag(puppet, "focusGesture", false);

            var feedback = member.GetComponent<CompanionFeedback>();
            if (feedback != null)
            {
                Reference(feedback, "visual", root);
                Reference(feedback, "bodyRenderer", torso.GetComponent<Renderer>());
            }
            Profile(member, "origin_control_01", "Control", "Field Specialist — Control", "ORIGIN", "Mid-Range Controller", false, Cyan);
            var fieldControl = member.GetComponent<FieldControlModule>();
            if (fieldControl == null) fieldControl = member.AddComponent<FieldControlModule>();
            Reference(fieldControl, "emitter", core.GetComponent<Renderer>());
            Reference(fieldControl, "skill", member.GetComponent<GravityFieldSkill>());
        }

        // Gravity Field as ORIGIN field technology: cyan rings, counter-rotation, slow width pulse.
        private static void DressField(GameObject member)
        {
            var skill = member.GetComponent<GravityFieldSkill>();
            if (skill == null || skill.Field == null) return;
            var fieldVisual = new SerializedObject(skill.Field).FindProperty("visual").objectReferenceValue as Transform;
            if (fieldVisual == null) return;
            var rings = fieldVisual.GetComponentsInChildren<LineRenderer>(true).OrderBy(r => r.name).ToArray();
            var fieldMaterial = Unlit(MaterialFolder, "M_Control_Field", new Color(0.45f, 0.88f, 1f));
            foreach (var ring in rings) ring.sharedMaterial = fieldMaterial;
            var pulse = fieldVisual.GetComponent<FieldVisualPulse>();
            if (pulse == null) pulse = fieldVisual.gameObject.AddComponent<FieldVisualPulse>();
            var data = new SerializedObject(pulse);
            var ringArray = data.FindProperty("rings");
            var spinArray = data.FindProperty("spinDegreesPerSecond");
            ringArray.arraySize = spinArray.arraySize = rings.Length;
            for (int i = 0; i < rings.Length; i++)
            {
                ringArray.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
                // Core, Inner Radius, Outer Radius (alphabetical): fast core, counter-rotating inner, slow outer.
                spinArray.GetArrayElementAtIndex(i).floatValue = rings[i].name.StartsWith("Core") ? 40f : rings[i].name.StartsWith("Inner") ? -18f : 9f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
