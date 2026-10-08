using System;
using System.Collections.Generic;
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
    // No tree, no choices, no portraits.
    [DefaultExecutionOrder(-7)]
    [DisallowMultipleComponent]
    public sealed class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        private readonly Queue<DialogueLine> queue = new Queue<DialogueLine>();
        private float lineEndsAt;
        private GUIStyle speakerStyle;
        private GUIStyle textStyle;
        private GUIStyle hintStyle;
        public bool IsPlaying { get; private set; }
        public DialogueLine CurrentLine { get; private set; }
        public int ShownCount { get; private set; }
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
            if ((input != null && input.InteractPressed) || Time.unscaledTime >= lineEndsAt) Advance();
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
            IsPlaying = true;
            ShownCount++;
            lineEndsAt = Time.unscaledTime + Mathf.Max(0.5f, CurrentLine.duration);
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
            GUI.Label(new Rect(panel.x + 16f, panel.y + 8f, w - 32f, 22f), CurrentLine.speaker, speakerStyle);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 32f, w - 32f, 56f), CurrentLine.text, textStyle);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 72f, w - 32f, 18f), queue.Count > 0 ? "F / A : continuer" : "", hintStyle);
        }
    }
}
