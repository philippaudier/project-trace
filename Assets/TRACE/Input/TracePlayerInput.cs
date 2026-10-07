using UnityEngine;
using UnityEngine.InputSystem;

namespace TRACE.Input
{
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class TracePlayerInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private bool captureCursor = true;

        private InputActionAsset runtimeActions;
        private InputActionMap player;
        private InputAction move;
        private InputAction look;
        private InputAction sprint;
        private InputAction attack;
        private InputAction dodge;
        private int cursorCapturedFrame = -1;
        private bool focused = true;

        private bool CanRead => isActiveAndEnabled && runtimeActions != null &&
            (Application.isBatchMode || (focused && (!captureCursor || Cursor.lockState == CursorLockMode.Locked)));
        public bool GameplayInputEnabled => CanRead;
        public bool DodgePressed => CanRead && dodge.WasPressedThisFrame();
        public bool AttackPressed => CanRead && Time.frameCount != cursorCapturedFrame && attack.WasPressedThisFrame();
        public Vector2 Move => CanRead ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => CanRead ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool Sprint => CanRead && sprint.IsPressed();

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("TRACE input requires an Input Action Asset.", this);
                enabled = false;
                return;
            }
            // Each controlled character owns its actions; never enable/disable the shared asset.
            runtimeActions = Instantiate(actions);
            player = runtimeActions.FindActionMap("Player", true);
            move = player.FindAction("Move", true);
            look = player.FindAction("Look", true);
            sprint = player.FindAction("Sprint", true);
            attack = player.FindAction("Attack", true);
            dodge = player.FindAction("Dodge", true);
        }

        private void OnEnable()
        {
            player?.Enable();
            if (captureCursor && !Application.isBatchMode)
                SetCursor(true);
        }

        private void Update()
        {
            if (!captureCursor || Application.isBatchMode || !focused)
                return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
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
