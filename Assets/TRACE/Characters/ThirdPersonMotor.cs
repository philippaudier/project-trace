using TRACE.Input;
using TRACE.Combat;
using UnityEngine;

namespace TRACE.Characters
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private Transform movementCamera;
        [Header("Locomotion (metres / seconds)")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6.2f;
        [SerializeField, Min(0.1f)] private float acceleration = 18f;
        [SerializeField, Min(0.1f)] private float deceleration = 24f;
        [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.12f;
        [Header("Ground")]
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField, Min(0.01f)] private float groundSnapDistance = 0.25f;
        [SerializeField, Min(0.1f)] private float gravity = 25f;
        [SerializeField, Min(1f)] private float terminalSpeed = 45f;

        [Header("Dodge")]
        [SerializeField] private DamageReceiver damageReceiver;
        [SerializeField, Min(0.1f)] private float dodgeDistance = 3f;
        [SerializeField, Min(0.05f)] private float dodgeDuration = 0.3f;
        [SerializeField, Min(0f)] private float dodgeCooldown = 0.7f;
        [SerializeField, Min(0f)] private float invulnerabilityDelay = 0.03f;
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.2f;
        private float dodgeElapsed;
        private float dodgeReadyAt;
        private Vector3 dodgeDirection;
        public bool IsDodging { get; private set; }
        public Vector3 DodgeDirection => dodgeDirection;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalSpeed;
        private float turnVelocity;
        public bool IsGrounded { get; private set; }
        public Vector3 PlanarVelocity => planarVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (input == null || movementCamera == null)
            {
                Debug.LogError("TRACE motor requires input and a movement camera.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            planarVelocity = Vector3.zero;
            verticalSpeed = turnVelocity = 0f;
            IsGrounded = false;
            IsDodging = false;
            if (damageReceiver != null) damageReceiver.ClearInvulnerability();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 direction = Application.isPlaying && IsDodging ? dodgeDirection : -transform.forward;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f,
                transform.position + Vector3.up * 0.1f + direction * dodgeDistance);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !controller.enabled)
                return;

            Vector2 movement = Vector2.ClampMagnitude(input.Move, 1f);
            // Yaw-only basis stays orthonormal even at the pitch limits.
            Quaternion yaw = Quaternion.Euler(0f, movementCamera.eulerAngles.y, 0f);
            Vector3 desired = yaw * new Vector3(movement.x, 0f, movement.y);
            if (IsDodging && (!input.GameplayInputEnabled || dodgeElapsed >= dodgeDuration))
            {
                IsDodging = false;
                planarVelocity = Vector3.zero;
                if (damageReceiver != null) damageReceiver.ClearInvulnerability();
            }
            if (input.DodgePressed && !IsDodging && IsGrounded && Time.time >= dodgeReadyAt)
            {
                IsDodging = true;
                dodgeElapsed = 0f;
                dodgeReadyAt = Time.time + Mathf.Max(dodgeCooldown, dodgeDuration);
                dodgeDirection = desired.sqrMagnitude > 0.001f ? desired.normalized : -transform.forward;
                if (damageReceiver != null)
                    damageReceiver.GrantInvulnerability(invulnerabilityDelay,
                        Mathf.Min(invulnerabilityDuration, Mathf.Max(0f, dodgeDuration - invulnerabilityDelay)));
            }
            float speed = input.Sprint ? sprintSpeed : walkSpeed;
            if (IsDodging)
            {
                // Integrate a fast-start/eased-stop distance curve: distance is stable across frame rates.
                float previous = Mathf.Clamp01(dodgeElapsed / dodgeDuration);
                dodgeElapsed += dt;
                float next = Mathf.Clamp01(dodgeElapsed / dodgeDuration);
                float travelled = dodgeDistance * (Mathf.Sin(next * Mathf.PI * 0.5f) - Mathf.Sin(previous * Mathf.PI * 0.5f));
                planarVelocity = dodgeDirection * (travelled / dt);
            }
            else
                planarVelocity = Vector3.MoveTowards(planarVelocity, desired * speed,
                    (movement.sqrMagnitude > 0f ? acceleration : deceleration) * dt);

            if (!IsDodging && desired.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, Mathf.SmoothDampAngle(
                    transform.eulerAngles.y, angle, ref turnVelocity, rotationSmoothTime), 0f);
            }

            float radius = controller.radius * 0.9f;
            Vector3 foot = transform.TransformPoint(controller.center) - Vector3.up * (controller.height * 0.5f);
            bool hitGround = Physics.SphereCast(foot + Vector3.up * (radius + 0.15f), radius,
                Vector3.down, out RaycastHit hit, 0.15f + groundSnapDistance,
                groundMask, QueryTriggerInteraction.Ignore);
            // At a step edge a sphere can return the riser's normal although our feet
            // still stand on flat ground. Prefer that support before rejecting uphill motion.
            if (hitGround && Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit &&
                Physics.Raycast(foot + Vector3.up * 0.15f, Vector3.down, out RaycastHit support,
                    0.15f + groundSnapDistance, groundMask, QueryTriggerInteraction.Ignore) &&
                Vector3.Angle(support.normal, Vector3.up) <= controller.slopeLimit)
                hit = support;
            bool walkable = hitGround && Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit;
            IsGrounded = walkable;

            Vector3 velocity = planarVelocity;
            if (walkable)
            {
                velocity = Vector3.ProjectOnPlane(planarVelocity, hit.normal);
                // Downward adhesion keeps contact while descending ramps and small steps.
                verticalSpeed = -2f - planarVelocity.magnitude * Mathf.Tan(controller.slopeLimit * Mathf.Deg2Rad);
            }
            else
            {
                verticalSpeed = Mathf.Max(verticalSpeed - gravity * dt, -terminalSpeed);
                if (hitGround)
                {
                    Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
                    Vector3 uphill = -new Vector3(downhill.x, 0f, downhill.z).normalized;
                    velocity -= uphill * Mathf.Max(0f, Vector3.Dot(velocity, uphill));
                    velocity += downhill * 3f;
                }
            }

            CollisionFlags flags = controller.Move((velocity + Vector3.up * verticalSpeed) * dt);
            IsGrounded = walkable && (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
        }
    }
}
