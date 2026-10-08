using TRACE.AI;
using UnityEngine;

namespace TRACE.Combat
{
    // Keep per-target markers, but only one readable instruction over a tightly grouped encounter.
    [DefaultExecutionOrder(50)]
    public sealed class ComboPrompt : MonoBehaviour
    {
        [SerializeField] private SquadController squad;
        [SerializeField] private ComboFeedback[] feedbacks;
        private UnityEngine.Camera view;
        private TRACE.Tactical.TacticalFocus focusMode;
        private void Awake()
        {
            view = UnityEngine.Camera.main;
            focusMode = squad.GetComponent<TRACE.Tactical.TacticalFocus>();
        }

        private void LateUpdate()
        {
            if (focusMode != null && focusMode.IsActive)
            {
                foreach (var feedback in feedbacks)
                    if (feedback != null && feedback.isActiveAndEnabled) feedback.SetDetailVisible(false);
                return;
            }
            ComboFeedback selected = null;
            int bestPriority = -1;
            float bestDistance = float.PositiveInfinity;
            foreach (var feedback in feedbacks)
            {
                if (feedback == null || !feedback.isActiveAndEnabled || view == null) continue;
                var opportunity = feedback.Opportunity;
                if (opportunity.Type == ComboOpportunityType.None && !feedback.IsShowingImpact) continue;
                Vector3 screen = view.WorldToViewportPoint(feedback.transform.position + Vector3.up * 2.8f);
                if (screen.z <= 0f || screen.x < 0f || screen.x > 1f || screen.y < 0f || screen.y > 1f) continue;
                int priority = feedback.IsShowingImpact ? 3 : opportunity.Type == ComboOpportunityType.Protected &&
                    squad.ActiveMember != null && feedback.gameObject == squad.ActiveMember.gameObject ? 2 :
                    opportunity.Type == ComboOpportunityType.Grouped ? 1 : 0;
                float distance = new Vector2(screen.x - 0.5f, screen.y - 0.55f).sqrMagnitude + screen.z * 0.001f;
                if (priority < bestPriority || (priority == bestPriority && distance >= bestDistance)) continue;
                selected = feedback;
                bestPriority = priority;
                bestDistance = distance;
            }
            foreach (var feedback in feedbacks)
                if (feedback != null && feedback.isActiveAndEnabled) feedback.SetDetailVisible(feedback == selected);
        }
    }
}
