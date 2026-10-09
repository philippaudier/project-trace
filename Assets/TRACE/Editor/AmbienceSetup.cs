using System;
using System.Linq;
using TRACE.AI;
using TRACE.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TRACE.Editor
{
    // TRACE Ambience V0.1. FirstTrace: four layers on the existing AmbienceController (base, environment, rare spatial
    // details, Trace anomaly) and a handful of placed 3D loops where the level has a reason to make noise. Prototype:
    // one neutral loop and one distant machine, nothing more. Configures the import of every AMB_ clip. Idempotent:
    // emitters and detail points are rebuilt, layer clips and volumes re-applied.
    public static class AmbienceSetup
    {
        private const string Folder = "Assets/TRACE/Audio/Ambience/";
        private const string FirstTrace = "Assets/TRACE/Scenes/FirstTrace.unity";
        private const string Prototype = "Assets/TRACE/Scenes/Prototype.unity";

        // Placed loops: name, clip, position, volume, max distance. Dock machinery, two powered terminals, pipes in
        // the clue corridor and the tension corridor, the hall's side-room panel, arena machinery. The hall centre
        // and the dock entrance are left without emitters: places to hear the space.
        private static readonly (string name, string clip, Vector3 position, float volume, float range)[] Emitters =
        {
            ("Dock Machinery", "AMB_Emitter_Machinery_LOOP", new Vector3(-9f, 2f, 4f), 0.5f, 13f),
            ("Corridor Pipe", "AMB_Emitter_Pipeline_LOOP", new Vector3(3.5f, 3f, 20f), 0.45f, 10f),
            ("Terminal A", "AMB_Emitter_Terminal_LOOP", new Vector3(2.6f, 1.2f, 28.5f), 0.35f, 6f),
            ("Hall Side Panel", "AMB_Emitter_Machinery_LOOP", new Vector3(-18.5f, 2f, 52f), 0.4f, 11f),
            ("Tension Pipe", "AMB_Emitter_Pipeline_LOOP", new Vector3(-3.5f, 3.5f, 67f), 0.45f, 10f),
            ("Terminal B", "AMB_Emitter_Terminal_LOOP", new Vector3(-14.3f, 1.2f, 78f), 0.35f, 6f),
            ("Arena Machinery", "AMB_Emitter_Machinery_LOOP", new Vector3(12f, 3f, 92f), 0.45f, 14f),
        };
        // Where rare details may come from: high in the dock, corridor ceiling, the hall's far walls, the tension
        // corridor, the arena's edges. Never at the player's feet.
        private static readonly Vector3[] DetailPoints =
        {
            new Vector3(-8f, 4.5f, 13f), new Vector3(4f, 3.5f, 23f), new Vector3(-12f, 6f, 47f), new Vector3(12f, 6f, 57f),
            new Vector3(0f, 4.5f, 70f), new Vector3(-13f, 5.5f, 92f), new Vector3(13f, 5.5f, 82f),
        };

        [MenuItem("TRACE/Apply Ambience V0.1")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigureImports();
            BuildFirstTrace();
            BuildPrototype();
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE Ambience V0.1 applied to FirstTrace and Prototype.");
        }

        // Long stereo beds stream (never fully in memory), placed loops stay compressed in memory, short details
        // decompress on load. The Tactical Focus float bed keeps its stereo width.
        private static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/TRACE/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("AMB_") || !(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                bool bed = file.EndsWith("_LOOP") && !file.StartsWith("AMB_Emitter_");
                bool emitter = file.StartsWith("AMB_Emitter_");
                importer.forceToMono = !bed;
                importer.loadInBackground = bed;
                var settings = importer.defaultSampleSettings;
                settings.loadType = bed ? AudioClipLoadType.Streaming : emitter ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = bed || emitter ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                settings.quality = 0.5f;
                settings.preloadAudioData = !bed;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static void BuildFirstTrace()
        {
            var scene = EditorSceneManager.OpenScene(FirstTrace);
            var ambience = Object.FindFirstObjectByType<AmbienceController>();
            if (ambience == null) throw new InvalidOperationException("FirstTrace has no AmbienceController.");
            var data = new SerializedObject(ambience);
            Clip(data, "baseLoop", "AMB_FirstTrace_Base_LOOP.ogg"); Number(data, "baseVolume", 0.3f);
            Clip(data, "environmentLoop", "AMB_FirstTrace_Environment_LOOP.ogg"); Number(data, "environmentVolume", 0.22f);
            Clip(data, "anomalyLoop", "AMB_TracePresence_LOOP.ogg"); Number(data, "anomalyVolume", 0.3f);
            Number(data, "layerFade", 1.5f); Number(data, "anomalyFade", 2.5f); Number(data, "tensionBoost", 1.35f); Number(data, "conclusionLevel", 0.45f);
            var details = data.FindProperty("detailClips");
            string[] detailFiles = { "AMB_Detail_MetalImpact_01.wav", "AMB_Detail_MetalImpact_02.wav", "AMB_Detail_Relay_01.wav" };
            details.arraySize = detailFiles.Length;
            for (int i = 0; i < detailFiles.Length; i++) details.GetArrayElementAtIndex(i).objectReferenceValue = Load(detailFiles[i]);
            data.FindProperty("detailInterval").vector2Value = new Vector2(8f, 30f);
            Number(data, "silenceChance", 0.3f); Number(data, "detailVolume", 0.4f); Number(data, "detailPitchJitter", 0.06f);
            Number(data, "detailMinDistance", 3f); Number(data, "detailMaxDistance", 22f);
            Groups(data);

            var root = Fresh(scene, "Ambience Emitters");
            foreach (var e in Emitters) Emitter(root, e.name, e.clip, e.position, e.volume, e.range);
            var points = Fresh(scene, "Ambience Detail Points");
            var pointArray = data.FindProperty("detailPoints");
            pointArray.arraySize = DetailPoints.Length;
            for (int i = 0; i < DetailPoints.Length; i++)
            {
                var point = new GameObject("Detail Point " + (i + 1)).transform;
                point.SetParent(points, false);
                point.position = DetailPoints[i];
                pointArray.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Test scene: one neutral loop, one distant machine; no environment layer, no details, no anomaly.
        private static void BuildPrototype()
        {
            var scene = EditorSceneManager.OpenScene(Prototype);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Ambience" || root.name == "Ambience Emitters") Object.DestroyImmediate(root);
            var ambience = new GameObject("Ambience").AddComponent<AmbienceController>();
            var data = new SerializedObject(ambience);
            Clip(data, "baseLoop", "AMB_FirstTrace_Base_LOOP.ogg"); Number(data, "baseVolume", 0.22f);
            Number(data, "environmentVolume", 0f);
            Groups(data);
            data.ApplyModifiedPropertiesWithoutUndo();
            var squad = Object.FindFirstObjectByType<SquadController>();
            var anchor = squad != null && squad.Members.Count > 0 ? squad.Members[0].transform.position : Vector3.zero;
            var emitters = new GameObject("Ambience Emitters").transform;
            Emitter(emitters, "Distant Machinery", "AMB_Emitter_Machinery_LOOP", anchor + new Vector3(14f, 3f, 16f), 0.35f, 22f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void Emitter(Transform parent, string name, string clip, Vector3 position, float volume, float range)
        {
            var go = new GameObject("Emitter " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = Load(clip + ".ogg");
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = range;
            source.dopplerLevel = 0f;
            source.spread = 60f;
            source.priority = 160;
            source.outputAudioMixerGroup = TacticalFocusPresentationSetup.MixerGroup("Ambience");
            var emitter = go.AddComponent<AmbienceEmitter>();
            var data = new SerializedObject(emitter);
            data.FindProperty("volume").floatValue = volume;
            data.FindProperty("fadeIn").floatValue = 1.5f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Fresh(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) Object.DestroyImmediate(root);
            return new GameObject(name).transform;
        }

        private static void Groups(SerializedObject data)
        {
            data.FindProperty("ambienceGroup").objectReferenceValue = TacticalFocusPresentationSetup.MixerGroup("Ambience");
            data.FindProperty("musicGroup").objectReferenceValue = TacticalFocusPresentationSetup.MixerGroup("Music");
        }

        private static AudioClip Load(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + file);
            if (clip == null) throw new InvalidOperationException("Missing ambience clip " + file);
            return clip;
        }

        private static void Clip(SerializedObject data, string field, string file) => data.FindProperty(field).objectReferenceValue = Load(file);
        private static void Number(SerializedObject data, string field, float value) => data.FindProperty(field).floatValue = value;
    }
}
