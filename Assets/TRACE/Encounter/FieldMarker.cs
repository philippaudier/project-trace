using UnityEngine;

namespace TRACE.Encounter
{
    // Editor gizmo for level landmarks (spawn, viewpoints, routes, navigation links, audio placeholders). No runtime behaviour.
    public sealed class FieldMarker : MonoBehaviour
    {
        public enum Kind { Spawn, Viewpoint, Route, NavigationLink, Zone, Audio }

        [SerializeField] private Kind kind = Kind.Zone;
        [SerializeField, Min(0.1f)] private float radius = 2f;
        [SerializeField] private Vector3 size = Vector3.zero;

        public Kind MarkerKind => kind;

        private void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                Kind.Spawn => new Color(0.37f, 0.84f, 1f, 0.9f),
                Kind.Viewpoint => new Color(1f, 1f, 1f, 0.8f),
                Kind.Route => new Color(1f, 0.78f, 0.16f, 0.8f),
                Kind.NavigationLink => new Color(0.4f, 1f, 0.6f, 0.8f),
                Kind.Audio => new Color(0.85f, 0.5f, 1f, 0.8f),
                _ => new Color(0.62f, 0.65f, 0.69f, 0.5f),
            };
            if (size != Vector3.zero) Gizmos.DrawWireCube(transform.position + Vector3.up * size.y * 0.5f, size);
            else Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, radius);
            if (kind == Kind.Spawn || kind == Kind.Viewpoint) Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * radius * 2f);
        }
    }
}
