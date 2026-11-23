using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Input;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>Local player slot (device -> player object binding).</summary>
    public class LocalPlayerSlot
    {
        /// <summary>Slot index (0=KB+mouse, 1..N=gamepad).</summary>
        public int SlotIndex;
        /// <summary>Device identifier (keyboardMouse / gamepad0..N).</summary>
        public string DeviceId;
        /// <summary>Bound player object (network spawned).</summary>
        public PlayerIdentity Player;
        /// <summary>This player's input source instance (independent).</summary>
        public IPlayerInputSource InputSource;
    }

    /// <summary>
    /// Local multiplayer join manager (Phase 3 minimal viable version):
    /// - KB+mouse = player 0 (auto-spawned with StartHost on Host; spawned with connection on Client)
    /// - Gamepad button press -> requests a local player for that device (**spawned via network flow, does not bypass networking**)
    /// - Each local player has an independent IPlayerInputSource (device-bound)
    /// - Split screen not in this phase (placeholder: extra local player cameras created by PlayerCameraController individually,
    ///   multi-camera simultaneous rendering requires split-screen/render target solution, Phase 10 PlayerInputManager unified takeover fills this gap)
    ///
    /// Does not conflict with Phase 10 PlayerInputManager: this class only handles the minimal loop of
    /// "device -> slot -> spawn request"; Phase 10 can replace/extend device detection and slot assignment logic,
    /// player spawn path remains unchanged.
    /// </summary>
    [AddComponentMenu("Cqw/Player/LocalPlayerJoinManager")]
    public class LocalPlayerJoinManager : MonoBehaviour
    {
        public static LocalPlayerJoinManager Instance { get; private set; }

        [Header("Config")]
        [Tooltip("Gamepad join button (any press of this button joins)")]
        public bool enableGamepadJoin = true;
        [Tooltip("Max local player count (including KB+mouse)")]
        public int maxLocalPlayers = 4;
        [Tooltip("Character type per slot (falls back to Default when insufficient)")]
        public CharacterType[] slotCharacterTypes = { CharacterType.Default, CharacterType.Chicken, CharacterType.Robo };

        /// <summary>Local player joined event (per slot).</summary>
        public event Action<LocalPlayerSlot> OnLocalPlayerJoined;
        /// <summary>Local player left event (per slot).</summary>
        public event Action<LocalPlayerSlot> OnLocalPlayerLeft;

        readonly List<LocalPlayerSlot> slots = new List<LocalPlayerSlot>();

        /// <summary>Current local player slots (read-only snapshot).</summary>
        public IReadOnlyList<LocalPlayerSlot> Slots => slots;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null) return;
            new GameObject("Systems_LocalPlayerJoinManager").AddComponent<LocalPlayerJoinManager>();
        }

        void OnEnable() => PlayerRegistry.OnPlayerRegistered += HandlePlayerRegistered;
        void OnDisable() => PlayerRegistry.OnPlayerRegistered -= HandlePlayerRegistered;

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Player registration callback: main local player (isLocalPlayer) auto-occupies KB+mouse slot and activates camera.</summary>
        void HandlePlayerRegistered(PlayerIdentity player)
        {
            if (player == null) return;
            var identity = player.GetComponent<Mirror.NetworkIdentity>();
            if (identity == null || !identity.isLocalPlayer) return;  // KB+mouse slot only for main player (extra local players use JoinGamepad)
            if (GetSlotOf(player) != null) return;                    // Already bound

            var slot = FindSlot("keyboardMouse");
            if (slot == null) slot = BindKeyboardMouse(player);
            else slot.Player = player;

            // Slot 0 activates camera; other slots' cameras stay disabled (split-screen gap, see class comment)
            var cam = player.GetComponent<PlayerCameraController>();
            if (cam != null)
            {
                if (slot.SlotIndex == 0) cam.ActivateForLocal();
                else Debug.LogWarning($"[LocalMulti] Slot {slot.SlotIndex} camera not activated (split-screen rendering deferred to Phase 10).");
            }
        }

        void Update()
        {
            if (!enableGamepadJoin) return;
            if (!NetworkServer.active) return; // Only host can add local players (client-side local multiplayer is Phase 10 scope)
            if (slots.Count >= maxLocalPlayers) return;

            // Detect unclaimed gamepad button press to join (A button)
            var pads = UnityEngine.InputSystem.Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                if (IsDeviceTaken("gamepad" + i)) continue;
                var pad = pads[i];
                if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)
                {
                    JoinGamepad(i);
                    break;
                }
            }
        }

        /// <summary>KB+mouse slot (player 0) — call to bind after first player spawns.</summary>
        public LocalPlayerSlot BindKeyboardMouse(PlayerIdentity player)
        {
            var existing = FindSlot("keyboardMouse");
            if (existing != null) { existing.Player = player; return existing; }

            var slot = new LocalPlayerSlot
            {
                SlotIndex = slots.Count,
                DeviceId = "keyboardMouse",
                Player = player,
                InputSource = CreateSource(player, "keyboardMouse", 0)
            };
            slots.Add(slot);
            player.SetCharacterType(TypeForSlot(slot.SlotIndex));
            OnLocalPlayerJoined?.Invoke(slot);
            Debug.Log($"[LocalMulti] KB+mouse joined -> slot {slot.SlotIndex} ({player.DisplayName})");
            return slot;
        }

        /// <summary>Gamepad join (server): requests a new local player object for this gamepad (network spawn flow).</summary>
        public LocalPlayerSlot JoinGamepad(int gamepadIndex)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[LocalMulti] Only host can add local players (client-side local multiplayer deferred to Phase 10).");
                return null;
            }
            var deviceId = "gamepad" + gamepadIndex;
            if (IsDeviceTaken(deviceId))
            {
                Debug.Log($"[LocalMulti] {deviceId} already in a slot, ignoring duplicate join.");
                return null;
            }
            if (slots.Count >= maxLocalPlayers)
            {
                Debug.LogWarning($"[LocalMulti] Max local players reached ({maxLocalPlayers}).");
                return null;
            }

            // Spawn via network flow: reuse NetworkManager's playerPrefab and spawn point rotation
            var nm = NetworkManager.singleton as CqwNetworkManager;
            if (nm == null || nm.playerPrefab == null)
            {
                Debug.LogError("[LocalMulti] NetworkManager/playerPrefab not ready.");
                return null;
            }

            // Local multiplayer on Host reuses host connection (Mirror supports multiple player objects per connection)
            var conn = NetworkServer.localConnection;
            if (conn == null)
            {
                Debug.LogError("[LocalMulti] No local connection (Host mode required).");
                return null;
            }

            var spTransform = RoundRobinSpawner.GetNext(nm.Config);
            var pos = spTransform != null ? RoundRobinSpawner.PositionWithJitter(spTransform) : Vector3.zero;
            var rot = spTransform != null ? spTransform.rotation : Quaternion.identity;

            var playerObj = Instantiate(nm.playerPrefab, pos, rot);
            var identity = playerObj.GetComponent<PlayerIdentity>();
            if (identity != null)
            {
                identity.SetNickname($"Player{slots.Count + 1}");
                identity.SetSteamId($"offline:local{slots.Count + 1}");
                identity.SetCharacterType(TypeForSlot(slots.Count));
            }

            // Extra local players on the same connection: Mirror's AddPlayerForConnection only allows one player object per connection,
            // so from the second onward use NetworkServer.Spawn(obj, conn) to grant that connection ownership (client-authoritative movement still works).
            NetworkServer.Spawn(playerObj, conn);

            var slot = new LocalPlayerSlot
            {
                SlotIndex = slots.Count,
                DeviceId = deviceId,
                Player = identity,
                InputSource = CreateSource(identity, deviceId, gamepadIndex)
            };
            slots.Add(slot);
            OnLocalPlayerJoined?.Invoke(slot);
            Debug.Log($"[LocalMulti] {deviceId} joined -> slot {slot.SlotIndex} ({identity?.DisplayName}) @ {pos}");
            return slot;
        }

        /// <summary>Local player leave (destroys player object and releases slot).</summary>
        public bool LeaveSlot(LocalPlayerSlot slot)
        {
            if (slot == null || !slots.Contains(slot)) return false;
            if (slot.Player != null && NetworkServer.active)
                NetworkServer.Destroy(slot.Player.gameObject);
            slots.Remove(slot);
            OnLocalPlayerLeft?.Invoke(slot);
            Debug.Log($"[LocalMulti] Slot {slot.SlotIndex} ({slot.DeviceId}) left.");
            return true;
        }

        /// <summary>Creates and binds an independent input source for the player (device isolation; T10-05 gamepad slots prefer LocalInputRelay).</summary>
        IPlayerInputSource CreateSource(PlayerIdentity player, string deviceId, int gamepadIndex)
        {
            if (player == null) return null;

            // T10-05: gamepad slots prefer LocalInputRelay (PlayerControls bridge + device awareness/prompt icon integration)
            if (deviceId != "keyboardMouse")
            {
                var relay = player.GetComponent<LocalInputRelay>();
                if (relay == null) relay = player.gameObject.AddComponent<LocalInputRelay>();
                relay.Configure(InputDeviceKind.Gamepad, gamepadIndex);
                var ctrlR = player.GetComponent<PlayerController>();
                if (ctrlR != null) ctrlR.SetInputSource(relay);
                var camR = player.GetComponent<PlayerCameraController>();
                if (camR != null) camR.SetInputSource(relay);
                return relay;
            }

            var src = player.GetComponent<DefaultPlayerInputSource>();
            if (src == null) src = player.gameObject.AddComponent<DefaultPlayerInputSource>();
            src.DeviceKind = InputDeviceKind.KeyboardMouse;
            var ctrl = player.GetComponent<PlayerController>();
            if (ctrl != null) ctrl.SetInputSource(src);
            var cam = player.GetComponent<PlayerCameraController>();
            if (cam != null) cam.SetInputSource(src);
            return src;
        }

        CharacterType TypeForSlot(int slotIndex) =>
            slotCharacterTypes != null && slotIndex < slotCharacterTypes.Length
                ? slotCharacterTypes[slotIndex]
                : CharacterType.Default;

        LocalPlayerSlot FindSlot(string deviceId)
        {
            foreach (var s in slots) if (s.DeviceId == deviceId) return s;
            return null;
        }

        bool IsDeviceTaken(string deviceId) => FindSlot(deviceId) != null;

        /// <summary>Find slot by player object (null if none).</summary>
        public LocalPlayerSlot GetSlotOf(PlayerIdentity player)
        {
            foreach (var s in slots) if (s.Player == player) return s;
            return null;
        }

        /// <summary>Diagnostic description.</summary>
        public string Describe() =>
            $"{slots.Count}/{maxLocalPlayers} local players: " +
            string.Join(", ", System.Linq.Enumerable.Select(slots, s => $"[{s.SlotIndex}]{s.DeviceId}"));
    }
}
