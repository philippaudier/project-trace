using UnityEngine;
using UnityEngine.InputSystem;

namespace TRACE.Input
{
    // Gameplay reads intents only (Attack, Dodge, SkillPrimary...). Physical keys and gamepad controls
    // live in TRACEInput.inputactions, so a rebinding menu can change them without touching combat code.
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class TracePlayerInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private bool captureCursor = true;
        [SerializeField, Min(1f), Tooltip("Right stick: look delta per second at full deflection, in mouse-delta units.")]
        private float stickLookSpeed = 1500f;

        private InputActionAsset runtimeActions;
        private InputActionMap player;
        private InputActionMap system;
        private InputAction move;
        private InputAction look;
        private InputAction sprint;
        private InputAction attack;
        private InputAction dodge;
        private InputAction skillPrimary;
        private InputAction skillSecondary;
        private InputAction tacticalFocus;
        private InputAction character1;
        private InputAction character2;
        private InputAction character3;
        private InputAction switchPrevious;
        private InputAction switchNext;
        private InputAction interact;
        private InputAction ultimate;
        private InputAction targetLock;
        private InputAction targetLeft;
        private InputAction targetRight;
        private InputAction tacticalMap;
        private InputAction pause;
        private InputAction restart;
        private TRACE.Tactical.TacticalFocus focusMode;
        private int cursorCapturedFrame = -1;
        private bool focused = true;
        private bool menuMode;

        private bool CanRead => isActiveAndEnabled && runtimeActions != null &&
            (Application.isBatchMode || (focused && (!captureCursor || Cursor.lockState == CursorLockMode.Locked)));
        public bool GameplayInputEnabled => CanRead;
        public bool CombatInputEnabled => CanRead && (focusMode == null || !focusMode.BlocksCombat);
        public bool TacticalFocusHeld => CanRead && tacticalFocus.IsPressed();
        public bool DodgePressed => CombatInputEnabled && dodge.WasPressedThisFrame();
        public bool SkillPressed => CombatInputEnabled && skillPrimary.WasPressedThisFrame();
        public bool AttackPressed => CombatInputEnabled && Time.frameCount != cursorCapturedFrame && attack.WasPressedThisFrame();
        public Vector2 Move => CombatInputEnabled ? move.ReadValue<Vector2>() : Vector2.zero;
        public bool Sprint => CombatInputEnabled && sprint.IsPressed();
        public int RequestedMember => !CanRead ? -1 : character1.WasPressedThisFrame() ? 0 :
            character2.WasPressedThisFrame() ? 1 : character3.WasPressedThisFrame() ? 2 : -1;
        // -1 previous, +1 next, 0 none: cyclic squad switch for controllers.
        public int RequestedSwitchDirection => !CanRead ? 0 : switchPrevious.WasPressedThisFrame() ? -1 : switchNext.WasPressedThisFrame() ? 1 : 0;
        // Prepared intents: bound, readable, not yet consumed by any gameplay system.
        public bool SkillSecondaryPressed => CombatInputEnabled && skillSecondary.WasPressedThisFrame();
        public bool InteractPressed => CanRead && interact.WasPressedThisFrame();
        public bool UltimatePressed => CombatInputEnabled && ultimate.WasPressedThisFrame();
        public bool TargetLockPressed => CanRead && targetLock.WasPressedThisFrame();
        // -1 left, +1 right: screen-space target change while locked (mouse wheel).
        public int TargetSwitchDirection => !CanRead ? 0 : targetLeft.WasPressedThisFrame() ? -1 : targetRight.WasPressedThisFrame() ? 1 : 0;
        // Raw right-stick value for impulse detection; zero when the look source is not a gamepad.
        public Vector2 StickLook => CanRead && look.activeControl != null && look.activeControl.device is Gamepad ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool TacticalMapPressed => CanRead && tacticalMap.WasPressedThisFrame();
        // System intents stay readable while gameplay input is disabled (defeat, menus).
        public bool PausePressed => runtimeActions != null && pause.WasPressedThisFrame();
        public bool InMenu => menuMode;

        // Clickable prototype panels (end of encounter, end of slice): free the cursor and stop recapturing it on click,
        // otherwise the mouse-down locks the cursor before the button ever sees the mouse-up.
        public void EnterMenu()
        {
            menuMode = true;
            if (captureCursor && !Application.isBatchMode) SetCursor(false);
        }

        public void ExitMenu() => menuMode = false;
        public bool RestartPressed => runtimeActions != null && restart.WasPressedThisFrame();

        public Vector2 Look
        {
            get
            {
                if (!CanRead) return Vector2.zero;
                Vector2 value = look.ReadValue<Vector2>();
                // A stick is a rate, a mouse is a delta: convert so the camera keeps one unscaled code path.
                return look.activeControl != null && look.activeControl.device is Gamepad
                    ? value * (stickLookSpeed * Time.unscaledDeltaTime) : value;
            }
        }

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("TRACE input requires an Input Action Asset.", this);
                enabled = false;
                return;
            }
            // The local input owner has one private action instance, shared by the active squad member.
            runtimeActions = Instantiate(actions);
            player = runtimeActions.FindActionMap("Player", true);
            move = player.FindAction("Move", true);
            look = player.FindAction("Look", true);
            sprint = player.FindAction("Sprint", true);
            attack = player.FindAction("Attack", true);
            dodge = player.FindAction("Dodge", true);
            skillPrimary = player.FindAction("SkillPrimary", true);
            skillSecondary = player.FindAction("SkillSecondary", true);
            tacticalFocus = player.FindAction("TacticalFocus", true);
            character1 = player.FindAction("Character1", true);
            character2 = player.FindAction("Character2", true);
            character3 = player.FindAction("Character3", true);
            switchPrevious = player.FindAction("SwitchPrevious", true);
            switchNext = player.FindAction("SwitchNext", true);
            interact = player.FindAction("Interact", true);
            ultimate = player.FindAction("Ultimate", true);
            targetLock = player.FindAction("TargetLock", true);
            targetLeft = player.FindAction("TargetLeft", true);
            targetRight = player.FindAction("TargetRight", true);
            tacticalMap = player.FindAction("TacticalMap", true);
            system = runtimeActions.FindActionMap("System", true);
            pause = system.FindAction("Pause", true);
            restart = system.FindAction("Restart", true);
            system.Enable();
            focusMode = GetComponent<TRACE.Tactical.TacticalFocus>();
        }

        private void OnEnable()
        {
            player?.Enable();
            if (captureCursor && !Application.isBatchMode)
                SetCursor(true);
        }

        private void Update()
        {
            if (!captureCursor || Application.isBatchMode || !focused || menuMode)
                return;
            if (pause.WasPressedThisFrame())
                SetCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked && attack.WasPressedThisFrame())
            {
                cursorCapturedFrame = Time.frameCount;
                SetCursor(true);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus && captureCursor && !Application.isBatchMode)
                SetCursor(false);
        }

        private void OnDisable()
        {
            player?.Disable();
            if (captureCursor && !Application.isBatchMode)
                SetCursor(false);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null)
                Destroy(runtimeActions);
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
