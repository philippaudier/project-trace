using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Skills;
using TRACE.Tactical;
using TRACE.UI;
using TRACE.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static TRACE.Editor.SquadVisualKit;

namespace TRACE.Editor
{
    // Voice V0.1: import rules for the voice folder, one CharacterVoiceSet per member filled from the file naming
    // convention (real recordings in the character folder win over TMP_ placeholders in Temp), a dedicated
    // VoiceAudioSource and CharacterVoice on every member, SquadVoiceDirector and the F9 debug panel on the squad.
    // Menu TRACE/Apply Voice V0.1, idempotent. Runtime never depends on file names.
    public static class VoiceSetup
    {
        public const string VoiceFolder = "Assets/TRACE/Audio/Voice";
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
            "Assets/TRACE/Scenes/FieldTest.unity",
        };
        // Member index: folder, file prefix, character id.
        // Real recordings: VO_<folder>_ or VO_<prefix>_; placeholders: TMP_VO_<prefix>_.
        private static readonly (string folder, string prefix, string id)[] Characters =
        {
            ("Tracewalker", "TW", "tracewalker_player"),
            ("Control", "Control", "origin_control_01"),
            ("Support", "Support", "origin_support_01"),
        };
        private static readonly Dictionary<string, VoiceCategory> Aliases = new Dictionary<string, VoiceCategory>(StringComparer.OrdinalIgnoreCase)
        {
            { "Skill", VoiceCategory.SkillPrimary }, { "DashStrike", VoiceCategory.SkillPrimary }, { "GravityField", VoiceCategory.SkillPrimary },
            { "PulseShield", VoiceCategory.SkillPrimary }, { "Focus", VoiceCategory.TacticalFocusEnter }, { "FocusEnter", VoiceCategory.TacticalFocusEnter },
            { "TacticalFocus", VoiceCategory.TacticalFocusEnter },
            { "FocusExit", VoiceCategory.TacticalFocusExit }, { "Hurt", VoiceCategory.HurtLight }, { "Attack", VoiceCategory.AttackLight },
        };

        [MenuItem("TRACE/Apply Voice V0.1")]
        public static void Apply() => ApplyTo(Scenes);

