using System;
using UnityEngine;

namespace TRACE.Voice
{
    // One character's vocalisations: a list of lines, one per category, each with its clip variants and tuning.
    // Clips are assigned here (by the setup from the naming convention, or by hand); runtime never reads file names.
    // An empty or missing category is silence, never an error.
    [CreateAssetMenu(fileName = "VoiceSet", menuName = "TRACE/Character Voice Set")]
    public sealed class CharacterVoiceSet : ScriptableObject
    {
        [Serializable]
        public sealed class Line
        {
            public VoiceCategory category;
            public AudioClip[] clips = new AudioClip[0];
            [Range(0f, 1f)] public float volume = 0.9f;
            [Range(0f, 1f), Tooltip("Chance a trigger actually speaks; silence is a valid answer.")] public float probability = 1f;
            [Min(0f), Tooltip("Seconds (real time) before this category may speak again.")] public float cooldown = 1f;
            public int ClipCount
            {
                get { int n = 0; if (clips != null) foreach (var c in clips) if (c != null) n++; return n; }
            }
        }

        [SerializeField] private string characterId = "";
        [SerializeField, Tooltip("File prefix of this character's recordings, e.g. TW, Control, Support.")] private string filePrefix = "";
        [SerializeField, Range(0f, 0.02f), Tooltip("Human voice: at most ±1 %, never cartoon.")] private float pitchJitter = 0.01f;
        [SerializeField] private Line[] lines = new Line[0];

        public string CharacterId => characterId;
        public string FilePrefix => filePrefix;
        public float PitchJitter => pitchJitter;
        public Line[] Lines => lines;

        public Line Get(VoiceCategory category)
        {
            if (lines == null) return null;
            foreach (var line in lines) if (line != null && line.category == category) return line;
            return null;
        }

        public int ClipCount(VoiceCategory category)
        {
            var line = Get(category);
            return line != null ? line.ClipCount : 0;
        }
    }
}
