using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.Input
{
    /// <summary>
    /// 本地输入中继（T10-05）：桥接 PlayerControls（T10-01 生成包装类）→ IPlayerInputSource，
    /// 同时实现 IInputDeviceProvider（阶段四交互提示图标用）。
    /// 设备隔离：每实例绑定键鼠对或指定索引手柄（经 asset.devices 限制），虚拟/真机手柄通用。
    /// 消费方（PlayerController/PlayerCameraController）不变——阶段三 SetInputSource 注入即可。
    /// </summary>
    [AddComponentMenu("Cqw/Input/LocalInputRelay")]
    public class LocalInputRelay : MonoBehaviour, IPlayerInputSource, IInputDeviceProvider
    {
        [Tooltip("设备类型：键鼠对或手柄")]
        public InputDeviceKind deviceKind = InputDeviceKind.KeyboardMouse;

        [Tooltip("手柄索引（DeviceKind=Gamepad 时有效，0 起）")]
        public int gamepadIndex;

        PlayerControls controls;
        Gamepad boundPad;
        bool prevJump, prevCamSwitch;
        int lastTickFrame = -1;

        // ---- IPlayerInputSource 缓存值 ----
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

        public string DeviceId => deviceKind == InputDeviceKind.KeyboardMouse
            ? "keyboardMouse"
            : "gamepad" + gamepadIndex;

        /// <summary>设备类型变化事件（UI 图标订阅；T10-05 验收点）。</summary>
        public event Action<InputDeviceType> OnDeviceChanged;

        void Awake()
        {
            controls = new PlayerControls();
            ApplyDeviceBinding();
            controls.Player.Enable();
        }

        void OnDestroy() => controls?.Dispose();

        void OnEnable() { if (controls != null) controls.Player.Enable(); }
        void OnDisable() { if (controls != null) controls.Player.Disable(); }

        /// <summary>配置设备（加入流程调用；改变类型时广播 OnDeviceChanged）。</summary>
        public void Configure(InputDeviceKind kind, int padIndex)
        {
            var before = GetCurrentDeviceType();
            deviceKind = kind;
            gamepadIndex = padIndex;
            ApplyDeviceBinding();
            var after = GetCurrentDeviceType();
            if (before != after) OnDeviceChanged?.Invoke(after);
        }

        void ApplyDeviceBinding()
        {
            if (controls == null) return;
            if (deviceKind == InputDeviceKind.KeyboardMouse)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                if (kb != null && mouse != null) controls.asset.devices = new InputDevice[] { kb, mouse };
                else if (kb != null) controls.asset.devices = new InputDevice[] { kb };
                else if (mouse != null) controls.asset.devices = new InputDevice[] { mouse };
                else controls.asset.devices = Array.Empty<InputDevice>();
            }
            else
            {
                ResolvePad();
                controls.asset.devices = boundPad != null
                    ? new InputDevice[] { boundPad }
                    : Array.Empty<InputDevice>();
            }

            // InputSystem 行为：修改 asset.devices 后已启用的 action 不自动重绑 → 强制重绑
            controls.Player.Disable();
            controls.Player.Enable();
        }

        void ResolvePad()
        {
            var pads = Gamepad.all;
            boundPad = (gamepadIndex >= 0 && gamepadIndex < pads.Count) ? pads[gamepadIndex] : null;
        }

        void Update() => Tick();

        public void SetEnabled(bool value)
        {
            IsEnabled = value;
            if (controls == null) return;
            if (value) controls.Player.Enable();
            else
            {
                controls.Player.Disable();
                Move = Vector2.zero; Look = Vector2.zero;
                Interact = Attack = Defend = PushToTalk = Jump = JumpPressed = CameraSwitchPressed = false;
            }
        }

        public void Tick()
        {
            if (!IsEnabled) { prevJump = prevCamSwitch = false; return; }
            if (lastTickFrame == Time.frameCount) return;
            lastTickFrame = Time.frameCount;

            // 设备热插拔：手柄重新解析
            if (deviceKind == InputDeviceKind.Gamepad && (boundPad == null || !boundPad.added))
                ApplyDeviceBinding();

            var p = controls.Player;
            Move = p.Move.ReadValue<Vector2>();
            Look = p.Look.ReadValue<Vector2>();
            Interact = p.Interact.IsPressed();
            Attack = p.Attack.IsPressed();
            Defend = p.Defend.IsPressed();
            PushToTalk = p.PushToTalk.IsPressed();
            Jump = p.Jump.IsPressed();
            var camHeld = p.CameraSwitch.IsPressed();

            JumpPressed = Jump && !prevJump;
            CameraSwitchPressed = camHeld && !prevCamSwitch;
            prevJump = Jump;
            prevCamSwitch = camHeld;
        }

        // ---- IInputDeviceProvider（阶段四提示图标联动）----
        public InputDeviceType GetCurrentDeviceType()
            => deviceKind == InputDeviceKind.KeyboardMouse
                ? InputDeviceType.KeyboardMouse
                : InputDeviceType.Gamepad;

        public string GetDeviceId() => DeviceId;
    }
}
