using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using UnityEngine;

namespace TRACE.Skills
{
    public sealed class SkillHud : MonoBehaviour
    {
        [SerializeField] private SquadController squad;
        [SerializeField] private CharacterSkill[] skills;
        private Shield[] shields;
        private CharacterProfile[] profiles;
        private readonly string[] roles = { "ASSAULT", "CONTROL", "SUPPORT" };
        private GUIStyle textStyle;
        private TRACE.Tactical.TacticalFocus focusMode;

        private void Start()
        {
            focusMode = squad.GetComponent<TRACE.Tactical.TacticalFocus>();
            shields = new Shield[skills.Length];
            profiles = new CharacterProfile[skills.Length];
            for (int i = 0; i < shields.Length; i++)
            {
                shields[i] = skills[i].GetComponent<Shield>();
                profiles[i] = skills[i].GetComponent<CharacterProfile>();
            }
        }

        // A member with a profile is named and tinted by it; the others keep their prototype role label.
        private string Label(int index) => profiles[index] != null ?
            $"<color=#{profiles[index].AccentHex}>{profiles[index].DisplayName.ToUpperInvariant()}</color>" : roles[index];

        private void OnGUI()
        {
            if (shields == null || (focusMode != null && focusMode.IsActive)) return;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            GUI.Box(new Rect(12, 12, 420, 133), GUIContent.none);
            GUI.Label(new Rect(24, 20, 400, 25), "1 / 2 / 3 : Switch     E : Skill", textStyle);
            for (int i = 0; i < skills.Length; i++)
            {
                var member = squad.Members[i];
                string state = member.Health.IsDead ? "DEAD" : skills[i].IsReady ? "READY" : $"{skills[i].CooldownRemaining:0.0}s";
                string selected = member == squad.ActiveMember ? "> " : "  ";
                GUI.Label(new Rect(24, 48 + i * 29, 400, 28),
                    $"{selected}{i + 1} {Label(i)}  {state}  HP {member.Health.CurrentHealth:0}  SH {shields[i].CurrentAmount:0}", textStyle);
            }
            if (squad.ActiveMember != null)
            {
                var active = squad.ActiveMember.GetComponent<CharacterSkill>();
                GUI.Box(new Rect(12, 149, 420, 30), active.SkillName);
            }
        }
    }
}
