using System;
using TRACE.AI;
using TRACE.Narrative;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TRACE.Encounter
{
    // DEVELOPMENT ONLY (FieldTest). Wakes a pre-placed enemy composition from a debug terminal or a function key, and
    // resets the zone by reloading the scene (the existing EncounterController.Restart path: health, cooldowns,
    // positions and time all come back). Enemies cannot be revived, so a group spawns once per load.
    [DisallowMultipleComponent]
    public sealed class EncounterDebugStations : MonoBehaviour
    {
        [Serializable]
        public sealed class Group
        {
            public string name = "";
            public EnemyBrain[] enemies = Array.Empty<EnemyBrain>();
            public Interactable terminal;
            public Key hotkey = Key.None;
            [Tooltip("Gizmo only: centre and radius of the combat area.")] public Vector3 area;
            public float radius = 12f;
            [NonSerialized] public bool active;
        }

        [SerializeField] private Group[] groups = Array.Empty<Group>();
        [SerializeField] private Interactable resetTerminal;
        [SerializeField] private Key resetKey = Key.F9;
        [SerializeField] private EncounterController encounter;

        public int GroupCount => groups.Length;
        public string GroupName(int index) => groups[index].name;
        public EnemyBrain[] GroupEnemies(int index) => groups[index].enemies;
        public bool IsActive(int index) => groups[index].active;
        public int ResetRequests { get; private set; }
        public bool ReloadOnReset { get; set; } = true;

        private void OnEnable()
        {
            for (int i = 0; i < groups.Length; i++)
            {
                int index = i;
                if (groups[i].terminal != null) groups[i].terminal.Interacted += () => Spawn(index);
            }
            if (resetTerminal != null) resetTerminal.Interacted += ResetZone;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            for (int i = 0; i < groups.Length; i++)
                if (groups[i].hotkey != Key.None && keyboard[groups[i].hotkey].wasPressedThisFrame) Spawn(i);
            if (resetKey != Key.None && keyboard[resetKey].wasPressedThisFrame) ResetZone();
        }

        // Wakes every enemy of the composition where it was placed; already awake or dead ones are left alone.
        public bool Spawn(int index)
        {
            if (index < 0 || index >= groups.Length || groups[index].active) return false;
            groups[index].active = true;
            foreach (var enemy in groups[index].enemies)
            {
                if (enemy == null || enemy.gameObject.activeSelf) continue;
                enemy.gameObject.SetActive(true);
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled && !agent.isOnNavMesh) agent.Warp(enemy.transform.position);
            }
            return true;
        }

        public void ResetZone()
        {
            ResetRequests++;
            if (!ReloadOnReset) return;
            if (encounter != null) encounter.Restart();
            else SceneManager.LoadScene(gameObject.scene.path);
        }

        private void OnDrawGizmos()
        {
            foreach (var group in groups)
            {
                Gizmos.color = new Color(1f, 0.45f, 0.3f, 0.6f);
                Gizmos.DrawWireSphere(group.area, group.radius);
                if (group.terminal == null) continue;
                Gizmos.color = new Color(1f, 0.78f, 0.16f, 0.9f);
                Gizmos.DrawWireCube(group.terminal.transform.position + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
                Gizmos.DrawLine(group.terminal.transform.position + Vector3.up * 2f, group.area);
            }
        }
    }
}
