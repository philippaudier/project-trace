using System;
using TRACE.AI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TRACE.Voice
{
    // Prototype IMGUI panel (F9) to audition the active member's voice categories: one button per category,
    // probability forced, cooldown ignored. Not a final UI.
    public sealed class VoiceDebugPanel : MonoBehaviour
    {
        [SerializeField] private SquadController squad;
        [SerializeField] private SquadVoiceDirector director;
        [SerializeField] private Key toggleKey = Key.F9;
        [SerializeField] private bool visible;
        private GUIStyle title;
        public bool Visible => visible;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || squad == null || squad.ActiveMember == null) return;
            if (title == null) title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            var voice = director != null ? director.VoiceOf(squad.ActiveMember) : squad.ActiveMember.GetComponent<CharacterVoice>();
            var categories = (VoiceCategory[])Enum.GetValues(typeof(VoiceCategory));
            float height = 60f + categories.Length * 26f;
            GUI.Box(new Rect(Screen.width - 250f, 80f, 238f, height), GUIContent.none);
            GUI.Label(new Rect(Screen.width - 240f, 86f, 220f, 22f), "VOICE DEBUG  (F9)  " + squad.ActiveMember.name, title);
            string status = voice == null ? "no CharacterVoice" : voice.VoiceSet == null ? "no voice set" :
                voice.IsSpeaking ? $"speaking {voice.LastCategory} ({voice.CurrentPriority})" : $"idle, {voice.PlayCount} lines";
            GUI.Label(new Rect(Screen.width - 240f, 106f, 220f, 20f), status);
            for (int i = 0; i < categories.Length; i++)
            {
                int clips = voice != null && voice.VoiceSet != null ? voice.VoiceSet.ClipCount(categories[i]) : 0;
                GUI.enabled = voice != null && clips > 0;
                if (GUI.Button(new Rect(Screen.width - 240f, 130f + i * 26f, 220f, 24f), $"{categories[i]}  [{clips}]"))
                    voice.Speak(categories[i], ignoreCooldown: true);
            }
            GUI.enabled = true;
        }
    }
}
