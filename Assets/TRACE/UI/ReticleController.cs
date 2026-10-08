using UnityEngine;
using UnityEngine.UI;

namespace TRACE.UI
{
    // Centre: a thin ring. Faint in exploration, present in combat, hidden under a hard lock (the world marker
    // takes over) and during Tactical Focus. Cyan while a soft target exists.
    public sealed class ReticleController : HudPanel
    {
        [SerializeField] private Image reticle;
        [SerializeField, Range(0f, 1f)] private float explorationAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float combatAlpha = 0.8f;

        protected override Vector2 SlideDirection => Vector2.zero;

        protected override float TargetAlpha(HudRoot hud)
        {
            if (hud.State == HudState.Focus || hud.IsLocked || hud.ActiveMember == null) return 0f;
            return hud.State == HudState.Combat ? combatAlpha : explorationAlpha;
        }

        protected override void OnTick(HudRoot hud, bool refresh)
        {
            if (reticle != null) reticle.color = hud.TargetHealth != null ? Cyan : OffWhite;
        }
    }
}
