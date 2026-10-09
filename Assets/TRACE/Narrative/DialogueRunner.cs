using System;
using System.Collections.Generic;
using TRACE.Characters;
using TRACE.Input;
using UnityEngine;

namespace TRACE.Narrative
{
    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea] public string text;
        [Min(0.5f)] public float duration;
        public DialogueLine(string speaker, string text, float duration) { this.speaker = speaker; this.text = text; this.duration = duration; }
    }

    // Short linear exchanges: one line at a time, auto-advance on a real-time clock, Interact skips ahead.
    // No tree, no choices. A speaker matching a squad profile gets its mini portrait and identity colour.
    [DefaultExecutionOrder(-7)]
    [DisallowMultipleComponent]
    public sealed class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField, Tooltip("Profiles a speaker resolves to, by display name or first archetype word (ASSAULT).")]
        private CharacterProfile[] speakers = Array.Empty<CharacterProfile>();
        [SerializeField, Tooltip("Shown for a squad speaker whose profile has no portrait.")] private Sprite fallbackPortrait;
        private static readonly Color NeutralSpeaker = new Color(0.62f, 0.65f, 0.69f);
        private readonly Queue<DialogueLine> queue = new Queue<DialogueLine>();
        private float lineEndsAt;
        private GUIStyle speakerStyle;
        private GUIStyle textStyle;
        private GUIStyle hintStyle;
        public bool IsPlaying { get; private set; }
        public DialogueLine CurrentLine { get; private set; }
        public CharacterProfile CurrentSpeaker { get; private set; }
        // Portrait drawn for the current line: the speaker's, the fallback for a portrait-less profile, none otherwise.
        public Sprite CurrentPortrait => !IsPlaying || CurrentSpeaker == null ? null : CurrentSpeaker.Portrait != null ? CurrentSpeaker.Portrait : fallbackPortrait;
        public int ShownCount { get; private set; }
        // Lines the player advanced with Interact (presentation feedback reads it).
        public int ManualAdvanceCount { get; private set; }
        public int QueuedCount => queue.Count;
        public event Action Finished;

        public void Play(IEnumerable<DialogueLine> lines)
        {
            foreach (var line in lines) queue.Enqueue(line);
            if (!IsPlaying) Advance();
        }

        public void Skip()
        {
            if (IsPlaying) Advance();
        }

        private void Update()
        {
            if (!IsPlaying) return;
            bool manual = input != null && input.InteractPressed;
            if (manual) ManualAdvanceCount++;
            if (manual || Time.unscaledTime >= lineEndsAt) Advance();
        }

        private void Advance()
        {
            if (queue.Count == 0)
            {
                bool wasPlaying = IsPlaying;
                IsPlaying = false;
                if (wasPlaying) Finished?.Invoke();
                return;
            }
            CurrentLine = queue.Dequeue();
            CurrentSpeaker = Resolve(CurrentLine.speaker);
            IsPlaying = true;
            ShownCount++;
            lineEndsAt = Time.unscaledTime + Mathf.Max(0.5f, CurrentLine.duration);
        }

        public CharacterProfile Resolve(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            foreach (var profile in speakers)
            {
                if (profile == null) continue;
                if (string.Equals(profile.DisplayName, speaker, StringComparison.OrdinalIgnoreCase)) return profile;
                string role = profile.Archetype.Split(' ')[0];
                if (role.Length > 0 && string.Equals(role, speaker, StringComparison.OrdinalIgnoreCase)) return profile;
            }
            return null;
        }

        private void OnGUI()
        {
            if (!IsPlaying) return;
            if (textStyle == null)
            {
                speakerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.LowerRight };
            }
            float w = Mathf.Min(760f, Screen.width - 40f);
            var panel = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 128f, w, 96f);
            GUI.Box(panel, GUIContent.none);
            var speaker = CurrentSpeaker;
            Color accent = speaker != null ? speaker.AccentColor : NeutralSpeaker;
            var oldColor = GUI.color;
            GUI.color = accent;
            GUI.DrawTexture(new Rect(panel.x, panel.y + 6f, 3f, panel.height - 12f), Texture2D.whiteTexture);
            float textX = panel.x + 16f;
            var portrait = CurrentPortrait;
            if (portrait != null)
            {
                var frame = new Rect(panel.x + 12f, panel.y + 11f, 74f, 74f);
                GUI.DrawTexture(frame, Texture2D.whiteTexture);
                GUI.color = Color.white;
                var texture = portrait.texture;
                var source = portrait.textureRect;
                GUI.DrawTextureWithTexCoords(new Rect(frame.x + 1f, frame.y + 1f, 72f, 72f), texture,
                    new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
                textX = frame.xMax + 14f;
            }
            GUI.color = oldColor;
            float textW = panel.xMax - 16f - textX;
            string name = speaker != null ? speaker.DisplayName.ToUpperInvariant() : CurrentLine.speaker;
            speakerStyle.normal.textColor = accent;
            GUI.Label(new Rect(textX, panel.y + 8f, textW, 22f), name, speakerStyle);
            GUI.Label(new Rect(textX, panel.y + 32f, textW, 56f), CurrentLine.text, textStyle);
            GUI.Label(new Rect(textX, panel.y + 72f, textW, 18f), queue.Count > 0 ? "F / A : continuer" : "", hintStyle);
        }
    }
}
