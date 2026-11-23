using UnityEngine;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>
    /// Player input source abstraction: controllers and cameras only consume this interface, do not read input devices directly.
    /// Phase 3 provides default implementation; Phase 10 has LocalInputRelay / PlayerInputManager take over injection,
    /// consumers (PlayerController / PlayerCameraController etc.) need no changes.
    /// Implementations are responsible for refreshing cached values in each frame's Tick() (properties read cached values, thread-safe).
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>Movement input (-1..1, x=right y=forward). WASD / left stick.</summary>
        Vector2 Move { get; }

        /// <summary>Look input delta (mouse delta / right stick value, unscaled — consumer applies sensitivity).</summary>
        Vector2 Look { get; }

        /// <summary>Interact (held).</summary>
        bool Interact { get; }

        /// <summary>Attack (held).</summary>
        bool Attack { get; }

        /// <summary>Defend (held).</summary>
        bool Defend { get; }

        /// <summary>Push-to-talk (held).</summary>
        bool PushToTalk { get; }

        /// <summary>Jump (held).</summary>
        bool Jump { get; }

        /// <summary>Jump (pressed this frame, consumable).</summary>
        bool JumpPressed { get; }

        /// <summary>Camera mode switch (pressed this frame, consumable).</summary>
        bool CameraSwitchPressed { get; }

        /// <summary>Device identifier: "keyboardMouse" or "gamepad0".."gamepadN" (local multiplayer and Phase 10 takeover).</summary>
        string DeviceId { get; }

        /// <summary>Whether input source is enabled (disabled sources return zero values).</summary>
        bool IsEnabled { get; }

        /// <summary>Enable/disable input source (used for pause, spectate, split-screen).</summary>
        void SetEnabled(bool value);

        /// <summary>Refresh cached values (called by host each frame; default implementation auto-updates internally, safe to call repeatedly).</summary>
        void Tick();
    }
}
