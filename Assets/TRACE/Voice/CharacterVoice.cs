using System;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Skills;
using UnityEngine;

namespace TRACE.Voice
{
    // One member's voice. Owns the dedicated AudioSource, applies the policy (priority, cooldown, probability,
    // no immediate repeat, tiny pitch jitter) and reacts to its own events: attacks, dodges, hits, low health,
    // primary skill. Squad-level events (switch, combat start, ally down, Tactical Focus) come from
    // SquadVoiceDirector through Speak. Purely presentational; no gameplay state is written.
    [DefaultExecutionOrder(48)]
    [DisallowMultipleComponent]
    public sealed class CharacterVoice : MonoBehaviour
    {
        [SerializeField] private CharacterVoiceSet voiceSet;
        [SerializeField] private AudioSource source;
        [SerializeField] private Health health;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private PlayerMeleeAttack melee;
        [SerializeField] private CharacterSkill skill;
        [SerializeField, Range(0f, 1f), Tooltip("Damage at or above this fraction of max health is a heavy hit.")] private float heavyHitFraction = 0.2f;
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
        [SerializeField, Range(0f, 0.3f), Tooltip("Low health re-arms once health climbs this far above the threshold.")] private float lowHealthHysteresis = 0.05f;

        private float[] lastPlayedAt;
        private int[] lastIndex;
        private int[] counts;
        private VoicePriority playingPriority;
        private float playingUntil;
        private bool wasAttacking, wasDodging, lowHealthArmed = true;

        public CharacterVoiceSet VoiceSet => voiceSet;
        public AudioSource Source => source;
        public bool IsSpeaking => source != null && source.isPlaying && Time.unscaledTime < playingUntil;
        public VoicePriority CurrentPriority => IsSpeaking ? playingPriority : VoicePriority.Low;
        public VoiceCategory? LastCategory { get; private set; }
        public AudioClip LastClip { get; private set; }
        public int PlayCount { get; private set; }
        public int Count(VoiceCategory category) => counts != null ? counts[(int)category] : 0;
        // Tests and the debug panel: every eligible trigger speaks, cooldowns aside.
        public bool ForceProbability { get; set; }
        public bool LowHealthArmed => lowHealthArmed;

        private void Awake()
        {
            int length = Enum.GetValues(typeof(VoiceCategory)).Length;
            lastPlayedAt = new float[length];
            for (int i = 0; i < length; i++) lastPlayedAt[i] = float.NegativeInfinity;
            lastIndex = new int[length];
            for (int i = 0; i < length; i++) lastIndex[i] = -1;
            counts = new int[length];
            if (source != null) { source.playOnAwake = false; source.loop = false; }
        }

        private void OnEnable()
        {
            if (health != null) { health.OnDamaged += Damaged; health.OnDeath += Died; }
            if (skill != null) skill.Activated += SkillUsed;
        }

        private void OnDisable()
        {
            if (health != null) { health.OnDamaged -= Damaged; health.OnDeath -= Died; }
            if (skill != null) skill.Activated -= SkillUsed;
        }

        private void Update()
        {
            if (health != null && health.IsDead) return;
            bool attacking = melee != null && melee.isActiveAndEnabled && melee.IsAttacking;
            if (attacking && !wasAttacking) Speak(VoiceCategory.AttackLight);
            wasAttacking = attacking;
            bool dodging = motor != null && motor.isActiveAndEnabled && motor.IsDodging;
            if (dodging && !wasDodging) Speak(VoiceCategory.Dodge);
            wasDodging = dodging;
        }

        private void Damaged(float amount)
        {
            if (health == null || health.IsDead) return;
            // One hurt line per hit: heavy wins, and a heavy hit (critical) may interrupt anything lighter.
            bool heavy = health.MaxHealth > 0f && amount >= health.MaxHealth * heavyHitFraction;
            Speak(heavy ? VoiceCategory.HurtHeavy : VoiceCategory.HurtLight);
            float ratio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 1f;
            if (lowHealthArmed && ratio < lowHealthThreshold && ratio > 0f)
            {
                lowHealthArmed = false;
                Speak(VoiceCategory.LowHealth);
            }
        }

        private void LateUpdate()
        {
            if (health == null || lowHealthArmed || health.IsDead) return;
            float ratio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 1f;
            if (ratio >= lowHealthThreshold + lowHealthHysteresis) lowHealthArmed = true;
        }

        private void SkillUsed() => Speak(VoiceCategory.SkillPrimary);

        private void Died()
        {
            if (source != null) source.Stop();
            playingUntil = 0f;
        }

        // Returns true when a clip actually starts. ignoreCooldown is for the debug panel only.
        public bool Speak(VoiceCategory category, bool ignoreCooldown = false)
        {
            if (voiceSet == null || source == null || !isActiveAndEnabled) return false;
            if (health != null && health.IsDead && category != VoiceCategory.AllyDown) return false;
            var line = voiceSet.Get(category);
            if (line == null || line.ClipCount == 0) return false;
            int index = (int)category;
            float now = Time.unscaledTime;
            if (!ignoreCooldown && now - lastPlayedAt[index] < line.cooldown) return false;
            var priority = VoicePolicy.PriorityOf(category);
            if (IsSpeaking && priority <= playingPriority) return false;
            if (!ForceProbability && !ignoreCooldown && UnityEngine.Random.value > line.probability)
            {
                // A declined roll still counts as the moment this category was considered: no re-roll spam.
                lastPlayedAt[index] = now;
                return false;
            }
            var clip = Pick(line, index);
            if (clip == null) return false;
            if (source.isPlaying) source.Stop();
            source.clip = clip;
            source.volume = line.volume;
            source.pitch = 1f + UnityEngine.Random.Range(-voiceSet.PitchJitter, voiceSet.PitchJitter);
            source.Play();
            playingPriority = priority;
            playingUntil = now + clip.length / Mathf.Max(0.5f, source.pitch) + 0.05f;
            lastPlayedAt[index] = now;
            counts[index]++;
            PlayCount++;
            LastCategory = category;
            LastClip = clip;
            return true;
        }

        // Random variant that is never the one played last for this category (when more than one exists).
        private AudioClip Pick(CharacterVoiceSet.Line line, int categoryIndex)
        {
            var clips = line.clips;
            int valid = line.ClipCount;
            if (valid == 1) { foreach (var c in clips) if (c != null) { lastIndex[categoryIndex] = Array.IndexOf(clips, c); return c; } }
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int i = UnityEngine.Random.Range(0, clips.Length);
                if (clips[i] == null || i == lastIndex[categoryIndex]) continue;
                lastIndex[categoryIndex] = i;
                return clips[i];
            }
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && i != lastIndex[categoryIndex]) { lastIndex[categoryIndex] = i; return clips[i]; }
            return null;
        }
    }
}
