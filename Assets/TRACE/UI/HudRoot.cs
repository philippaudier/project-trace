using System;
using System.Collections.Generic;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Encounter;
using TRACE.Tactical;
using UnityEngine;

namespace TRACE.UI
{
    public enum HudState { Exploration, Combat, Focus }

    // Scene-local HUD driver. Reads gameplay state (squad, focus, targeting, encounter, enemies), derives the HUD
    // state and the current target, then ticks every panel. Panels own their presentation; no gameplay logic here.
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class HudRoot : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private SquadController squad;
        [SerializeField] private TacticalFocus focus;
        [SerializeField] private TargetingSystem targeting;
        [SerializeField] private EncounterController encounter;
        [SerializeField] private EnemyBrain[] enemies = Array.Empty<EnemyBrain>();
        [Header("Panels")]
        [SerializeField] private HudPanel[] panels = Array.Empty<HudPanel>();
        [Header("Combat detection")]
        [SerializeField, Min(1f)] private float combatRange = 18f;
        [SerializeField, Min(0f), Tooltip("Seconds of calm before the HUD returns to exploration.")] private float combatLinger = 2.5f;

        private readonly Dictionary<Health, EnemyBrain> brains = new Dictionary<Health, EnemyBrain>();
        private float lastCombatAt = float.NegativeInfinity;
        private SquadMember lastActive;

        public HudState State { get; private set; } = HudState.Exploration;
        public float StateChangedAt { get; private set; }
        public SquadController Squad => squad;
        public TacticalFocus Focus => focus;
        public TargetingSystem Targeting => targeting;
        public EncounterController Encounter => encounter;
        public SquadMember ActiveMember => squad != null ? squad.ActiveMember : null;
        public Health TargetHealth { get; private set; }
        public EnemyBrain TargetBrain { get; private set; }
        public bool IsLocked { get; private set; }
        public float ActiveChangedAt { get; private set; }
        public IReadOnlyList<EnemyBrain> Enemies => enemies;
        public float CombatRange => combatRange;
        public event Action<HudState> StateChanged;

        private void Awake()
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                var health = enemy.GetComponent<Health>();
                if (health != null) brains[health] = enemy;
            }
            StateChangedAt = ActiveChangedAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            var active = ActiveMember;
            if (active != lastActive)
            {
                lastActive = active;
                ActiveChangedAt = Time.unscaledTime;
            }
            UpdateTarget(active);
            if (InCombatNow(active)) lastCombatAt = Time.unscaledTime;
            bool combat = Time.unscaledTime - lastCombatAt < combatLinger;
            var desired = focus != null && focus.IsActive ? HudState.Focus : combat ? HudState.Combat : HudState.Exploration;
            if (desired != State)
            {
                State = desired;
                StateChangedAt = Time.unscaledTime;
                StateChanged?.Invoke(State);
            }
            foreach (var panel in panels)
                if (panel != null) panel.Tick(this);
        }

        // Enemies currently alive, active and within combat range of the active member.
        public int CountHostiles(out int preparing, out int combos)
        {
            int count = 0; preparing = combos = 0;
            var active = ActiveMember;
            if (active == null) return 0;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead) continue;
                if (Vector3.Distance(enemy.transform.position, active.transform.position) > combatRange) continue;
                count++;
                if (enemy.IsPreparingAttack) preparing++;
                var combo = enemy.GetComponent<ComboOpportunity>();
                if (combo != null && combo.Type == ComboOpportunityType.Grouped) combos++;
            }
            return count;
        }

        private bool InCombatNow(SquadMember active)
        {
            if (encounter != null && (encounter.State == EncounterController.EncounterState.Spawning ||
                encounter.State == EncounterController.EncounterState.Fighting ||
                encounter.State == EncounterController.EncounterState.Cleared)) return true;
            if (active == null) return false;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsEngaged && !enemy.IsPreparingAttack) continue;
                var health = enemy.GetComponent<Health>();
                if (health != null && health.IsDead) continue;
                if (Vector3.Distance(enemy.transform.position, active.transform.position) <= combatRange) return true;
            }
            return false;
        }

        // Hard lock first, then the soft target of the active member's attack, then the nearest enemy engaging them.
        private void UpdateTarget(SquadMember active)
        {
            IsLocked = targeting != null && targeting.IsLocked && targeting.LockedTarget != null && !targeting.LockedTarget.IsDead;
            Health target = IsLocked ? targeting.LockedTarget : null;
            if (target == null && active != null)
            {
                var melee = active.GetComponent<PlayerMeleeAttack>();
                var ranged = active.GetComponent<PlayerRangedAttack>();
                target = melee != null && melee.CurrentTarget != null && !melee.CurrentTarget.IsDead ? melee.CurrentTarget :
                    ranged != null && ranged.CurrentTarget != null && !ranged.CurrentTarget.IsDead ? ranged.CurrentTarget : null;
                if (target != null && !target.gameObject.activeInHierarchy) target = null;
            }
            if (target == null && active != null)
            {
                float best = combatRange;
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentTarget != active.Receiver) continue;
                    var health = enemy.GetComponent<Health>();
                    if (health == null || health.IsDead) continue;
                    float distance = Vector3.Distance(enemy.transform.position, active.transform.position);
                    if (distance >= best) continue;
                    best = distance;
                    target = health;
                }
            }
            TargetHealth = target;
            TargetBrain = target != null && brains.TryGetValue(target, out var brain) ? brain : target != null ? target.GetComponent<EnemyBrain>() : null;
        }
    }
}
