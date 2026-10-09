using System;
using TRACE.Encounter;
using TRACE.Narrative;
using TRACE.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TRACE.Editor
{
    // TRACE Audio V0.1: functional UI sound. Configures the import of the curated clips in Assets/TRACE/Audio (small
    // mono UI one-shots: decompressed on load, ADPCM, preloaded) and places one 2D "HUD Audio" source with HudAudio in
    // every playable scene, with the first mix. Idempotent: re-running rebuilds the object and re-applies the mix.
    public static class AudioSetup
    {
        public const string AudioFolder = "Assets/TRACE/Audio";
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
            "Assets/TRACE/Scenes/FieldTest.unity",
        };

        // Event, clip, volume, pitch jitter, minimum interval. Order of importance: danger, skill / combo, targeting,
        // switch, secondary UI. Tactical Focus cues live in TacticalFocusPresentationSetup.
        private static readonly (HudSound sound, string clip, float volume, float jitter, float interval)[] Mix =
        {
            (HudSound.ThreatWarning, "Gameplay/Warnings/SFX_UI_ThreatWarning_01", 1f, 0f, 2.5f),
            (HudSound.ComboReady, "UI/SFX_UI_ComboReady_01", 0.6f, 0f, 0.4f),
            (HudSound.SkillReady, "UI/SFX_UI_SkillReady_01", 0.6f, 0.02f, 0.35f),
            (HudSound.TargetLock, "Gameplay/Targeting/SFX_UI_TargetLock_01", 0.6f, 0.02f, 0.05f),
            (HudSound.TargetUnlock, "Gameplay/Targeting/SFX_UI_TargetUnlock_01", 0.5f, 0.02f, 0.05f),
            (HudSound.TargetChange, "Gameplay/Targeting/SFX_UI_TargetChange_01", 0.35f, 0.03f, 0.04f),
            (HudSound.CharacterSwitch, "UI/SFX_UI_CharacterSwitch_01", 0.4f, 0.03f, 0.04f),
            (HudSound.ObjectiveUpdate, "UI/SFX_UI_ObjectiveUpdate_01", 0.4f, 0f, 1f),
            (HudSound.Interact, "UI/SFX_UI_Interact_01", 0.35f, 0.02f, 0.1f),
            (HudSound.Confirm, "UI/SFX_UI_Confirm_01", 0.25f, 0.02f, 0.08f),
        };

        [MenuItem("TRACE/Apply Audio V0.1")]
        public static void Apply() => ApplyTo(Scenes);

        // The same pass on chosen scenes only (FieldTestSetup uses it to leave the other scenes untouched).
        public static void ApplyTo(params string[] paths)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigureImports();
            int applied = 0;
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                Build(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Audio V0.1 applied to {applied} scene(s).");
        }

        // Short mono UI clips: decompressed on load for minimum latency, ADPCM keeps memory reasonable without
        // the Vorbis decode cost, preloaded so the first play is never late.
        internal static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Ambience loops and details (AMB_*) have their own import rules in AmbienceSetup.
                if (!System.IO.Path.GetFileName(path).StartsWith("SFX_")) continue;
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                importer.forceToMono = true;
                importer.loadInBackground = false;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                settings.preloadAudioData = true;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static void Build(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "HUD Audio") UnityEngine.Object.DestroyImmediate(root);
            var hud = UnityEngine.Object.FindFirstObjectByType<HudRoot>();
            if (hud == null) throw new InvalidOperationException(scene.path + " has no HUD; run TRACE/Apply HUD V0.1 first.");

            var go = new GameObject("HUD Audio");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            source.priority = 32;
            source.outputAudioMixerGroup = TacticalFocusPresentationSetup.MixerGroup("UI");
            var audio = go.AddComponent<HudAudio>();
            var data = new SerializedObject(audio);
            data.FindProperty("hud").objectReferenceValue = hud;
            data.FindProperty("mission").objectReferenceValue = hud.GetComponentInChildren<MissionPanel>(true);
            data.FindProperty("interaction").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<InteractionController>();
            data.FindProperty("threat").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<ThreatIndicator>();
            data.FindProperty("dialogue").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<DialogueRunner>();
            data.FindProperty("source").objectReferenceValue = source;
            var cues = data.FindProperty("cues");
            cues.arraySize = Enum.GetValues(typeof(HudSound)).Length;
            foreach (var entry in Mix)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{entry.clip}.wav");
                if (clip == null) throw new InvalidOperationException("Missing clip " + entry.clip);
                var cue = cues.GetArrayElementAtIndex((int)entry.sound);
                cue.FindPropertyRelative("clip").objectReferenceValue = clip;
                cue.FindPropertyRelative("volume").floatValue = entry.volume;
                cue.FindPropertyRelative("pitchJitter").floatValue = entry.jitter;
                cue.FindPropertyRelative("minInterval").floatValue = entry.interval;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // HudSetup rebuilds the HUD canvas; it calls this to point the existing HUD Audio at the new HudRoot.
        public static void Relink(HudRoot hud)
        {
            var audio = UnityEngine.Object.FindFirstObjectByType<HudAudio>();
            if (audio == null) return;
            var data = new SerializedObject(audio);
            data.FindProperty("hud").objectReferenceValue = hud;
            data.FindProperty("mission").objectReferenceValue = hud.GetComponentInChildren<MissionPanel>(true);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
