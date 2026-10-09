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
    // Dresses member 3 as SUPPORT, ORIGIN Field Specialist (concept V3): a compact, defensive primitive body with a
    // long off-white coat and hood, light ash hair in a high ponytail with a mint clip, a barrier gauntlet on the
    // left forearm as her signature, a compact circular Support Module on the back, a discreet ORIGIN mark, the
    // concept portrait and a calm, guarded puppet. No long weapon. Pulse Shield is projected from the gauntlet and
    // amplified by the module; halos stay mint on every member. Gameplay untouched.
    public static class SupportSetup
    {
        public const string MaterialFolder = "Assets/TRACE/Scenes/Materials/Support";
        public const string MemberName = "Member 3 - Support";
        public const string PortraitPath = "Assets/TRACE/UI/Portraits/Portrait_Support.png";
        // Turquoise-mint from the concept palette: protection and stabilization. Green leads, unlike Control's cyan.
        public static readonly Color Mint = new Color(0.36f, 0.9f, 0.74f);
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
            "Assets/TRACE/Scenes/FieldTest.unity",
        };
        // Materials from the previous pass that no longer have a part to dress.
        private static readonly string[] Retired = { "M_Support_Staff" };

        [MenuItem("TRACE/Apply Support V0.2")]
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
                var shieldMaterial = Tuned("M_Support_Shield", new Color(0.42f, 0.95f, 0.8f), 0f, unlit: true);
                Dress(member, squad, shieldMaterial);
                DressShields(squad, shieldMaterial);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            foreach (string name in Retired) AssetDatabase.DeleteAsset(MaterialFolder + "/" + name + ".mat");
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Support V0.2 applied to {applied} scene(s).");
        }

        // Load-or-create, then (re)tune: re-running the setup applies palette changes to existing assets.
        private static Material Tuned(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null, bool unlit = false)
        {
            var material = unlit ? Unlit(MaterialFolder, name, color) : Mat(MaterialFolder, name, color, smoothness, metallic, emission);
            material.SetColor("_BaseColor", color);
            if (!unlit)
            {
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                if (emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Dress(GameObject member, TRACE.AI.SquadController squad, Material shieldMaterial)
        {
            ClearChildren(member, "Support Visual", "Body");
            var charcoal = Tuned("M_Support_Charcoal", new Color(0.11f, 0.12f, 0.13f), 0.25f);
            var shadow = Tuned("M_Support_Shadow", new Color(0.04f, 0.04f, 0.05f), 0.35f);
            var grey = Tuned("M_Support_CoolGrey", new Color(0.62f, 0.6f, 0.58f), 0.2f);
            var offWhite = Tuned("M_Support_OffWhite", new Color(0.88f, 0.88f, 0.86f), 0.2f);
            var hair = Tuned("M_Support_Hair", new Color(0.74f, 0.71f, 0.66f), 0.3f);
            var mint = Tuned("M_Support_Mint", new Color(0.4f, 0.86f, 0.74f), 0.35f);
            var amber = Tuned("M_Support_OriginAmber", TracewalkerSetup.Amber, 0.3f);
            // Technical plating shared by the gauntlet and the module shell: brighter and harder than the coat.
            var plating = Tuned("M_Support_Gauntlet", new Color(0.93f, 0.93f, 0.91f), 0.6f, metallic: 0.15f);
            var module = Tuned("M_Support_Module", new Color(0.05f, 0.12f, 0.11f), 0.45f, emission: Mint * 0.6f);

            var visual = new GameObject("Support Visual");
            visual.layer = 2;
            visual.transform.SetParent(member.transform, false);
            var root = visual.transform;
            // Planted stance, slightly lower shoulders: compact and defensive next to Control's vertical line.
            var upper = Pivot("Upper Body", root, new Vector3(0f, 0.92f, 0f));
            var headPivot = Pivot("Head", upper, new Vector3(0f, 0.72f, 0f));
            Transform leftHip = null, rightHip = null, leftShoulder = null, rightShoulder = null;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Pivot(side < 0 ? "Hip L" : "Hip R", root, new Vector3(0.12f * side, 0.92f, 0f));
                Part("Leg", PrimitiveType.Cube, hip, new Vector3(0f, -0.42f, 0f), Vector3.zero, new Vector3(0.17f, 0.85f, 0.21f), charcoal);
                Part("Knee Guard", PrimitiveType.Cube, hip, new Vector3(0f, -0.5f, 0.1f), Vector3.zero, new Vector3(0.16f, 0.16f, 0.03f), shadow);
                Part("Boot", PrimitiveType.Cube, hip, new Vector3(0f, -0.85f, 0.02f), Vector3.zero, new Vector3(0.19f, 0.17f, 0.3f), shadow);
                Part("Boot Cap", PrimitiveType.Cube, hip, new Vector3(0f, -0.76f, 0.02f), Vector3.zero, new Vector3(0.195f, 0.05f, 0.305f), offWhite);
                var shoulder = Pivot(side < 0 ? "Shoulder L" : "Shoulder R", upper, new Vector3(0.28f * side, 0.44f, 0f));
                Part("Arm", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.27f, 0f), Vector3.zero, new Vector3(0.12f, 0.52f, 0.14f), charcoal);
                Part("Sleeve", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.13f, 0f), Vector3.zero, new Vector3(0.16f, 0.28f, 0.18f), offWhite);
                Part("Glove", PrimitiveType.Cube, shoulder, new Vector3(0f, -0.59f, 0.01f), Vector3.zero, new Vector3(0.13f, 0.13f, 0.15f), shadow);
                Part("Shoulder Pad", PrimitiveType.Cube, upper, new Vector3(0.27f * side, 0.51f, 0f), Vector3.zero, new Vector3(0.16f, 0.08f, 0.22f), offWhite);
                Part("Coat Side", PrimitiveType.Cube, upper, new Vector3(0.24f * side, -0.2f, -0.02f), Vector3.zero, new Vector3(0.06f, 1.16f, 0.3f), offWhite);
                Part("Coat Flap", PrimitiveType.Cube, upper, new Vector3(0.11f * side, -0.24f, 0.125f), Vector3.zero, new Vector3(0.14f, 1.02f, 0.018f), offWhite);
                Part("Flap Edge", PrimitiveType.Cube, upper, new Vector3(0.045f * side, -0.24f, 0.13f), Vector3.zero, new Vector3(0.012f, 1.02f, 0.02f), charcoal);
                if (side < 0) { leftHip = hip; leftShoulder = shoulder; } else { rightHip = hip; rightShoulder = shoulder; }
            }
            Part("Cuff", PrimitiveType.Cube, rightShoulder, new Vector3(0f, -0.5f, 0.01f), Vector3.zero, new Vector3(0.135f, 0.02f, 0.155f), mint);
            var torso = Part("Torso", PrimitiveType.Cube, upper, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.38f, 0.54f, 0.24f), charcoal);
            Part("Chest Plate", PrimitiveType.Cube, upper, new Vector3(0f, 0.32f, 0.125f), Vector3.zero, new Vector3(0.24f, 0.24f, 0.015f), charcoal);
            Part("Chest Module", PrimitiveType.Cube, upper, new Vector3(0f, 0.3f, 0.135f), Vector3.zero, new Vector3(0.09f, 0.09f, 0.02f), plating);
            Part("Chest Lens", PrimitiveType.Cube, upper, new Vector3(0f, 0.3f, 0.147f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.006f), module);
            Part("Harness", PrimitiveType.Cube, upper, new Vector3(0f, 0.02f, 0.01f), Vector3.zero, new Vector3(0.4f, 0.05f, 0.26f), shadow);
            Part("Collar", PrimitiveType.Cube, upper, new Vector3(0f, 0.53f, 0f), Vector3.zero, new Vector3(0.32f, 0.04f, 0.26f), charcoal);
            Part("Coat Back", PrimitiveType.Cube, upper, new Vector3(0f, -0.14f, -0.14f), Vector3.zero, new Vector3(0.46f, 1.06f, 0.05f), offWhite);
            Part("Hood", PrimitiveType.Cube, upper, new Vector3(0f, 0.6f, -0.15f), Vector3.zero, new Vector3(0.38f, 0.17f, 0.22f), offWhite);

            // Light ash hair: fringe, shoulder-length sides, a high ponytail and a small mint tech clip.
            Part("Skull", PrimitiveType.Sphere, headPivot, Vector3.zero, Vector3.zero, Vector3.one * 0.27f, grey);
            Part("Hair Top", PrimitiveType.Cube, headPivot, new Vector3(0f, 0.1f, -0.03f), Vector3.zero, new Vector3(0.29f, 0.09f, 0.29f), hair);
            Part("Fringe", PrimitiveType.Cube, headPivot, new Vector3(0f, 0.065f, 0.125f), new Vector3(-12f, 0f, 0f), new Vector3(0.26f, 0.07f, 0.04f), hair);
            Part("Hair Back", PrimitiveType.Cube, headPivot, new Vector3(0f, -0.08f, -0.12f), Vector3.zero, new Vector3(0.28f, 0.24f, 0.08f), hair);
            for (int side = -1; side <= 1; side += 2)
                Part("Side Lock", PrimitiveType.Cube, headPivot, new Vector3(0.135f * side, -0.09f, 0.03f), Vector3.zero, new Vector3(0.04f, 0.28f, 0.09f), hair);
            var tail = Pivot("Ponytail", headPivot, new Vector3(0.02f, 0.13f, -0.14f));
            Part("Tail", PrimitiveType.Cube, tail, new Vector3(0f, -0.13f, -0.05f), new Vector3(28f, 0f, 0f), new Vector3(0.08f, 0.32f, 0.07f), hair);
            Part("Hair Clip", PrimitiveType.Cube, tail, new Vector3(0.05f, 0f, 0f), Vector3.zero, new Vector3(0.025f, 0.07f, 0.05f), mint);

            // ORIGIN institutional mark on the right shoulder plate, small and outward; the gauntlet carries the left.
            OriginMark("Origin Mark", upper, new Vector3(0.355f, 0.51f, 0f), Quaternion.Euler(0f, 90f, 0f), 0.1f, offWhite, shadow, amber);

            var gauntlet = BuildGauntlet(leftShoulder, charcoal, plating, module, amber, shieldMaterial, out var plates, out var emitters, out var emitterPoint, out var projection);

            // Support Module: compact circular stabilizer on the back. Same white plating as the gauntlet, a mint
            // core and a segmented ring that turns while a shield is up. It amplifies; the gauntlet projects.
            var moduleRoot = Pivot("Support Module", upper, new Vector3(0f, 0.22f, -0.19f));
            Part("Housing", PrimitiveType.Cylinder, moduleRoot, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.24f, 0.03f, 0.24f), charcoal);
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                Vector3 offset = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.13f, 0f);
                Part("Shell Plate", PrimitiveType.Cube, moduleRoot, offset + new Vector3(0f, 0f, -0.02f), new Vector3(0f, 0f, angle), new Vector3(0.09f, 0.045f, 0.04f), plating);
            }
            var moduleRing = Pivot("Module Ring", moduleRoot, new Vector3(0f, 0f, -0.035f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 offset = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.075f, 0f);
                Part("Ring Segment", PrimitiveType.Cube, moduleRing, offset, new Vector3(0f, 0f, angle), new Vector3(0.05f, 0.016f, 0.016f), i % 4 == 0 ? charcoal : module);
            }
            var core = Part("Module Core", PrimitiveType.Sphere, moduleRoot, new Vector3(0f, 0f, -0.04f), Vector3.zero, Vector3.one * 0.065f, module);
            Part("Module Band", PrimitiveType.Cube, moduleRoot, new Vector3(0f, -0.155f, -0.01f), Vector3.zero, new Vector3(0.09f, 0.012f, 0.014f), amber);
            Part("Hip Pouch", PrimitiveType.Cube, upper, new Vector3(0.21f, -0.04f, 0.05f), Vector3.zero, new Vector3(0.07f, 0.11f, 0.1f), charcoal);
            Part("Pouch Lens", PrimitiveType.Cube, upper, new Vector3(0.25f, -0.04f, 0.05f), Vector3.zero, new Vector3(0.01f, 0.05f, 0.05f), module);

            // Procedural animation: the calmest of the three, gauntlet carried slightly forward, raised in combat,
            // extended to cast Pulse Shield.
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
            Number(puppet, "guardArmPitch", 16f);
            Number(puppet, "combatGuardPitch", 34f);
            Number(puppet, "skillArmPitch", 85f);
            Number(puppet, "skillGestureTime", 0.5f);
            Flag(puppet, "focusGesture", false);
            Reference(puppet, "skill", member.GetComponent<PulseShield>());
            Reference(puppet, "ranged", member.GetComponent<PlayerRangedAttack>());

            var feedback = member.GetComponent<CompanionFeedback>();
            if (feedback != null)
            {
                Reference(feedback, "visual", root);
                Reference(feedback, "bodyRenderer", torso.GetComponent<Renderer>());
            }
            var profile = Profile(member, "origin_support_01", "Support", "Field Specialist — Support", "ORIGIN", "Defensive Support", false, Mint);
            Reference(profile, "portrait", AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath));
            var shields = squad != null ? squad.Members.Select(m => m.GetComponent<Shield>()).Where(s => s != null).ToArray() : new Shield[0];

            var supportModule = member.GetComponent<SupportModule>();
            if (supportModule == null) supportModule = member.AddComponent<SupportModule>();
            Reference(supportModule, "emitter", core.GetComponent<Renderer>());
            Reference(supportModule, "ring", moduleRing);
            var data = new SerializedObject(supportModule);
            data.FindProperty("emissionColor").colorValue = Mint;
            Objects(data.FindProperty("shields"), shields);
            data.ApplyModifiedPropertiesWithoutUndo();

            var gauntletComponent = member.GetComponent<SupportGauntlet>();
            if (gauntletComponent == null) gauntletComponent = member.AddComponent<SupportGauntlet>();
            var tethers = new LineRenderer[shields.Length];
            for (int i = 0; i < shields.Length; i++)
            {
                tethers[i] = Line("Shield Link " + (i + 1), gauntlet, new[] { Vector3.zero, Vector3.zero }, false, 0.022f, shieldMaterial, worldSpace: true);
                tethers[i].enabled = false;
            }
            data = new SerializedObject(gauntletComponent);
            data.FindProperty("skill").objectReferenceValue = member.GetComponent<PulseShield>();
            data.FindProperty("emitterPoint").objectReferenceValue = emitterPoint;
            data.FindProperty("projection").objectReferenceValue = projection;
            data.FindProperty("emissionColor").colorValue = Mint;
            Objects(data.FindProperty("shields"), shields);
            Objects(data.FindProperty("emitters"), emitters);
            Objects(data.FindProperty("plates"), plates);
            Objects(data.FindProperty("tethers"), tethers);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // SupportGauntlet on the left forearm (the arm axis is local -y): white plated shell over a charcoal
        // underlay, two deployable plates, a mint emitter disc facing outward, a thin ORIGIN band, a knuckle guard,
        // and the emitter point past the hand where the hexagonal barrier is projected.
        private static Transform BuildGauntlet(Transform shoulder, Material charcoal, Material plating, Material module, Material amber,
            Material shieldMaterial, out Transform[] plates, out Renderer[] emitters, out Transform emitterPoint, out Transform projection)
        {
            var gauntlet = Pivot("Support Gauntlet", shoulder, new Vector3(0f, -0.42f, 0.01f));
            Part("Underlay", PrimitiveType.Cube, gauntlet, new Vector3(0f, -0.01f, 0f), Vector3.zero, new Vector3(0.15f, 0.3f, 0.17f), charcoal);
            Part("Shell", PrimitiveType.Cube, gauntlet, Vector3.zero, Vector3.zero, new Vector3(0.17f, 0.24f, 0.19f), plating);
            var outer = Part("Outer Plate", PrimitiveType.Cube, gauntlet, new Vector3(-0.095f, 0.02f, 0f), Vector3.zero, new Vector3(0.02f, 0.2f, 0.15f), plating);
            var top = Part("Top Plate", PrimitiveType.Cube, gauntlet, new Vector3(0f, 0.02f, 0.1f), Vector3.zero, new Vector3(0.13f, 0.2f, 0.02f), plating);
            Part("Origin Band", PrimitiveType.Cube, gauntlet, new Vector3(0f, 0.105f, 0f), Vector3.zero, new Vector3(0.175f, 0.012f, 0.195f), amber);
            Part("Emitter Housing", PrimitiveType.Cylinder, gauntlet, new Vector3(-0.112f, -0.04f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.11f, 0.012f, 0.11f), charcoal);
            var disc = Part("Emitter", PrimitiveType.Cylinder, gauntlet, new Vector3(-0.122f, -0.04f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.07f, 0.008f, 0.07f), module);
            var list = new System.Collections.Generic.List<Renderer> { disc.GetComponent<Renderer>() };
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 offset = Quaternion.Euler(angle, 0f, 0f) * new Vector3(0f, 0.058f, 0f);
                var segment = Part("Emitter Segment", PrimitiveType.Cube, gauntlet, new Vector3(-0.12f, -0.04f, 0f) + offset, new Vector3(angle, 0f, 0f), new Vector3(0.012f, 0.012f, 0.03f), i % 2 == 0 ? module : charcoal);
                if (i % 2 == 0) list.Add(segment.GetComponent<Renderer>());
            }
            var front = Part("Front Lens", PrimitiveType.Cube, gauntlet, new Vector3(0f, -0.06f, 0.106f), Vector3.zero, new Vector3(0.05f, 0.08f, 0.006f), module);
            list.Add(front.GetComponent<Renderer>());
            Part("Knuckle Guard", PrimitiveType.Cube, gauntlet, new Vector3(0f, -0.18f, 0.01f), Vector3.zero, new Vector3(0.14f, 0.05f, 0.16f), plating);
            emitters = list.ToArray();
            plates = new[] { outer.transform, top.transform };

            // Hexagonal barrier past the hand, in the plane across the arm: faces forward when the arm extends.
            emitterPoint = Pivot("Emitter Point", gauntlet, new Vector3(0f, -0.24f, 0f));
            projection = Pivot("Barrier Projection", emitterPoint, new Vector3(0f, -0.28f, 0f));
            Line("Outer Hex", projection, Hexagon(0.42f), true, 0.02f, shieldMaterial);
            Line("Inner Hex", projection, Hexagon(0.22f), true, 0.012f, shieldMaterial);
            for (int i = 0; i < 3; i++)
            {
                var a = Hexagon(0.42f)[i]; var b = Hexagon(0.42f)[i + 3];
                Line("Hex Spoke", projection, new[] { a, b }, false, 0.008f, shieldMaterial);
            }
            projection.gameObject.SetActive(false);
            return gauntlet;
        }

        private static Vector3[] Hexagon(float radius) => Enumerable.Range(0, 6)
            .Select(i => new Vector3(Mathf.Cos(i * Mathf.PI / 3f) * radius, 0f, Mathf.Sin(i * Mathf.PI / 3f) * radius)).ToArray();

        private static LineRenderer Line(string name, Transform parent, Vector3[] points, bool loop, float width, Material material, bool worldSpace = false)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = worldSpace;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void Objects(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // Pulse Shield as ORIGIN protection technology: mint halos on every member with a slow, calm rotation and
        // three very light meridian arcs that close them into a dome.
        private static void DressShields(TRACE.AI.SquadController squad, Material shieldMaterial)
        {
            if (squad == null) return;
            foreach (var member in squad.Members)
            {
                var shield = member.GetComponent<Shield>();
                if (shield == null) continue;
                var halo = new SerializedObject(shield).FindProperty("halo").objectReferenceValue as GameObject;
                if (halo == null) continue;
                var stale = halo.transform.Find("Dome");
                if (stale != null) UnityEngine.Object.DestroyImmediate(stale.gameObject);
                var rings = halo.GetComponentsInChildren<LineRenderer>(true).OrderBy(r => r.name).ToArray();
                foreach (var ring in rings) ring.sharedMaterial = shieldMaterial;
                var dome = Pivot("Dome", halo.transform, Vector3.zero);
                for (int i = 0; i < 3; i++)
                {
                    var arc = Enumerable.Range(0, 25).Select(k =>
                    {
                        float t = k * Mathf.PI / 24f;
                        return Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(Mathf.Cos(t) * 0.72f, 0.35f + Mathf.Sin(t) * 1.45f, 0f);
                    }).ToArray();
                    Line("Dome Arc", dome, arc, false, 0.012f, shieldMaterial);
                }
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
