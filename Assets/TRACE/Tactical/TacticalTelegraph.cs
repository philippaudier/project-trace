using TRACE.AI;
using UnityEngine;

namespace TRACE.Tactical
{
    [RequireComponent(typeof(BasicMeleeEnemy))]
    public sealed class TacticalTelegraph : MonoBehaviour
    {
        [SerializeField] private TacticalFocus focus;
        [SerializeField] private GameObject visual;
        [SerializeField] private LineRenderer boundary;
        [SerializeField] private LineRenderer direction;
        [SerializeField] private LineRenderer victimLine;
        private BasicMeleeEnemy enemy;
        public bool IsVisible => visual.activeSelf;
        private void Awake() => enemy = GetComponent<BasicMeleeEnemy>();
        private void LateUpdate()
        {
            bool show = focus.IsActive && enemy.isActiveAndEnabled &&
                (enemy.Phase == BasicMeleeEnemy.AttackPhase.Windup || enemy.Phase == BasicMeleeEnemy.AttackPhase.Active);
            visual.SetActive(show);
            if (!show) return;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Vector3 forward = enemy.StrikeDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 end = origin + forward * enemy.HitRange;
            boundary.SetPosition(0, origin - right * enemy.HitHalfWidth);
            boundary.SetPosition(1, origin + right * enemy.HitHalfWidth);
            boundary.SetPosition(2, end + right * enemy.HitHalfWidth);
            boundary.SetPosition(3, end - right * enemy.HitHalfWidth);
            direction.SetPosition(0, origin);
            direction.SetPosition(1, end);
            direction.SetPosition(2, end - forward * 0.45f + right * 0.3f);
            direction.SetPosition(3, end);
            direction.SetPosition(4, end - forward * 0.45f - right * 0.3f);
            victimLine.enabled = enemy.CurrentTarget != null && !enemy.CurrentTarget.Health.IsDead;
            if (victimLine.enabled)
            {
                victimLine.SetPosition(0, origin + Vector3.up * 0.25f);
                victimLine.SetPosition(1, enemy.CurrentTarget.transform.position + Vector3.up * 0.4f);
            }
        }
        private void OnDisable() { if (visual != null) visual.SetActive(false); }
    }
}
