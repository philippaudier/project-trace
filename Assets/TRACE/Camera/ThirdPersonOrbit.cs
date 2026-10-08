using TRACE.Input;
using UnityEngine;

namespace TRACE.Camera
{
    // Motor updates first, then this target, then CinemachineBrain in LateUpdate.
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class ThirdPersonOrbit : MonoBehaviour
    {
        [SerializeField] private TracePlayerInput input;
        [SerializeField] private Transform player;
        [SerializeField] private Transform orbitTarget;
        [SerializeField, Min(0f)] private float targetHeight = 1.45f;
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minimumPitch = -30f;
        [SerializeField] private float maximumPitch = 65f;
        [SerializeField] private float initialPitch = 15f;
        [SerializeField, Min(0f)] private float lookSmoothTime = 0.035f;
        [Header("Optional recenter while moving")]
        [SerializeField] private bool recenterWhileMoving;
        [SerializeField, Min(0f)] private float recenterDelay = 1.5f;
        [SerializeField, Min(0.01f)] private float recenterTime = 0.8f;
        [Header("Target lock assist")]
        [SerializeField, Min(0f), Tooltip("Max yaw drift toward the locked target, degrees per real second.")]
        private float lockAssistSpeed = 90f;
        [SerializeField, Min(0f), Tooltip("Real seconds without look input before the assist resumes.")]
        private float lockAssistDelay = 0.25f;
        [SerializeField] private float lockPitch = 18f;

        private float yaw;
        private float pitch;
        private float smoothYaw;
        private float smoothPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private float recenterVelocity;
        private float idleLookTime;
        private Vector3 transitionFrom;
        private float transitionElapsed;
        private float transitionDuration;
        private TRACE.Combat.TargetingSystem targeting;
        public Transform FollowedCharacter => player;
        public bool IsAssisting { get; private set; }
        public bool IsTransitioning => transitionElapsed < transitionDuration;

        public void FollowCharacter(Transform character, float duration)
        {
            if (character == null || character == player) return;
            transitionFrom = orbitTarget.position;
            transitionElapsed = 0f;
            transitionDuration = Mathf.Max(0f, duration);
            player = character;
            // Keep yaw, pitch and their smoothing velocities: only the positional target changes.
        }

        private void Start()
        {
            if (input == null || player == null || orbitTarget == null)
            {
                Debug.LogError("TRACE orbit requires input, player and a separate orbit target.", this);
                enabled = false;
                return;
            }
            targeting = input.GetComponent<TRACE.Combat.TargetingSystem>();
            yaw = smoothYaw = player.eulerAngles.y;
            pitch = smoothPitch = Mathf.Clamp(initialPitch, minimumPitch, maximumPitch);
            ApplyTarget();
        }

        private void Update()
        {
            Vector2 delta = input.Look;
            float dt = Time.unscaledDeltaTime;
            // Mouse delta already measures pixels per frame: do not multiply it by deltaTime.
            yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minimumPitch, maximumPitch);
            idleLookTime = delta.sqrMagnitude > 0.01f ? 0f : idleLookTime + dt;
            if (recenterWhileMoving && idleLookTime >= recenterDelay && input.Move.sqrMagnitude > 0.01f)
                yaw = Mathf.SmoothDampAngle(yaw, player.eulerAngles.y, ref recenterVelocity, recenterTime,
                    Mathf.Infinity, dt);
            else
                recenterVelocity = 0f;
            AssistTowardLock(dt);

            smoothYaw = Mathf.SmoothDampAngle(smoothYaw, yaw, ref yawVelocity, lookSmoothTime, Mathf.Infinity, dt);
            smoothPitch = Mathf.SmoothDampAngle(smoothPitch, pitch, ref pitchVelocity, lookSmoothTime, Mathf.Infinity, dt);
            transitionElapsed += dt;
            ApplyTarget();
        }

        // Drift toward the locked target only while the player is not looking around; eases out near alignment,
        // so the camera helps keep both actors on screen without becoming a rigid duel camera.
        private void AssistTowardLock(float dt)
        {
            var target = targeting != null ? targeting.LockedTarget : null;
            IsAssisting = target != null && idleLookTime >= lockAssistDelay;
            if (!IsAssisting) return;
            Vector3 toTarget = target.transform.position - player.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.25f) return;
            float desired = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float strength = Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(yaw, desired)) / 40f) + 0.15f;
            yaw = Mathf.MoveTowardsAngle(yaw, desired, lockAssistSpeed * strength * dt);
            pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(lockPitch, minimumPitch, maximumPitch), lockAssistSpeed * 0.2f * dt);
        }

        private void ApplyTarget()
        {
            Vector3 position = player.position + Vector3.up * targetHeight;
            if (IsTransitioning)
                position = Vector3.Lerp(transitionFrom, position, Mathf.SmoothStep(0f, 1f, transitionElapsed / transitionDuration));
            orbitTarget.SetPositionAndRotation(position,
                Quaternion.Euler(smoothPitch, smoothYaw, 0f));
        }

        private void OnValidate()
        {
            maximumPitch = Mathf.Max(minimumPitch, maximumPitch);
            initialPitch = Mathf.Clamp(initialPitch, minimumPitch, maximumPitch);
        }
    }
}