        public static void ApplyTo(params string[] paths)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string folder in new[] { VoiceFolder, VoiceFolder + "/Tracewalker", VoiceFolder + "/Control", VoiceFolder + "/Support", VoiceFolder + "/Temp" })
                EnsureFolder(folder);
            ConfigureImports();
            var mixer = TacticalFocusPresentationSetup.BuildMixer();
            var voiceGroup = TacticalFocusPresentationSetup.MixerGroup("Voice");
            if (voiceGroup == null) throw new InvalidOperationException("Mixer has no Voice group.");
            foreach (var c in Characters) BuildSet(c.folder, c.prefix, c.id);
            AssetDatabase.SaveAssets();
            int applied = 0;
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                // Reload by path per scene: opening a scene reimports fresh assets and would orphan held references.
                var sets = Characters.Select(c => AssetDatabase.LoadAssetAtPath<CharacterVoiceSet>(SetPath(c.folder))).ToArray();
                if (sets.Any(set => set == null)) throw new InvalidOperationException("A voice set failed to load.");
                Build(scene, sets, voiceGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE Voice V0.1 applied to {applied} scene(s).");
        }

        // Short mono one-shots: decompressed on load, ADPCM, preloaded, never streamed.
        internal static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
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

        // VO_<Prefix>_<Category>_NN (character folder) wins; TMP_VO_<Prefix>_<Category>_NN (Temp) fills the rest.
        private static string SetPath(string folder) => $"{VoiceFolder}/{folder}/VoiceSet_{folder}.asset";

        private static CharacterVoiceSet BuildSet(string folder, string prefix, string id)
        {
            string path = SetPath(folder);
            var set = AssetDatabase.LoadAssetAtPath<CharacterVoiceSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CharacterVoiceSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            var real = Collect($"{VoiceFolder}/{folder}", "VO_" + folder + "_", "VO_" + prefix + "_");
            var temp = Collect($"{VoiceFolder}/Temp", "TMP_VO_" + prefix + "_");
            var data = new SerializedObject(set);
            data.FindProperty("characterId").stringValue = id;
            data.FindProperty("filePrefix").stringValue = prefix;
            var categories = (VoiceCategory[])Enum.GetValues(typeof(VoiceCategory));
            var lines = data.FindProperty("lines");
            // Keep hand-tuned volume / probability / cooldown of existing lines; only clips are refreshed.
            var existing = new Dictionary<VoiceCategory, (float volume, float probability, float cooldown)>();
            for (int i = 0; i < lines.arraySize; i++)
            {
                var line = lines.GetArrayElementAtIndex(i);
                existing[(VoiceCategory)line.FindPropertyRelative("category").enumValueIndex] =
                    (line.FindPropertyRelative("volume").floatValue, line.FindPropertyRelative("probability").floatValue, line.FindPropertyRelative("cooldown").floatValue);
            }
            lines.arraySize = categories.Length;
            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var line = lines.GetArrayElementAtIndex(i);
                line.FindPropertyRelative("category").enumValueIndex = (int)category;
                var clips = real.TryGetValue(category, out var r) && r.Count > 0 ? r : temp.TryGetValue(category, out var t) ? t : new List<AudioClip>();
                var array = line.FindPropertyRelative("clips");
                array.arraySize = clips.Count;
                for (int c = 0; c < clips.Count; c++) array.GetArrayElementAtIndex(c).objectReferenceValue = clips[c];
                bool keep = existing.TryGetValue(category, out var tuning);
                line.FindPropertyRelative("volume").floatValue = keep ? tuning.volume : 0.9f;
                line.FindPropertyRelative("probability").floatValue = keep ? tuning.probability : VoicePolicy.DefaultProbability(category);
                line.FindPropertyRelative("cooldown").floatValue = keep ? tuning.cooldown : VoicePolicy.DefaultCooldown(category);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            return set;
        }

        private static Dictionary<VoiceCategory, List<AudioClip>> Collect(string folder, params string[] prefixes)
        {
            var result = new Dictionary<VoiceCategory, List<AudioClip>>();
            if (!AssetDatabase.IsValidFolder(folder)) return result;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                string prefix = prefixes.FirstOrDefault(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                if (prefix == null) continue;
                var category = CategoryOf(name.Substring(prefix.Length));
                if (category == null) { Debug.LogWarning($"Voice clip {name} matches no category; skipped."); continue; }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (!result.TryGetValue(category.Value, out var list)) result[category.Value] = list = new List<AudioClip>();
                list.Add(clip);
            }
            foreach (var list in result.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        // "<Category>_NN", "Skill_<Name>_NN", "<Alias>_NN".
        private static VoiceCategory? CategoryOf(string rest)
        {
            string[] parts = rest.Split('_');
            if (parts.Length == 0) return null;
            if (Enum.TryParse(parts[0], true, out VoiceCategory category)) return category;
            if (Aliases.TryGetValue(parts[0], out category)) return category;
            return null;
        }

        private static void Build(UnityEngine.SceneManagement.Scene scene, CharacterVoiceSet[] sets, UnityEngine.Audio.AudioMixerGroup voiceGroup)
        {
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            if (squad == null) throw new InvalidOperationException(scene.path + " has no squad.");
            var roster = new SerializedObject(squad).FindProperty("members");
            for (int i = 0; i < roster.arraySize && i < sets.Length; i++)
            {
                var member = (SquadMember)roster.GetArrayElementAtIndex(i).objectReferenceValue;
                var go = member.gameObject;
                var existing = go.transform.Find("VoiceAudioSource");
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                var sourceObject = new GameObject("VoiceAudioSource");
                sourceObject.layer = go.layer;
                sourceObject.transform.SetParent(go.transform, false);
                sourceObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                var source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false;
                // Light 3D: a voice sits on its body but the controlled member always stays intelligible.
                source.spatialBlend = 0.6f; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f; source.maxDistance = 28f; source.dopplerLevel = 0f; source.spread = 30f;
                source.priority = 20; source.reverbZoneMix = 0.3f;
                source.outputAudioMixerGroup = voiceGroup;
                var voice = go.GetComponent<CharacterVoice>();
                if (voice == null) voice = go.AddComponent<CharacterVoice>();
                Reference(voice, "voiceSet", sets[i]);
                Reference(voice, "source", source);
                Reference(voice, "health", go.GetComponent<Health>());
                Reference(voice, "motor", go.GetComponent<ThirdPersonMotor>());
                Reference(voice, "melee", go.GetComponent<PlayerMeleeAttack>());
                Reference(voice, "skill", go.GetComponent<CharacterSkill>());
            }
            var director = squad.GetComponent<SquadVoiceDirector>();
            if (director == null) director = squad.gameObject.AddComponent<SquadVoiceDirector>();
            Reference(director, "squad", squad);
            Reference(director, "hud", UnityEngine.Object.FindFirstObjectByType<HudRoot>());
            Reference(director, "focus", squad.GetComponent<TacticalFocus>());
            var panel = squad.GetComponent<VoiceDebugPanel>();
            if (panel == null) panel = squad.gameObject.AddComponent<VoiceDebugPanel>();
            Reference(panel, "squad", squad);
            Reference(panel, "director", director);
        }
    }
}
