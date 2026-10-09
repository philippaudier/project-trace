using UnityEngine;

namespace TRACE.Voice
{
    public enum VoiceCategory
    {
        AttackLight, AttackHeavy, Dodge, HurtLight, HurtHeavy, LowHealth, SkillPrimary, SwitchIn, SwitchOut,
        CombatStart, AllyDown, ComboReady, TacticalFocusEnter, TacticalFocusExit, Contextual
    }

    public enum VoicePriority { Low = 0, Medium = 1, High = 2, Critical = 3 }

    // Fixed policy of the V0.1 voice layer: priority per category, and the default cooldown (seconds, real time)
    // and probability a new line starts with. Sets can override cooldown and probability per line; priority is
    // not editable so the mix stays predictable. Silence is a valid outcome everywhere.
    public static class VoicePolicy
    {
        public static VoicePriority PriorityOf(VoiceCategory category)
        {
            switch (category)
            {
                case VoiceCategory.AllyDown:
                case VoiceCategory.HurtHeavy: return VoicePriority.Critical;
                case VoiceCategory.LowHealth:
                case VoiceCategory.SkillPrimary: return VoicePriority.High;
                case VoiceCategory.SwitchIn:
                case VoiceCategory.SwitchOut:
                case VoiceCategory.CombatStart:
                case VoiceCategory.ComboReady: return VoicePriority.Medium;
                default: return VoicePriority.Low;
            }
        }

        public static float DefaultCooldown(VoiceCategory category)
        {
            switch (category)
            {
                case VoiceCategory.AttackLight: return 0.35f;
                case VoiceCategory.AttackHeavy: return 0.5f;
                case VoiceCategory.Dodge: return 0.8f;
                case VoiceCategory.HurtLight: return 0.6f;
                case VoiceCategory.HurtHeavy: return 1.2f;
                case VoiceCategory.LowHealth: return 12f;
                case VoiceCategory.SkillPrimary: return 1.5f;
                case VoiceCategory.SwitchIn: return 1.5f;
                case VoiceCategory.SwitchOut: return 1.5f;
                case VoiceCategory.CombatStart: return 6f;
                case VoiceCategory.AllyDown: return 2f;
                case VoiceCategory.ComboReady: return 4f;
                case VoiceCategory.TacticalFocusEnter: return 6f;
                case VoiceCategory.TacticalFocusExit: return 6f;
                default: return 3f;
            }
        }

        public static float DefaultProbability(VoiceCategory category)
        {
            switch (category)
            {
                case VoiceCategory.AttackLight: return 0.55f;
                case VoiceCategory.AttackHeavy: return 0.8f;
                case VoiceCategory.Dodge: return 0.5f;
                case VoiceCategory.HurtLight: return 0.85f;
                case VoiceCategory.HurtHeavy: return 1f;
                case VoiceCategory.LowHealth: return 1f;
                case VoiceCategory.SkillPrimary: return 0.8f;
                case VoiceCategory.SwitchIn: return 0.55f;
                case VoiceCategory.SwitchOut: return 0.2f;
                case VoiceCategory.CombatStart: return 0.4f;
                case VoiceCategory.AllyDown: return 1f;
                case VoiceCategory.ComboReady: return 0.5f;
                case VoiceCategory.TacticalFocusEnter: return 0.25f;
                case VoiceCategory.TacticalFocusExit: return 0.15f;
                default: return 0.5f;
            }
        }
    }
}
