using TRACE.Combat;
using UnityEngine;

namespace TRACE.AI
{
    // Read-only surface shared by companions, the tactical overlay and the encounter.
    // Each concrete brain keeps its own state machine; no generic AI framework.
    public abstract class EnemyBrain : MonoBehaviour
    {
        [SerializeField, Tooltip("Elite or boss: the HUD gives its target panel more weight.")] private bool elite;
        public bool IsElite => elite;
        public abstract string Archetype { get; }
        public abstract DamageReceiver CurrentTarget { get; }
        // Chasing, repositioning or attacking: anything but idle or dead.
        public abstract bool IsEngaged { get; }
        // Windup or aim: the moment a telegraph is readable and a reaction is still possible.
        public abstract bool IsPreparingAttack { get; }
        public abstract float PreparationRemaining { get; }
        // Short rich-text state for the tactical overlay card.
        public abstract string StatusLabel { get; }
    }
}
