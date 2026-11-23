using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.Input
{
    /// <summary>
    /// 本地输入管理（T10-03）：PlayerInputManager 接管设备加入。
    /// 设备按键加入 → playerJoined → 识别设备类型 → 经阶段三 LocalPlayerJoinManager
    /// 既有网络生成流程（JoinGamepad 内部 NetworkServer.Spawn，不绕开网络）→ 销毁锚点。
    /// 仅主机（NetworkServer.active）启用；启用期间关闭 LocalPlayerJoinManager 的手柄轮询防双加入。
    /// 修复（T10-03）：阶段三 LocalPlayerJoinManager 未挂场景时惰性自创建（其 Awake 自带
    /// Instance 防重 + DontDestroyOnLoad，幂等），否则 EnsureManager 因 join==null 永不建 PIM。
    /// 补丁（T10-05）：订阅 OnLocalPlayerJoined，为手柄槽位补装 LocalInputRelay 并替换
    /// 槽位 InputSource（阶段三 CreateSource 只产 DefaultPlayerInputSource，不认识阶段十中继）。
    /// </summary>
    [AddComponentMenu("Cqw/Input/LocalInputManager")]
    public class LocalInputManager : MonoBehaviour
    {
        public static LocalInputManager Instance { get; private set; }

        PlayerInputManager pim;
        GameObject anchorPrefab;
        bool lastServerActive;
        bool joinSubscribed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureJoinManager();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TeardownManager();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null) return;
            new GameObject("Systems_LocalInputManager").AddComponent<LocalInputManager>();
        }

        /// <summary>阶段三 LocalPlayerJoinManager 缺席时自创建（幂等；其 Awake 自防重），并挂接手柄槽位 relay 补丁。</summary>
        void EnsureJoinManager()
        {
            var join = LocalPlayerJoinManager.Instance;
            if (join == null)
            {
                join = new GameObject("Systems_LocalPlayerJoinManager").AddComponent<LocalPlayerJoinManager>();
                Debug.Log("[LocalInput] LocalPlayerJoinManager 缺席 → 已自创建。");
            }
            if (!joinSubscribed)
            {
                join.OnLocalPlayerJoined += EnsureRelayForSlot;
                joinSubscribed = true;
            }
        }

        /// <summary>T10-05：手柄槽位 InputSource → LocalInputRelay（设备隔离桥接 + 提示图标联动）。</summary>
        void EnsureRelayForSlot(LocalPlayerSlot slot)
        {
            if (slot == null || slot.DeviceId == "keyboardMouse") return;
            if (slot.InputSource is LocalInputRelay) return;
            if (slot.Player == null) return;

            var relay = slot.Player.GetComponent<LocalInputRelay>();
            if (relay == null) relay = slot.Player.gameObject.AddComponent<LocalInputRelay>();
            int padIdx = slot.DeviceId != null && slot.DeviceId.StartsWith("gamepad") &&
                         int.TryParse(slot.DeviceId.Substring(7), out var i) ? i : 0;
            relay.Configure(InputDeviceKind.Gamepad, padIdx);

            var ctrl = slot.Player.GetComponent<PlayerController>();
            if (ctrl != null) ctrl.SetInputSource(relay);
            var cam = slot.Player.GetComponent<PlayerCameraController>();
            if (cam != null) cam.SetInputSource(relay);
            slot.InputSource = relay;
            Debug.Log($"[LocalInput] 槽位 {slot.SlotIndex} InputSource → LocalInputRelay（{slot.DeviceId}）。");
        }

        void Update()
        {
            bool serverActive = NetworkServer.active;
            if (serverActive == lastServerActive) return;
            lastServerActive = serverActive;
            if (serverActive) EnsureManager();
            else TeardownManager();
        }

        void EnsureManager()
        {
            if (pim != null) return;
            var join = LocalPlayerJoinManager.Instance;
            if (join == null) return;

            // 锚点模板：PlayerInputManager 需要带 PlayerInput 的 playerPrefab；
            // 其 actions 资产同时是加入检测源（Join = 任意键/手柄键）。
            anchorPrefab = new GameObject("LocalJoinAnchor", typeof(PlayerInput));
            anchorPrefab.SetActive(false);
            var pi = anchorPrefab.GetComponent<PlayerInput>();
            pi.actions = RebindManager.Controls.asset;
            pi.defaultActionMap = "Player";
            pi.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            var go = new GameObject("Systems_PlayerInputManager");
            pim = go.AddComponent<PlayerInputManager>();
            pim.playerPrefab = anchorPrefab;
            pim.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
            pim.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            // maxPlayerCount 在当前 Input System 版本为只读属性（默认 4，与 LocalPlayerJoinManager.maxLocalPlayers 对齐）
            pim.playerJoinedEvent.AddListener(OnPlayerJoined);

            join.enableGamepadJoin = false; // PlayerInputManager 接管期间关闭轮询防双加入
            Debug.Log("[LocalInput] PlayerInputManager 已接管设备加入。");
        }

        void TeardownManager()
        {
            var join = LocalPlayerJoinManager.Instance;
            if (join != null) join.enableGamepadJoin = true;

            if (pim != null)
            {
                pim.playerJoinedEvent.RemoveListener(OnPlayerJoined);
                Destroy(pim.gameObject);
                pim = null;
            }
            if (anchorPrefab != null) { Destroy(anchorPrefab); anchorPrefab = null; }
        }

        void OnPlayerJoined(PlayerInput pi)
        {
            var join = LocalPlayerJoinManager.Instance;

            // 识别加入设备
            string deviceId = null;
            int padIndex = -1;
            foreach (var dev in pi.devices)
            {
                if (dev is Gamepad pad)
                {
                    for (int i = 0; i < Gamepad.all.Count; i++)
                        if (Gamepad.all[i] == pad) { padIndex = i; break; }
                    deviceId = "gamepad" + padIndex;
                    break;
                }
                if (dev is Keyboard || dev is Mouse) deviceId = "keyboardMouse";
            }

            // 锚点仅用于识别设备：真身走网络生成流程
            Destroy(pi.gameObject);

            if (join == null || deviceId == null)
            {
                Debug.LogWarning("[LocalInput] 无法识别加入设备（忽略）。");
                return;
            }
            if (deviceId == "keyboardMouse")
            {
                // 键鼠槽位由 isLocalPlayer 主玩家自动绑定（阶段三行为），跳过
                Debug.Log("[LocalInput] 键鼠设备加入（主玩家已自动绑定，跳过）。");
                return;
            }

            Debug.Log($"[LocalInput] {deviceId} 加入 → 网络生成流程。");
            join.JoinGamepad(padIndex);
        }
    }
}
