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

        private float yaw;
        private float pitch;
        private float smoothYaw;
        private float smoothPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private float recenterVelocity;
        private float idleLookTime;

        private void Start()
        {
            if (input == null || player == null || orbitTarget == null)
            {
                Debug.LogError("TRACE orbit requires input, player and a separate orbit target.", this);
                enabled = false;
                return;
            }
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

            smoothYaw = Mathf.SmoothDampAngle(smoothYaw, yaw, ref yawVelocity, lookSmoothTime, Mathf.Infinity, dt);
            smoothPitch = Mathf.SmoothDampAngle(smoothPitch, pitch, ref pitchVelocity, lookSmoothTime, Mathf.Infinity, dt);
            ApplyTarget();
        }

        private void ApplyTarget()
        {
            orbitTarget.SetPositionAndRotation(player.position + Vector3.up * targetHeight,
                Quaternion.Euler(smoothPitch, smoothYaw, 0f));
        }

        private void OnValidate()
        {
            maximumPitch = Mathf.Max(minimumPitch, maximumPitch);
            initialPitch = Mathf.Clamp(initialPitch, minimumPitch, maximumPitch);
        }
    }
}
