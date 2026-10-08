using System;
using System.Collections.Generic;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Tactical;
using UnityEngine;
using TRACE.Input;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace TRACE.Encounter
{
    [Serializable]
    public sealed class EncounterWave
    {
        public string name = "Wave";
        public EnemyBrain[] enemies = Array.Empty<EnemyBrain>();
    }

    // Light sequencer for one structured fight: pre-placed enemies, three waves, victory/defeat, restart.
    // Deliberately not a mission or objective framework.
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class EncounterController : MonoBehaviour
    {
        public enum EncounterState { Pending, Spawning, Fighting, Cleared, Complete, Defeated }

        [SerializeField] private SquadController squad;
        [SerializeField] private TacticalFocus focus;
        [SerializeField] private EncounterWave[] waves = Array.Empty<EncounterWave>();
        [SerializeField] private Transform[] spawnMarkers = Array.Empty<Transform>();
        [SerializeField, Tooltip("Start on its own after Start Delay; otherwise wait for Begin().")]
        private bool autoStart = true;
        [SerializeField, Min(0f)] private float startDelay = 2f;
        [SerializeField, Min(0f)] private float spawnWarning = 1f;
        [SerializeField, Min(0f)] private float interWaveDelay = 3f;
        [SerializeField, Min(0f), Tooltip("HP restored to each living member when a wave is cleared.")]
        private float interWaveHeal = 20f;
        private readonly List<ComboOpportunity> opportunities = new List<ComboOpportunity>();
        private readonly List<Health> trackedMembers = new List<Health>();
        private TracePlayerInput input;
        private float stateEnteredAt;
        private float startedAt;
        private float completedAt;
        private SquadMember lastActive;
        private bool focusWasActive;
        private bool begun;
        private int comboBaseline;
        public EncounterState State { get; private set; } = EncounterState.Pending;
        public int WaveIndex { get; private set; } = -1;
        public int WaveCount => waves.Length;
        public EncounterWave CurrentWave => WaveIndex >= 0 && WaveIndex < waves.Length ? waves[WaveIndex] : null;
        public int EnemiesAlive { get; private set; }
        public float StateElapsed => Time.time - stateEnteredAt;
        public float StateRemaining => State == EncounterState.Pending ? Mathf.Max(0f, startDelay - StateElapsed) :
            State == EncounterState.Spawning ? Mathf.Max(0f, spawnWarning - StateElapsed) :
            State == EncounterState.Cleared ? Mathf.Max(0f, interWaveDelay - StateElapsed) : 0f;
        public float Duration => State == EncounterState.Pending ? 0f :
            (State == EncounterState.Complete || State == EncounterState.Defeated ? completedAt : Time.time) - startedAt;
        public float InterWaveHeal => interWaveHeal;
        public int SwitchCount { get; private set; }
        public int FocusCount { get; private set; }
        public int ComboCount { get; private set; }
        public float DamageTaken { get; private set; }
        public int MembersFallen { get; private set; }
        public IReadOnlyList<EncounterWave> Waves => waves;

        private void Awake()
        {
            if (squad == null || waves.Length == 0)
            {
                Debug.LogError("TRACE encounter requires a squad and at least one wave.", this);
                enabled = false;
                return;
            }
            // Later waves must be dormant whatever the saved scene state says.
            foreach (var wave in waves)
                foreach (var enemy in wave.enemies)
                    if (enemy != null) enemy.gameObject.SetActive(false);
            foreach (var marker in spawnMarkers) if (marker != null) marker.gameObject.SetActive(false);
            input = squad.GetComponent<TracePlayerInput>();
            foreach (var opportunity in FindObjectsByType<ComboOpportunity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                opportunities.Add(opportunity);
            stateEnteredAt = Time.time;
        }

        private void Start()
        {
            foreach (var member in squad.Members)
            {
                if (member == null) continue;
                trackedMembers.Add(member.Health);
                member.Health.OnDamaged += MemberDamaged;
                member.Health.OnDeath += MemberDied;
            }
            lastActive = squad.ActiveMember;
            comboBaseline = TotalCombos();
        }

        private void OnDestroy()
        {
            foreach (var health in trackedMembers)
            {
                if (health == null) continue;
                health.OnDamaged -= MemberDamaged;
                health.OnDeath -= MemberDied;
            }
        }

        private void MemberDamaged(float amount) { if (State != EncounterState.Pending) DamageTaken += amount; }
        private void MemberDied() { if (State != EncounterState.Pending) MembersFallen++; }

        private void Update()
        {
            if (input != null && input.RestartPressed) { Restart(); return; }
            TrackStatistics();
            if (State != EncounterState.Complete && State != EncounterState.Defeated && squad.IsDefeated)
            {
                Enter(EncounterState.Defeated);
                completedAt = Time.time;
                return;
            }
            switch (State)
            {
                case EncounterState.Pending:
                    if ((autoStart || begun) && StateElapsed >= startDelay) BeginWave(0);
                    break;
                case EncounterState.Spawning:
                    if (StateElapsed >= spawnWarning) ActivateWave();
                    break;
                case EncounterState.Fighting:
                    EnemiesAlive = CountAlive(CurrentWave);
                    if (EnemiesAlive > 0) break;
                    if (WaveIndex + 1 >= waves.Length)
                    {
                        Enter(EncounterState.Complete);
                        completedAt = Time.time;
                    }
                    else
                    {
                        Enter(EncounterState.Cleared);
                        foreach (var member in squad.Members)
                            if (member != null && !member.Health.IsDead) member.Health.Heal(interWaveHeal);
                    }
                    break;
                case EncounterState.Cleared:
                    if (StateElapsed >= interWaveDelay) BeginWave(WaveIndex + 1);
                    break;
            }
        }

        private void TrackStatistics()
        {
            if (squad.ActiveMember != lastActive)
            {
                if (lastActive != null && squad.ActiveMember != null && State != EncounterState.Pending) SwitchCount++;
                lastActive = squad.ActiveMember;
            }
            bool focusActive = focus != null && focus.IsActive;
            if (focusActive && !focusWasActive && State != EncounterState.Pending) FocusCount++;
            focusWasActive = focusActive;
            ComboCount = TotalCombos() - comboBaseline;
        }

        private int TotalCombos()
        {
            int total = 0;
            foreach (var opportunity in opportunities) if (opportunity != null) total += opportunity.ConsumptionCount;
            return total;
        }

        private void BeginWave(int index)
        {
            if (index == 0) startedAt = Time.time;
            WaveIndex = index;
            Enter(EncounterState.Spawning);
            var wave = waves[index];
            for (int i = 0; i < spawnMarkers.Length; i++)
            {
                bool used = i < wave.enemies.Length && wave.enemies[i] != null;
                if (spawnMarkers[i] == null) continue;
                spawnMarkers[i].gameObject.SetActive(used);
                if (used) spawnMarkers[i].position = wave.enemies[i].transform.position + Vector3.up * 0.05f;
            }
            if (spawnWarning <= 0f) ActivateWave();
        }

        private void ActivateWave()
        {
            foreach (var marker in spawnMarkers) if (marker != null) marker.gameObject.SetActive(false);
            foreach (var enemy in CurrentWave.enemies)
            {
                if (enemy == null) continue;
                enemy.gameObject.SetActive(true);
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled && !agent.isOnNavMesh) agent.Warp(enemy.transform.position);
            }
            Enter(EncounterState.Fighting);
            EnemiesAlive = CountAlive(CurrentWave);
        }

        private static int CountAlive(EncounterWave wave)
        {
            int alive = 0;
            if (wave == null) return 0;
            foreach (var enemy in wave.enemies)
                if (enemy != null && !enemy.GetComponent<Health>().IsDead) alive++;
            return alive;
        }

        private void Enter(EncounterState next)
        {
            State = next;
            stateEnteredAt = Time.time;
        }

        // Manual start for staged scenes; the Start Delay still applies so spawn warnings stay readable.
        public void Begin()
        {
            if (State != EncounterState.Pending || begun) return;
            begun = true;
            stateEnteredAt = Time.time;
        }

        public bool HasBegun => begun || autoStart;

        // Reloading the scene is the simplest complete reset: health, cooldowns, positions and time ownership.
        public void Restart()
        {
            if (Time.timeScale <= 0f) Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.buildIndex >= 0 ? gameObject.scene.path : gameObject.scene.name);
        }
    }
}
