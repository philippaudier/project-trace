using TRACE.AI;
using TRACE.Combat;
using TRACE.Tactical;
using UnityEngine;

namespace TRACE.Characters
{
    // Procedural puppet for a primitive squad body. Every frame it reads the owner's state (movement, melee
    // attack, companion attack, dodge, dash, hits, Tactical Focus) and rotates joint pivots to match. The
    // amplitudes and responses give each character its own movement identity. Purely visual: it never writes
    // gameplay state. Removed with the primitives when a rigged model arrives.
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class CharacterPuppet : MonoBehaviour
    {
        [Header("Pivots")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform upperBody;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHip;
        [SerializeField] private Transform rightHip;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Transform rightShoulder;
        [Header("State sources")]
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private PlayerMeleeAttack melee;
        [SerializeField] private CompanionController companion;
        [SerializeField] private Health health;
        [SerializeField] private TacticalFocus focus;
        [Header("Locomotion")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6.2f;
        [SerializeField, Min(0.1f), Tooltip("Metres travelled per full stride cycle (two steps).")] private float cycleLength = 2f;
        [SerializeField, Range(0f, 80f)] private float legSwing = 32f;
        [SerializeField, Range(0f, 80f)] private float armSwing = 22f;
        [SerializeField, Min(0f)] private float bobHeight = 0.035f;
        [SerializeField, Range(0f, 30f)] private float sprintLean = 8f;
        [SerializeField, Range(0f, 5f), Tooltip("Idle breathing amplitude in degrees.")] private float breathing = 1.2f;
        [Header("Actions (degrees, metres, seconds)")]
        [SerializeField, Range(0f, 120f)] private float attackWindupPitch = 60f;
        [SerializeField, Range(0f, 120f)] private float attackSwingPitch = 75f;
        [SerializeField, Min(0f)] private float attackWindup = 0.08f;
        [SerializeField, Min(0.01f)] private float attackSwing = 0.14f;
        [SerializeField, Min(0f)] private float dodgeCrouch = 0.28f;
        [SerializeField, Range(0f, 45f)] private float dodgeLean = 12f;
        [SerializeField, Range(0f, 45f)] private float dashLean = 22f;
        [SerializeField, Range(0f, 120f)] private float dashArmPitch = 85f;
        [SerializeField, Range(0f, 45f)] private float hitFlinch = 14f;
        [SerializeField, Tooltip("Left hand to the chest while Tactical Focus is held (Tracewalker only).")] private bool focusGesture = true;
        [SerializeField, Range(0f, 120f)] private float focusArmPitch = 70f;
        [SerializeField, Min(0.1f)] private float poseResponse = 14f;
        [SerializeField, Min(0.1f)] private float strikeResponse = 28f;

        private Vector3 lastPosition;
        private float phase;
        private float flinch;
        private float attackStartedAt = -10f;
        private float swingUntil = -10f;
        private bool wasAttacking;
        private bool wasWindingUp;
        private float leftLeg, rightLeg, leftArm, rightArm, lean, crouch, bob, headPitch;

        // Read-outs for tests and debugging. Pitches are local x rotations: positive swings a limb backward,
        // negative forward; a positive lean tilts the upper body forward.
        public float Speed { get; private set; }
        public float LegSwing => leftLeg;
        public float LeftArmPitch => leftArm;
        public float RightArmPitch => rightArm;
        public float Lean => lean;
        public float Crouch => crouch;
        public float LegSwingAmplitude => legSwing;
        public float PoseResponse => poseResponse;
        public bool FocusGesture => focusGesture;
        public bool IsActive => isActiveAndEnabled && (health == null || !health.IsDead);

        private void Awake()
        {
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            if (health != null) health.OnDamaged += Flinch;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamaged -= Flinch;
        }

        private void Flinch(float amount) => flinch = hitFlinch;

        private void LateUpdate()
        {
            // Death hands the pose to CompanionFeedback (body laid on the ground); never fight it.
            if (health != null && health.IsDead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 position = transform.position;
            Vector3 travelled = position - lastPosition;
            travelled.y = 0f;
            lastPosition = position;
            Speed = Mathf.Lerp(Speed, travelled.magnitude / dt, 1f - Mathf.Exp(-dt * 12f));

            bool motorActive = motor != null && motor.isActiveAndEnabled;
            bool dodging = motorActive && motor.IsDodging;
            bool dashing = motorActive && motor.IsSkillDashing;
            bool focused = focusGesture && focus != null && focus.IsActive;
            flinch = Mathf.Lerp(flinch, 0f, 1f - Mathf.Exp(-dt * 8f));

            // Locomotion: stride phase advances with distance, limbs swing in opposition, slight bob and lean.
            float move = Mathf.Clamp01(Speed / walkSpeed);
            if (Speed > 0.15f) phase += Speed / cycleLength * Mathf.PI * 2f * dt;
            float stride = Mathf.Sin(phase) * move;
            float legTarget = stride * legSwing;
            float armTarget = -stride * armSwing;
            float bobTarget = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * move;
            float leanTarget = Mathf.Clamp01(Speed / sprintSpeed) * sprintLean;
            // Idle breathing keeps the body alive when nothing else moves.
            leanTarget += Mathf.Sin(Time.time * 1.3f) * breathing * (1f - move);

            // Attack: the arm pulls back during the windup, whips forward for the swing, then recovers.
            bool playerAttacking = melee != null && melee.isActiveAndEnabled && melee.IsAttacking;
            bool windingUp = companion != null && companion.isActiveAndEnabled && companion.Attack != null && companion.Attack.IsWindingUp;
            if (playerAttacking && !wasAttacking) attackStartedAt = Time.time;
            if (wasWindingUp && !windingUp) swingUntil = Time.time + attackSwing;
            wasAttacking = playerAttacking;
            wasWindingUp = windingUp;
            float rightTarget = armTarget;
            float rightResponse = poseResponse;
            if (playerAttacking)
            {
                rightTarget = Time.time - attackStartedAt < attackWindup ? attackWindupPitch : -attackSwingPitch;
                rightResponse = strikeResponse;
            }
            else if (windingUp)
            {
                rightTarget = attackWindupPitch;
                rightResponse = strikeResponse;
            }
            else if (Time.time < swingUntil)
            {
                rightTarget = -attackSwingPitch;
                rightResponse = strikeResponse;
            }
            if (dashing)
            {
                rightTarget = -dashArmPitch;
                rightResponse = strikeResponse;
                leanTarget += dashLean;
                legTarget = 0f;
            }
            float leftTarget = focused ? -focusArmPitch : -armTarget;
            float crouchTarget = 0f;
            if (dodging)
            {
                crouchTarget = dodgeCrouch;
                leanTarget += dodgeLean;
                legTarget = 0f;
            }
            leanTarget -= flinch;
            float headTarget = focused ? -6f : Mathf.Clamp01(Speed / sprintSpeed) * -4f;

            float blend = 1f - Mathf.Exp(-dt * poseResponse);
            float strike = 1f - Mathf.Exp(-dt * rightResponse);
            leftLeg = Mathf.Lerp(leftLeg, legTarget, blend);
            rightLeg = Mathf.Lerp(rightLeg, -legTarget, blend);
            leftArm = Mathf.Lerp(leftArm, leftTarget, blend);
            rightArm = Mathf.Lerp(rightArm, rightTarget, strike);
            lean = Mathf.Lerp(lean, leanTarget, blend);
            crouch = Mathf.Lerp(crouch, crouchTarget, strike);
            bob = Mathf.Lerp(bob, bobTarget, blend);
            headPitch = Mathf.Lerp(headPitch, headTarget, blend);

            if (leftHip != null) leftHip.localRotation = Quaternion.Euler(leftLeg, 0f, 0f);
            if (rightHip != null) rightHip.localRotation = Quaternion.Euler(rightLeg, 0f, 0f);
            if (leftShoulder != null) leftShoulder.localRotation = Quaternion.Euler(leftArm, 0f, 0f);
            if (rightShoulder != null) rightShoulder.localRotation = Quaternion.Euler(rightArm, 0f, 0f);
            if (upperBody != null) upperBody.localRotation = Quaternion.Euler(lean, 0f, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(headPitch, 0f, 0f);
            if (body != null) body.localPosition = new Vector3(0f, bob - crouch, 0f);
        }
    }
}
