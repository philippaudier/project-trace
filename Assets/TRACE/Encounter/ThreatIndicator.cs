using System.Collections.Generic;
using TRACE.AI;
using UnityEngine;

namespace TRACE.Encounter
{
    // Minimal screen-edge hint: an enemy preparing an attack on the controlled member while off camera.
    // Only this case is shown; no radar or full HUD. Hints are computed in LateUpdate so they are testable headless.
    public sealed class ThreatIndicator : MonoBehaviour
    {
        public readonly struct Hint
        {
            public readonly EnemyBrain Enemy;
            public readonly Vector2 ScreenPosition;
            public readonly string Text;
            public Hint(EnemyBrain enemy, Vector2 screenPosition, string text) { Enemy = enemy; ScreenPosition = screenPosition; Text = text; }
        }

        [SerializeField] private SquadController squad;
        [SerializeField] private EnemyBrain[] enemies;
        [SerializeField, Min(0f)] private float edgeMargin = 70f;
        [SerializeField, Tooltip("Draw the prototype IMGUI boxes; off when the HUD ThreatIndicatorView renders the hints.")] private bool legacyGui = true;
        private readonly List<Hint> hints = new List<Hint>();
        private UnityEngine.Camera view;
        private GUIStyle style;
        public IReadOnlyList<Hint> Hints => hints;

        private void Awake() => view = UnityEngine.Camera.main;

        private void LateUpdate()
        {
            hints.Clear();
            if (view == null || squad == null || squad.ActiveMember == null || enemies == null) return;
            float width = view.pixelWidth, height = view.pixelHeight;
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsPreparingAttack ||
                    enemy.CurrentTarget != squad.ActiveMember.Receiver) continue;
                Vector3 screen = view.WorldToScreenPoint(enemy.transform.position + Vector3.up);
                bool behind = screen.z < 0f;
                if (behind) screen = -screen;
                if (!behind && screen.x >= 0f && screen.x <= width && screen.y >= 0f && screen.y <= height) continue;
                Vector2 direction = (new Vector2(screen.x, screen.y) - center).normalized;
                if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
                // Clamp to the screen border with a margin.
                float scaleX = Mathf.Abs(direction.x) > 0.0001f ? (center.x - edgeMargin) / Mathf.Abs(direction.x) : float.PositiveInfinity;
                float scaleY = Mathf.Abs(direction.y) > 0.0001f ? (center.y - edgeMargin) / Mathf.Abs(direction.y) : float.PositiveInfinity;
                Vector2 edge = center + direction * Mathf.Min(scaleX, scaleY);
                string arrow = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0f ? ">" : "<") : (direction.y > 0f ? "^" : "v");
                hints.Add(new Hint(enemy, edge, $"{arrow} {enemy.Archetype} {enemy.PreparationRemaining:0.0}s {arrow}"));
            }
        }

        private void OnGUI()
        {
            if (hints.Count == 0 || !legacyGui) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (var hint in hints)
                GUI.Box(new Rect(hint.ScreenPosition.x - 110f, Screen.height - hint.ScreenPosition.y - 20f, 220f, 40f), hint.Text, style);
        }
    }
}
