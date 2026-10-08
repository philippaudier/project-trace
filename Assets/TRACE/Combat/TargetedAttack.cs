using UnityEngine;

namespace TRACE.Combat
{
    // The same windup, damage, cooldown and shot cue serve AI and controlled ranged attacks.
    [DefaultExecutionOrder(25)]
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class TargetedAttack : MonoBehaviour
    {
        private Health owner;
        private Health victim;
        private Collider victimCollider;
        private GameObject cue;
        private LayerMask obstructionMask;
        private bool ranged;
        private float damage;
        private float cooldown;
        private float windup;
        private float impactAt;
        private float cueUntil;
        private bool playerInitiated;
        public float Range { get; private set; }
        public float ReadyAt { get; private set; }
        public bool IsWindingUp { get; private set; }
        private Vector3 Origin => transform.position + Vector3.up * 0.9f;

        private void Awake() => owner = GetComponent<Health>();
        private void OnDisable() => Cancel();

        public void Configure(bool isRanged, float hitDamage, float hitCooldown, float preparation,
            float range, GameObject visual, LayerMask obstacles)
        {
            ranged = isRanged;
            damage = hitDamage;
            cooldown = hitCooldown;
            windup = preparation;
            Range = range;
            cue = visual;
            obstructionMask = obstacles;
            Cancel();
        }

        public void DelayUntil(float time) => ReadyAt = Mathf.Max(ReadyAt, time);

        public bool TryBegin(Health target, Collider body, bool fromPlayer = false)
        {
            if (!isActiveAndEnabled || owner.IsDead || IsWindingUp || Time.time < ReadyAt || Time.timeScale <= 0f ||
                !CanHit(target, body)) return false;
            victim = target;
            playerInitiated = fromPlayer;
            victimCollider = body;
            IsWindingUp = true;
            impactAt = Time.time + windup;
            ReadyAt = Time.time + Mathf.Max(cooldown, windup + 0.12f);
            ShowCue(body.bounds.center, false);
            return true;
        }

        private void Update()
        {
            if (owner.IsDead) { Cancel(); return; }
            if (!IsWindingUp)
            {
                if (Time.time >= cueUntil) HideCue();
                return;
            }
            if (Time.time < impactAt) return;
            IsWindingUp = false;
            if (CanHit(victim, victimCollider))
            {
                Vector3 end = victimCollider.bounds.center;
                if (ComboDamage.CanHit(victim))
                {
                    float multiplier = playerInitiated ? ComboDamage.ConsumeProtected(gameObject) : 1f;
                    ComboDamage.Apply(victim, damage * multiplier, transform.position);
                }
                ShowCue(end, true);
            }
            else HideCue();
            victim = null;
            victimCollider = null;
        }

        private bool CanHit(Health target, Collider body) => target != null && target.isActiveAndEnabled &&
            !target.IsDead && body != null && body.enabled && body.gameObject.activeInHierarchy &&
            Vector3.Distance(transform.position, target.transform.position) <= Range + 0.15f &&
            !Physics.Linecast(Origin, body.bounds.center, obstructionMask, QueryTriggerInteraction.Ignore);

        public void Cancel()
        {
            IsWindingUp = false;
            victim = null;
            victimCollider = null;
            HideCue(); // Deliberately preserve ReadyAt through ownership changes.
        }

        private void ShowCue(Vector3 end, bool impact)
        {
            if (cue == null) return;
            Vector3 delta = end - Origin;
            if (delta.sqrMagnitude < 0.001f) delta = transform.forward;
            float length = ranged && impact ? delta.magnitude : Mathf.Min(delta.magnitude, 1.1f);
            cue.transform.SetPositionAndRotation(Origin + delta.normalized * length * 0.5f, Quaternion.LookRotation(delta));
            cue.transform.localScale = new Vector3(impact ? 0.12f : 0.25f, 0.08f, length);
            cue.SetActive(true);
            cueUntil = Time.time + 0.12f;
        }

        private void HideCue() { if (cue != null) cue.SetActive(false); }
    }
}
