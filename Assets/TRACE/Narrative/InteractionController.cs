using TRACE.AI;
using TRACE.Input;
using UnityEngine;

namespace TRACE.Narrative
{
    // Picks the nearest usable Interactable around the controlled member and shows one prompt.
    [DefaultExecutionOrder(-6)]
    [DisallowMultipleComponent]
    public sealed class InteractionController : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private SquadController squad;
        [SerializeField] private DialogueRunner dialogue;
        private Interactable[] interactables;
        private GUIStyle style;
        public Interactable Current { get; private set; }
        public int InteractionCount { get; private set; }

        private void Start() => interactables = FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private void Update()
        {
            Interactable best = null;
            float bestDistance = float.PositiveInfinity;
            var member = squad != null ? squad.ActiveMember : null;
            bool busy = dialogue != null && dialogue.IsPlaying;
            if (member != null && !busy && interactables != null)
            {
                foreach (var candidate in interactables)
                {
                    if (candidate == null || !candidate.CanInteract) continue;
                    float distance = Vector3.Distance(member.transform.position, candidate.transform.position);
                    if (distance > candidate.Range || distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (Current != null && Current != best) Current.SetFocused(false);
            Current = best;
            if (Current == null) return;
            Current.SetFocused(true);
            if (input != null && input.InteractPressed && Current.Interact())
            {
                InteractionCount++;
                Current.SetFocused(false);
                Current = null;
            }
        }

        private void OnGUI()
        {
            if (Current == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * 0.5f - 170f, Screen.height - 150f, 340f, 40f), "F / A  :  " + Current.Prompt, style);
        }
    }
}
