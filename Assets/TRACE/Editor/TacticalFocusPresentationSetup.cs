using System;
using System.Linq;
using System.Reflection;
using TRACE.AI;
using TRACE.Combat;
using TRACE.Narrative;
using TRACE.Rendering;
using TRACE.Tactical;
using TRACE.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TRACE.Editor
{
    // TRACE Tactical Focus presentation: the AudioMixer (Master > Music, Ambience, World, Combat, UI, Tactical; snapshots
    // Normal and TacticalFocus), the grade and aberration profiles, the line / ghost materials, and one "Tactical Focus
    // Presentation" object per playable scene wired to the squad, the enemies, the HUD and the tactical overlay.
    // Idempotent: assets are created when missing and re-tuned on every run; the scene object is rebuilt.
    public static class TacticalFocusPresentationSetup
    {
        public const string MixerPath = "Assets/TRACE/Audio/TRACE_Mixer.mixer";
        private const string Folder = "Assets/TRACE/Tactical/Presentation";
        private const string GradePath = Folder + "/TacticalFocusGrade.asset";
        private const string AberrationPath = Folder + "/TacticalFocusAberration.asset";
        private const string LineMaterialPath = Folder + "/M_TacticalFocus_Line.mat";
        private const string GhostMaterialPath = Folder + "/M_TacticalFocus_Ghost.mat";
        private const string RetiredGradePath = "Assets/TRACE/UI/HudFocusGrade.asset";
        private const string ClipFolder = "Assets/TRACE/Audio/Tactical/";
        private const string OutlineFolder = "Assets/TRACE/Rendering";
        private static readonly string[] RendererAssets = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
        // Tier: rendering layer, base colour, accent colour, width (px), pulse Hz, pulse brightness, accent at peak.
        // Standard is a quiet desaturated cyan; locked is crisper with a very slow breath; threat warms toward orange
        // in short pulses; combo keeps the cyan and gains an amber-white pulse. Never only a colour change.
        private static readonly (string name, int layer, Color color, Color accent, float width, float pulse, float pulseAmount, float accentAmount)[] OutlineTiers =
        {
            ("Standard", 8, new Color(0.62f, 0.82f, 0.9f, 0.5f), Color.white, 1.3f, 0f, 0f, 0f),
            ("Locked", 9, new Color(0.6f, 0.93f, 1f, 0.92f), new Color(0.88f, 0.98f, 1f, 1f), 1.8f, 0.5f, 0.15f, 0.25f),
            ("Threat", 10, new Color(0.75f, 0.9f, 0.95f, 0.95f), new Color(1f, 0.7f, 0.45f, 1f), 1.8f, 2.2f, 0.3f, 0.85f),
            ("Combo", 11, new Color(0.62f, 0.86f, 0.95f, 0.75f), new Color(1f, 0.86f, 0.55f, 1f), 1.5f, 1.2f, 0.2f, 0.6f),
        };
        private static readonly string[] RenderingLayerNames = { "TacticalReadable", "TacticalLocked", "TacticalThreat", "TacticalCombo" };
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
            "Assets/TRACE/Scenes/FieldTest.unity",
        };
        private static readonly string[] Groups = { "Music", "Ambience", "World", "Combat", "UI", "Voice", "Tactical" };
        // Group: (low-pass cutoff in Focus, attenuation in Focus). The tactical layer, UI and warnings stay clear.
        private static readonly (string group, float cutoff, float volume)[] FocusMix =
        {
            // Tuned by ear (2026-10-09): the place goes well behind the membrane so the bed can take the room.
            ("World", 1600f, -9f),
            ("Ambience", 800f, -14f),
            ("Music", 2400f, -8f),
            ("Combat", 4000f, -3f),
            ("Voice", 6000f, -2f),
        };
        private const float OpenCutoff = 22000f;

        [MenuItem("TRACE/Apply Tactical Focus Presentation V1")]
        public static void Apply() => ApplyTo(Scenes);

        // The same pass on chosen scenes only (FieldTestSetup uses it to leave the other scenes untouched).
        public static void ApplyTo(params string[] paths)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SquadVisualKit.EnsureFolder(Folder);
            var mixer = BuildMixer();
            var grade = GradeProfile();
            var aberration = AberrationProfile();
            var line = TransparentMaterial(LineMaterialPath, Color.white);
            var ghost = TransparentMaterial(GhostMaterialPath, new Color(0.55f, 0.9f, 1f, 0.22f));
            AudioSetup.ConfigureImports();
            SetupOutline();
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(RetiredGradePath) != null) AssetDatabase.DeleteAsset(RetiredGradePath);
            int applied = 0;
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                Build(scene, mixer, grade, aberration, line, ghost);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Tactical Focus presentation applied to {applied} scene(s).");
        }

        public static AudioMixerGroup MixerGroup(string name)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            return mixer != null ? mixer.FindMatchingGroups("Master/" + name).FirstOrDefault(g => g.name == name) : null;
        }

        // HudSetup rebuilds the HUD; it calls this so the new focus layer follows the presentation's reveal timing.
        public static void Relink(HudRoot hud)
        {
            var presentation = Object.FindFirstObjectByType<TacticalFocusPresentationController>();
            if (presentation == null) return;
            var overlay = hud.GetComponentInChildren<TacticalFocusOverlay>(true);
            if (overlay != null) SquadVisualKit.Reference(overlay, "presentation", presentation);
        }

        // ---- Scene ----------------------------------------------------------------------------------------------

        private static void Build(UnityEngine.SceneManagement.Scene scene, AudioMixer mixer, VolumeProfile grade, VolumeProfile aberration, Material line, Material ghost)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Tactical Focus Presentation" || root.name == "HUD Focus Grade") Object.DestroyImmediate(root);
            var squad = Object.FindFirstObjectByType<SquadController>();
            if (squad == null) throw new InvalidOperationException(scene.path + " has no squad.");
            var focus = squad.GetComponent<TacticalFocus>();
            var enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();

            var go = new GameObject("Tactical Focus Presentation");
            var gradeVolume = Child<Volume>(go, "Grade Volume");
            gradeVolume.isGlobal = true; gradeVolume.priority = 10f; gradeVolume.weight = 0f; gradeVolume.sharedProfile = grade;
            var aberrationVolume = Child<Volume>(go, "Aberration Volume");
            aberrationVolume.isGlobal = true; aberrationVolume.priority = 11f; aberrationVolume.weight = 1f; aberrationVolume.sharedProfile = aberration;
            var tactical = MixerGroup("Tactical");
            var cue = Child<AudioSource>(go, "Cue Source");
            cue.playOnAwake = false; cue.spatialBlend = 0f; cue.priority = 24; cue.outputAudioMixerGroup = tactical;
            var bed = Child<AudioSource>(go, "Bed Source");
            bed.playOnAwake = false; bed.loop = true; bed.spatialBlend = 0f; bed.volume = 0f; bed.priority = 40; bed.outputAudioMixerGroup = tactical;
            bed.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipFolder + "AMB_TacticalFocus_FLOAT_LOOP.ogg");
            if (bed.clip == null) throw new InvalidOperationException("Missing AMB_TacticalFocus_FLOAT_LOOP");
            var ring = Child<LineRenderer>(go, "Scan Ring");
            ring.gameObject.layer = 2;
            ring.useWorldSpace = true; ring.loop = true; ring.positionCount = 64; ring.widthMultiplier = 0.09f;
            ring.sharedMaterial = line; ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false; ring.enabled = false;

            var controller = go.AddComponent<TacticalFocusPresentationController>();
            var data = new SerializedObject(controller);
            data.FindProperty("focus").objectReferenceValue = focus;
            data.FindProperty("squad").objectReferenceValue = squad;
            data.FindProperty("mixer").objectReferenceValue = mixer;
            data.FindProperty("normalSnapshot").objectReferenceValue = mixer.FindSnapshot("Normal");
            data.FindProperty("focusSnapshot").objectReferenceValue = mixer.FindSnapshot("TacticalFocus");
            data.FindProperty("cueSource").objectReferenceValue = cue;
            data.FindProperty("enterClip").objectReferenceValue = Clip("SFX_TacticalFocus_Enter");
            data.FindProperty("exitClip").objectReferenceValue = Clip("SFX_TacticalFocus_Exit");
            data.FindProperty("bedSource").objectReferenceValue = bed;
            data.FindProperty("artefactClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TRACE/Audio/Gameplay/Targeting/SFX_UI_TargetChange_01.wav");
            data.FindProperty("gradeVolume").objectReferenceValue = gradeVolume;
            data.FindProperty("aberrationVolume").objectReferenceValue = aberrationVolume;
            data.FindProperty("scanRing").objectReferenceValue = ring;
            data.ApplyModifiedPropertiesWithoutUndo();

            var readability = go.AddComponent<TacticalReadability>();
            data = new SerializedObject(readability);
            data.FindProperty("presentation").objectReferenceValue = controller;
            data.FindProperty("squad").objectReferenceValue = squad;
            data.FindProperty("targeting").objectReferenceValue = squad.GetComponent<TargetingSystem>();
            data.FindProperty("lineMaterial").objectReferenceValue = line;
            data.FindProperty("ghostMaterial").objectReferenceValue = ghost;
            var layers = data.FindProperty("outlineLayers");
            layers.arraySize = OutlineTiers.Length;
            for (int i = 0; i < OutlineTiers.Length; i++) layers.GetArrayElementAtIndex(i).intValue = OutlineTiers[i].layer;
            var array = data.FindProperty("enemies");
            array.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            data.ApplyModifiedPropertiesWithoutUndo();

            var overlay = squad.GetComponent<TacticalOverlay>();
            if (overlay != null) SquadVisualKit.Reference(overlay, "presentation", controller);
            var hud = Object.FindFirstObjectByType<HudRoot>();
            if (hud != null) Relink(hud);
            // Routing of the existing sources.
            var hudAudio = Object.FindFirstObjectByType<HudAudio>();
            if (hudAudio != null && hudAudio.TryGetComponent(out AudioSource uiSource)) uiSource.outputAudioMixerGroup = MixerGroup("UI");
            var ambience = Object.FindFirstObjectByType<AmbienceController>();
            if (ambience != null)
            {
                SquadVisualKit.Reference(ambience, "ambienceGroup", MixerGroup("Ambience"));
                SquadVisualKit.Reference(ambience, "musicGroup", MixerGroup("Music"));
            }
        }

        private static T Child<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipFolder + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Missing clip " + name);
            return clip;
        }

        // ---- Outline ------------------------------------------------------------------------------------------------

        // Named rendering layers, one material per tier, and the renderer feature on both URP renderers.
        private static void SetupOutline()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var names = tagManager.FindProperty("m_RenderingLayers");
            for (int i = 0; i < OutlineTiers.Length; i++)
            {
                int index = OutlineTiers[i].layer;
                if (names.arraySize <= index) names.arraySize = index + 1;
                names.GetArrayElementAtIndex(index).stringValue = RenderingLayerNames[i];
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            var shader = Shader.Find("TRACE/TacticalOutline");
            if (shader == null) throw new InvalidOperationException("TRACE/TacticalOutline shader is missing.");
            var materials = new Material[OutlineTiers.Length];
            for (int i = 0; i < OutlineTiers.Length; i++)
            {
                var tier = OutlineTiers[i];
                string path = $"{OutlineFolder}/M_TacticalOutline_{tier.name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.SetColor("_OutlineColor", tier.color);
                material.SetColor("_AccentColor", tier.accent);
                material.SetFloat("_OutlineWidth", tier.width);
                material.SetFloat("_PulseSpeed", tier.pulse);
                material.SetFloat("_PulseAmount", tier.pulseAmount);
                material.SetFloat("_AccentAmount", tier.accentAmount);
                EditorUtility.SetDirty(material);
                materials[i] = material;
            }

            foreach (string path in RendererAssets)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null) continue;
                var feature = rendererData.rendererFeatures.OfType<TacticalOutlineFeature>().FirstOrDefault();
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<TacticalOutlineFeature>();
                    feature.name = "Tactical Outline";
                    AssetDatabase.AddObjectToAsset(feature, rendererData);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                    var dataObject = new SerializedObject(rendererData);
                    var list = dataObject.FindProperty("m_RendererFeatures");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                    var map = dataObject.FindProperty("m_RendererFeatureMap");
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    dataObject.ApplyModifiedPropertiesWithoutUndo();
                }
                var featureData = new SerializedObject(feature);
                var tiers = featureData.FindProperty("tiers");
                tiers.arraySize = OutlineTiers.Length;
                for (int i = 0; i < OutlineTiers.Length; i++)
                {
                    var entry = tiers.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("name").stringValue = OutlineTiers[i].name;
                    entry.FindPropertyRelative("material").objectReferenceValue = materials[i];
                    entry.FindPropertyRelative("renderingLayer").intValue = OutlineTiers[i].layer;
                }
                featureData.ApplyModifiedPropertiesWithoutUndo();
                feature.SetActive(true);
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
                EditorUtility.SetDirty(feature);
            }
        }

        // ---- Post-process and materials ---------------------------------------------------------------------------

        // Values here are only the starting point; the controller applies its Inspector values to a runtime copy.
        private static VolumeProfile GradeProfile()
        {
            var profile = LoadOrCreateProfile(GradePath);
            var color = Component<ColorAdjustments>(profile);
            color.saturation.Override(-20f);
            color.contrast.Override(8f);
            color.postExposure.Override(-0.1f);
            color.colorFilter.Override(new Color(0.94f, 0.985f, 1f));
            color.hueShift.overrideState = false;
            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile AberrationProfile()
        {
            var profile = LoadOrCreateProfile(AberrationPath);
            Component<ChromaticAberration>(profile).intensity.Override(0f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile LoadOrCreateProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            var added = profile.Add<T>(false);
            AssetDatabase.AddObjectToAsset(added, profile);
            return added;
        }

        // URP Particles/Unlit, alpha-blended, colour from vertex colour (lines) or _BaseColor (ghost).
        private static Material TransparentMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---- AudioMixer -------------------------------------------------------------------------------------------
        // Unity has no public API to author a mixer; the editor's own AudioMixerController calls are used by reflection.

        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type EditorType(string name) => typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Audio." + name, true);
        private static object Call(object target, string method, params object[] args) =>
            (target as Type ?? target.GetType()).GetMethods(Any).First(m => m.Name == method && m.GetParameters().Length == args.Length)
                .Invoke(target is Type ? null : target, args);
        private static object Get(object target, string property) => target.GetType().GetProperty(property, Any).GetValue(target);

        internal static AudioMixer BuildMixer()
        {
            var controllerType = EditorType("AudioMixerController");
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            object controller = mixer != null ? mixer : Call(controllerType, "CreateMixerControllerAtPath", MixerPath);
            mixer = (AudioMixer)controller;
            mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            var master = Get(controller, "masterGroup");
            var groups = ((System.Collections.IEnumerable)Call(controller, "GetAllAudioGroupsSlow")).Cast<object>().ToList();
            foreach (string name in Groups)
            {
                if (groups.Any(g => ((Object)g).name == name)) continue;
                var group = Call(controller, "CreateNewGroup", name, false);
                Call(controller, "AddChildToParent", group, master);
                // Display only (Audio Mixer window views); a mixer created from code has no view yet.
                try { Call(controller, "AddGroupToCurrentView", group); } catch (TargetInvocationException) { }
                groups.Add(group);
            }

            var snapshots = ((Array)Get(controller, "snapshots")).Cast<object>().ToList();
            var normal = snapshots[0];
            ((Object)normal).name = "Normal";
            var focus = snapshots.FirstOrDefault(s => ((Object)s).name == "TacticalFocus");
            if (focus == null)
            {
                Call(controller, "CloneNewSnapshotFromTarget", false);
                focus = ((Array)Get(controller, "snapshots")).Cast<object>().Last();
                ((Object)focus).name = "TacticalFocus";
            }
            controller.GetType().GetProperty("TargetSnapshot", Any).SetValue(controller, normal);
            controller.GetType().GetProperty("startSnapshot", Any).SetValue(controller, normal);

            var effectType = EditorType("AudioMixerEffectController");
            foreach (var group in groups)
            {
                string name = ((Object)group).name;
                if (name == "Master") continue;
                var entry = FocusMix.FirstOrDefault(m => m.group == name);
                Call(group, "SetValueForVolume", controller, normal, 0f);
                Call(group, "SetValueForVolume", controller, focus, entry.group != null ? entry.volume : 0f);
                if (entry.group == null) continue;
                var effects = ((Array)Get(group, "effects")).Cast<object>().ToList();
                var lowpass = effects.FirstOrDefault(e => (string)Get(e, "effectName") == "Lowpass Simple");
                if (lowpass == null)
                {
                    lowpass = Activator.CreateInstance(effectType, "Lowpass Simple");
                    Call(lowpass, "PreallocateGUIDs");
                    AssetDatabase.AddObjectToAsset((Object)lowpass, mixer);
                    Call(group, "InsertEffect", lowpass, effects.Count);
                }
                Call(lowpass, "SetValueForParameter", controller, normal, "Cutoff freq", OpenCutoff);
                Call(lowpass, "SetValueForParameter", controller, focus, "Cutoff freq", entry.cutoff);
            }
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
            return mixer;
        }
    }
}
