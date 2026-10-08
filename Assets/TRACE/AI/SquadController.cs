using System.Collections.Generic;
using TRACE.Camera;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using UnityEngine;

namespace TRACE.AI
{
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class SquadController : MonoBehaviour
    {
        [SerializeField] private DamageReceiver leader;
        [SerializeField] private CompanionController[] companions;
        [SerializeField, Min(1f)] private float formationTurnSpeed = 100f;
        [SerializeField] private bool showDebug;
        [Header("Optional V0.5 control transfer")]
        [SerializeField] private SquadMember[] members;
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private ThirdPersonOrbit orbit;
        [SerializeField] private Transform activeIndicator;
        [SerializeField, Min(0f)] private float switchCooldown = 0.2f;
        [SerializeField, Min(0f)] private float cameraTransition = 0.18f;
        [SerializeField] private Vector3 leftOffset = new Vector3(-1.6f, 0f, -2.2f);
        [SerializeField] private Vector3 rightOffset = new Vector3(1.9f, 0f, -3f);
        private Quaternion heading;
        private Vector3 previousLeaderPosition;
        private float switchReadyAt;
        private float switchReadyUnscaledAt;
        private TRACE.Tactical.TacticalFocus focusMode;
        private float indicatorFlashUntil;
        public SquadMember ActiveMember { get; private set; }
        public IReadOnlyList<SquadMember> Members => members;
        public IReadOnlyList<CompanionController> Companions => companions;
        public bool IsDefeated { get; private set; }
        private bool SupportsSwitching => members != null && members.Length == 3;

        public DamageReceiver Leader => leader;
        public bool LeaderAlive => leader != null && leader.isActiveAndEnabled && !leader.Health.IsDead;
        public bool ShowDebug => showDebug;

        private void Awake()
        {
            focusMode = GetComponent<TRACE.Tactical.TacticalFocus>();
            if (leader == null || companions == null || companions.Length != 2)
            {
                Debug.LogError("TRACE squad requires a leader and exactly two companions.", this);
                enabled = false;
                return;
            }
            heading = Quaternion.Euler(0f, leader.transform.eulerAngles.y, 0f);
            previousLeaderPosition = leader.transform.position;
        }

        private void Start()
        {
            if (SupportsSwitching)
            {
                if (input == null || orbit == null || activeIndicator == null ||
                    members[0] == null || members[1] == null || members[2] == null)
                {
                    Debug.LogError("TRACE switching squad requires three members, shared input, orbit and indicator.", this);
                    enabled = false;
                    return;
                }
                ActiveMember = members[0];
                leader = ActiveMember.Receiver;
                for (int i = 0; i < members.Length; i++) members[i].SetPlayerControlled(i == 0);
                RebuildFormation();
            }
            foreach (CompanionController companion in companions)
                if (companion != null) IgnoreMemberCollisions(companion);
        }

        private void Update()
        {
            if (!SupportsSwitching || IsDefeated) return;
            if (ActiveMember == null || ActiveMember.Health.IsDead || !ActiveMember.gameObject.activeInHierarchy)
            {
                for (int i = 0; i < members.Length; i++)
                    if (CanSelect(i)) { TransferControl(i); return; }
                IsDefeated = true;
                ActiveMember = null;
                activeIndicator.gameObject.SetActive(false);
                input.enabled = false;
                return;
            }
            if (input.RequestedMember >= 0) SwitchToMember(input.RequestedMember);
            else if (input.RequestedSwitchDirection != 0) SwitchRelative(input.RequestedSwitchDirection);
        }

        // Zero-based stable roster indices, independent of keyboard or any future selection UI.
        public bool SwitchToMember(int index)
        {
            if (!SupportsSwitching || IsDefeated || Time.timeScale <= 0f || !CanSelect(index) ||
                members[index] == ActiveMember || (focusMode != null && focusMode.IsActive
                    ? Time.unscaledTime < switchReadyUnscaledAt : Time.time < switchReadyAt)) return false;
            TransferControl(index);
            return true;
        }

        // Cyclic selection over living members, for SwitchPrevious / SwitchNext. Same rules as a direct switch.
        public bool SwitchRelative(int direction)
        {
            if (!SupportsSwitching || ActiveMember == null || direction == 0) return false;
            int current = System.Array.IndexOf(members, ActiveMember);
            for (int step = 1; step < members.Length; step++)
            {
                int index = (current + (direction > 0 ? step : members.Length - step)) % members.Length;
                if (CanSelect(index)) return SwitchToMember(index);
            }
            return false;
        }

        private bool CanSelect(int index) => index >= 0 && index < members.Length &&
            members[index] != null && members[index].isActiveAndEnabled && !members[index].Health.IsDead;

        private void TransferControl(int index)
        {
            if (ActiveMember != null) ActiveMember.SetPlayerControlled(false);
            ActiveMember = members[index];
            leader = ActiveMember.Receiver;
            ActiveMember.SetPlayerControlled(true);
            RebuildFormation();
            orbit.FollowCharacter(ActiveMember.transform, cameraTransition);
            switchReadyAt = Time.time + switchCooldown;
            switchReadyUnscaledAt = Time.unscaledTime + switchCooldown;
            indicatorFlashUntil = Time.time + 0.30f;
        }

        private void RebuildFormation()
        {
            int slot = 0;
            foreach (SquadMember member in members)
            {
                if (member == null || member == ActiveMember) continue;
                companions[slot] = member.Companion;
                member.Companion.SetFormationOffset(slot == 0 ? leftOffset : rightOffset);
                slot++;
            }
            heading = Quaternion.Euler(0f, leader.transform.eulerAngles.y, 0f);
            previousLeaderPosition = leader.transform.position;
            foreach (CompanionController companion in companions)
                if (companion != null) IgnoreMemberCollisions(companion);
        }

        internal void IgnoreMemberCollisions(CompanionController member)
        {
            if (leader == null || companions == null) return;
            // Also reapplied on member activation: Unity can discard ignored pairs on deactivation.
            Collider body = member.GetComponent<Collider>();
            Physics.IgnoreCollision(leader.GetComponent<Collider>(), body);
            foreach (CompanionController other in companions)
                if (other != null && other != member)
                    Physics.IgnoreCollision(body, other.GetComponent<Collider>());
        }

        private void LateUpdate()
        {
            if (leader == null) return;
            if (activeIndicator != null && ActiveMember != null)
            {
                activeIndicator.position = ActiveMember.transform.position + Vector3.up * 0.06f;
                activeIndicator.localScale = Vector3.one * (1f + Mathf.Clamp01((indicatorFlashUntil - Time.time) / 0.12f) * 0.25f);
            }
            Vector3 movement = leader.transform.position - previousLeaderPosition;
            movement.y = 0f;
            // Freeze formation orientation at rest; turning the camera cannot orbit the team.
            if (movement.sqrMagnitude > 0.000025f)
                heading = Quaternion.RotateTowards(heading,
                    Quaternion.Euler(0f, leader.transform.eulerAngles.y, 0f), formationTurnSpeed * Time.deltaTime);
            previousLeaderPosition = leader.transform.position;
        }

        public Vector3 FormationPosition(Vector3 offset) => leader.transform.position + heading * offset;

        public Vector3 SeparateDestination(CompanionController member, Vector3 destination, float radius)
        {
            destination = AwayFrom(destination, leader.transform.position, radius, member.FormationOffset);
            foreach (CompanionController other in companions)
                if (other != null && other != member && other.isActiveAndEnabled && !other.Health.IsDead)
                    destination = AwayFrom(destination, other.transform.position, radius, member.FormationOffset);
            return destination;
        }

        public bool IsRecoverySpaceFree(CompanionController member, Vector3 point)
        {
            if (Vector3.Distance(point, leader.transform.position) < 1.2f) return false;
            foreach (CompanionController other in companions)
                if (other != null && other != member && Vector3.Distance(point, other.transform.position) < 1.1f)
                    return false;
            return true;
        }

        // Only used by enemies explicitly wired to this squad. The V0.3 duel keeps its fixed target.
        public DamageReceiver NearestLivingMember(Vector3 position, float range, LayerMask obstructionMask)
        {
            if (!LeaderAlive) return null;
            DamageReceiver best = null;
            float bestDistance = range * range;
            Consider(leader, position, obstructionMask, ref best, ref bestDistance);
            foreach (CompanionController companion in companions)
                if (companion != null && companion.isActiveAndEnabled)
                    Consider(companion.Receiver, position, obstructionMask, ref best, ref bestDistance);
            return best;
        }

        private static void Consider(DamageReceiver candidate, Vector3 position, LayerMask mask,
            ref DamageReceiver best, ref float bestDistance)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.Health.IsDead) return;
            float distance = (candidate.transform.position - position).sqrMagnitude;
            if (distance >= bestDistance || Physics.Linecast(position + Vector3.up * 0.9f,
                candidate.transform.position + Vector3.up * 0.9f, mask, QueryTriggerInteraction.Ignore)) return;
            best = candidate;
            bestDistance = distance;
        }

        private static Vector3 AwayFrom(Vector3 point, Vector3 other, float radius, Vector3 fallback)
        {
            Vector3 delta = point - other;
            delta.y = 0f;
            if (delta.sqrMagnitude >= radius * radius) return point;
            if (delta.sqrMagnitude < 0.0001f) delta = fallback;
            Vector3 separated = other + delta.normalized * radius;
            separated.y = point.y;
            return separated;
        }

        private void OnDrawGizmos()
        {
            if (!showDebug || leader == null || companions == null) return;
            foreach (CompanionController companion in companions)
            {
                if (companion == null) continue;
                Vector3 point = Application.isPlaying ? FormationPosition(companion.FormationOffset) :
                    leader.transform.TransformPoint(companion.FormationOffset);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(point, 0.5f);
                Gizmos.DrawLine(leader.transform.position, point);
            }
        }
    }
}
