using System;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Player;
using Cqw.MultilayerFramework.Systems;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>
    /// 命令行参数（T11-03）：--netmode=host|client|server、--address=ip[:port]、
    /// --automove（自动移动测试）、--verbose（日志 Debug 级）、--lobby=id（占位记录）。
    /// verbose 启动即生效；netmode/address 等 NetworkManager+SessionManager 就绪后应用；
    /// automove 在本地玩家生成后注入 AutoMoveInputSource（仅 Editor/DevBuild）。
    /// </summary>
    public static class CommandLineArgs
    {
        public static string NetMode { get; private set; }
        public static string Address { get; private set; }
        public static string LobbyId { get; private set; }
        public static bool Automove { get; private set; }
        public static bool Verbose { get; private set; }

        static bool parsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoRun()
        {
#if UNITY_EDITOR
            Parse();
            if (Verbose) GameLogger.Level = GameLogLevel.Debug;
            var go = new GameObject("Systems_CommandLineArgs");
            go.AddComponent<CommandLineArgsRunner>();
#else
            Parse();
#endif
        }

        static void Parse()
        {
            if (parsed) return;
            parsed = true;
            foreach (var raw in Environment.GetCommandLineArgs())
            {
                var arg = raw.TrimStart('-');
                if (arg.StartsWith("netmode=", StringComparison.OrdinalIgnoreCase))
                    NetMode = arg.Substring(8).Trim().ToLowerInvariant();
                else if (arg.StartsWith("address=", StringComparison.OrdinalIgnoreCase))
                    Address = arg.Substring(8).Trim();
                else if (arg.StartsWith("lobby=", StringComparison.OrdinalIgnoreCase))
                    LobbyId = arg.Substring(6).Trim();
                else if (string.Equals(arg, "automove", StringComparison.OrdinalIgnoreCase))
                    Automove = true;
                else if (string.Equals(arg, "verbose", StringComparison.OrdinalIgnoreCase))
                    Verbose = true;
            }
        }

        /// <summary>诊断摘要（HUD/日志用）。</summary>
        public static string Describe() =>
            $"netmode={NetMode ?? "-"} address={Address ?? "-"} lobby={LobbyId ?? "-"} automove={Automove} verbose={Verbose}";
    }

    /// <summary>命令行参数延迟应用驱动（等待 NM/Session 就绪；轮询本地玩家注入 automove）。</summary>
    public class CommandLineArgsRunner : MonoBehaviour
    {
        float nextProbe;
        float deadline;
        bool launched;
        bool automoveApplied;

        void Awake() => DontDestroyOnLoad(gameObject);

        void Start() => deadline = Time.unscaledTime + 30f;

        void Update()
        {
            if (Time.unscaledTime < nextProbe) return;
            nextProbe = Time.unscaledTime + 0.5f;

            // netmode：NM + SessionManager 就绪后一次性应用
            if (!launched && !string.IsNullOrEmpty(CommandLineArgs.NetMode))
            {
                var nm = UnityEngine.Object.FindAnyObjectByType<CqwNetworkManager>();
                var sm = SessionManager.Instance;
                if (nm != null && sm != null)
                {
                    launched = true;
                    if (!string.IsNullOrEmpty(CommandLineArgs.Address))
                    {
                        nm.networkAddress = SplitHost(CommandLineArgs.Address, out var port);
                        if (port > 0 && nm.transport is kcp2k.KcpTransport kcp) kcp.port = (ushort)port;
                    }
                    switch (CommandLineArgs.NetMode)
                    {
                        case "host": sm.CreateSession(); break;
                        case "client": sm.JoinSession(nm.networkAddress); break;
                        case "server": nm.StartServer(); break;
                        default: Debug.LogWarning($"[CmdArgs] 未知 netmode：{CommandLineArgs.NetMode}"); break;
                    }
                    if (!string.IsNullOrEmpty(CommandLineArgs.LobbyId))
                        Debug.Log($"[CmdArgs] lobby={CommandLineArgs.LobbyId}（Steam 大厅直连占位，阶段十二联调）");
                    Debug.Log($"[CmdArgs] netmode 已应用：{CommandLineArgs.Describe()}");
                }
                else if (Time.unscaledTime > deadline)
                {
                    launched = true;
                    Debug.LogWarning("[CmdArgs] 等待超时，netmode 未应用（NM/SessionManager 未就绪）。");
                }
            }

            // automove：本地玩家就绪后注入
            if (CommandLineArgs.Automove && !automoveApplied && PlayerRegistry.GetAll().Count > 0)
            {
                var player = PlayerRegistry.GetAll()[0];
                var identity = player.GetComponent<Mirror.NetworkIdentity>();
                if (identity != null && identity.isOwned)
                {
                    automoveApplied = true;
                    var src = new AutoMoveInputSource();
                    player.GetComponent<PlayerController>()?.SetInputSource(src);
                    player.GetComponent<PlayerCameraController>()?.SetInputSource(src);
                    Debug.Log("[CmdArgs] automove 已注入本地玩家。");
                }
            }
        }

        static string SplitHost(string address, out int port)
        {
            port = 0;
            int i = address.LastIndexOf(':');
            if (i > 0 && int.TryParse(address.Substring(i + 1), out port))
                return address.Substring(0, i);
            return address;
        }
    }

    /// <summary>自动移动输入源（--automove 测试用）：圆形轨迹移动 + 周期跳跃。</summary>
    public class AutoMoveInputSource : IPlayerInputSource
    {
        float t;
        float nextJump;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Interact => false;
        public bool Attack => false;
        public bool Defend => false;
        public bool PushToTalk => false;
        public bool Jump { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CameraSwitchPressed => false;
        public bool IsEnabled { get; private set; } = true;

        public string DeviceId => "automove";

        public void SetEnabled(bool value) => IsEnabled = value;

        public void Tick()
        {
            if (!IsEnabled) { Move = Vector2.zero; Jump = JumpPressed = false; return; }
            t += Time.deltaTime;
            var dir = new Vector2(Mathf.Cos(t * 0.8f), Mathf.Sin(t * 0.8f));
            Move = dir.normalized;
            Jump = Time.unscaledTime >= nextJump;
            JumpPressed = Jump;
            if (Jump) nextJump = Time.unscaledTime + 2.5f;
        }
    }
}
