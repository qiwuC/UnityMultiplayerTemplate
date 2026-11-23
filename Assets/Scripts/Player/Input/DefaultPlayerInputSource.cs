using UnityEngine;
using UnityEngine.InputSystem;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>Default input source: reads Input System devices directly (KB+mouse or indexed gamepad).</summary>
    public enum InputDeviceKind
    {
        KeyboardMouse,
        Gamepad
    }

    /// <summary>
    /// Default input source implementation: reads Input System devices directly (Keyboard+Mouse or indexed Gamepad).
    /// Attached to player object (enabled for local player, disabled for non-local).
    /// This is the Phase 3 minimal viable implementation; Phase 10 will have PlayerInputManager/LocalInputRelay
    /// take over injection, at which point consumers just swap the IPlayerInputSource instance without changes.
    /// </summary>
    [AddComponentMenu("Cqw/Player/DefaultPlayerInputSource")]
    public class DefaultPlayerInputSource : MonoBehaviour, IPlayerInputSource
    {
        [Tooltip("Device kind: KB+mouse or gamepad")]
        public InputDeviceKind DeviceKind = InputDeviceKind.KeyboardMouse;

        [Tooltip("Gamepad index (valid when DeviceKind=Gamepad, starting from 0)")]
        public int GamepadIndex;

        // ---- IPlayerInputSource cached values ----
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Interact { get; private set; }
        public bool Attack { get; private set; }
        public bool Defend { get; private set; }
        public bool PushToTalk { get; private set; }
        public bool Jump { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CameraSwitchPressed { get; private set; }
        public bool IsEnabled { get; private set; } = true;

        public string DeviceId => DeviceKind == InputDeviceKind.KeyboardMouse
            ? "keyboardMouse"
            : "gamepad" + GamepadIndex;

        Gamepad boundGamepad;
        // Self-managed edge detection (previous frame held state), independent of wasPressedThisFrame input update timing
        bool prevJumpHeld, prevCamSwitchHeld;
        // Same-frame idempotency: Update and consumer manual Tick dual-channel, edge semantics computed only once
        int lastTickFrame = -1;

        void Update() => Tick();

        public void SetEnabled(bool value)
        {
            IsEnabled = value;
            if (!value)
            {
                Move = Vector2.zero; Look = Vector2.zero;
                Interact = Attack = Defend = PushToTalk = Jump = JumpPressed = CameraSwitchPressed = false;
            }
        }

        public void Tick()
        {
            if (!IsEnabled)
            {
                prevJumpHeld = prevCamSwitchHeld = false;
                return;
            }
            // Same-frame repeated Tick idempotency (avoids edge being double-computed)
            if (lastTickFrame == Time.frameCount) return;
            lastTickFrame = Time.frameCount;

            if (DeviceKind == InputDeviceKind.KeyboardMouse) TickKeyboardMouse();
            else TickGamepad();

            // Edge detection (held->pressed semantics), computed after device reads
            JumpPressed = Jump && !prevJumpHeld;
            CameraSwitchPressed = CameraSwitchHeld && !prevCamSwitchHeld;
            prevJumpHeld = Jump;
            prevCamSwitchHeld = CameraSwitchHeld;
        }

        bool CameraSwitchHeld;

        void TickKeyboardMouse()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                Vector2 move = Vector2.zero;
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                Move = move;

                Interact = kb.eKey.isPressed;
                Jump = kb.spaceKey.isPressed;
                CameraSwitchHeld = kb.cKey.isPressed;
            }
            else { Move = Vector2.zero; Interact = Jump = CameraSwitchHeld = false; }

            if (mouse != null)
            {
                Look = mouse.delta.ReadValue();
                Attack = mouse.leftButton.isPressed;
                Defend = mouse.rightButton.isPressed;
                // Mouse side button (forward) as push-to-talk; devices without side button use V key as fallback
                PushToTalk = mouse.forwardButton.isPressed || (kb != null && kb.vKey.isPressed);
            }
            else { Look = Vector2.zero; Attack = Defend = PushToTalk = false; }
        }

        void TickGamepad()
        {
            // Resolve bound gamepad (cached; re-resolve on hot-plug)
            if (boundGamepad == null || !boundGamepad.added)
            {
                var pads = Gamepad.all;
                boundGamepad = GamepadIndex >= 0 && GamepadIndex < pads.Count ? pads[GamepadIndex] : null;
            }

            if (boundGamepad == null)
            {
                Move = Vector2.zero; Look = Vector2.zero;
                Interact = Attack = Defend = PushToTalk = Jump = JumpPressed = CameraSwitchPressed = false;
                return;
            }

            var pad = boundGamepad;
            Move = pad.leftStick.ReadValue();
            Look = pad.rightStick.ReadValue();
            Interact = pad.buttonWest.isPressed;          // X
            Attack = pad.rightShoulder.isPressed;          // RB
            Defend = pad.leftShoulder.isPressed;           // LB
            PushToTalk = pad.buttonEast.isPressed;         // B
            Jump = pad.buttonSouth.isPressed;              // A
            CameraSwitchHeld = pad.buttonNorth.isPressed;  // Y
        }
    }
}
