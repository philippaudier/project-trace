using UnityEngine;

namespace TRACE.Narrative
{
    // Sliding slab: Open() moves it along an offset over a short time. The collider travels with it.
    [DisallowMultipleComponent]
    public sealed class GateDoor : MonoBehaviour
    {
        [SerializeField] private Vector3 openOffset = Vector3.up * 4.2f;
        [SerializeField, Min(0.05f)] private float duration = 1.6f;
        private Vector3 closedPosition;
        private float progress;
        private bool opening;
        public bool IsOpening => opening && progress < 1f;
        public bool IsOpen => progress >= 1f;
        public int OpenCount { get; private set; }

        private void Awake() => closedPosition = transform.localPosition;

        public void Open()
        {
            if (opening) return;
            opening = true;
            OpenCount++;
        }

        private void Update()
        {
            if (!opening || progress >= 1f) return;
            progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
            transform.localPosition = closedPosition + openOffset * Mathf.SmoothStep(0f, 1f, progress);
        }
    }
}
