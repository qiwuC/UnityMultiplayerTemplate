using UnityEngine;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>Input device type.</summary>
    public enum InputDeviceType { KeyboardMouse, Gamepad }

    /// <summary>Input device provider interface (Phase 9/10 replacement implementation).</summary>
    public interface IInputDeviceProvider
    {
        InputDeviceType GetCurrentDeviceType();
        string GetDeviceId();
    }

    /// <summary>Default implementation: reads Phase 3 DefaultPlayerInputSource device type.</summary>
    public class DefaultInputDeviceProvider : IInputDeviceProvider
    {
        readonly DefaultPlayerInputSource source;

        public DefaultInputDeviceProvider(DefaultPlayerInputSource source) => this.source = source;

        public InputDeviceType GetCurrentDeviceType() =>
            source != null && source.DeviceKind == InputDeviceKind.Gamepad
                ? InputDeviceType.Gamepad
                : InputDeviceType.KeyboardMouse;

        public string GetDeviceId() => source != null ? source.DeviceId : "unknown";
    }
}
