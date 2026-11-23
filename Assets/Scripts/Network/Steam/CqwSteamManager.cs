#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.IO;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network.Steam
{
    /// <summary>
    /// Steamworks 常驻单例：负责 SteamAPI.Init / RunCallbacks / Shutdown 生命周期。
    /// 与官方 SteamManager 不同，本类在初始化失败时不会退出应用（Editor/开发环境友好），
    /// 只记录明确原因并保持 IsInitialized=false。
    /// 使用者（SteamLobbyManager 等）应先检查 IsInitialized。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Cqw/Network/CqwSteamManager")]
    public class CqwSteamManager : MonoBehaviour
    {
        public static CqwSteamManager Instance { get; private set; }

        /// <summary>当前 AppID（来自 steam_appid.txt，供诊断显示）。</summary>
        public uint AppId { get; private set; }

        /// <summary>初始化失败的明确原因（成功时为 null）。</summary>
        public string InitFailureReason { get; private set; }

        bool shutdownDone;

#if !DISABLESTEAMWORKS
        // ---- 状态属性 ----
        public bool IsInitialized { get; private set; }

        /// <summary>当前用户 SteamID（未初始化时为 CSteamID.Nil）。</summary>
        public Steamworks.CSteamID SteamId =>
            IsInitialized ? Steamworks.SteamUser.GetSteamID() : Steamworks.CSteamID.Nil;

        /// <summary>当前用户昵称（未初始化时为 null）。</summary>
        public string PersonaName =>
            IsInitialized ? Steamworks.SteamFriends.GetPersonaName() : null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        void Initialize()
        {
            AppId = ReadSteamAppId();

            // 基础二进制自检
            try
            {
                if (!Steamworks.Packsize.Test())
                    Debug.LogError("[CqwSteam] Packsize.Test 失败：Steamworks.NET 二进制与当前平台不匹配。", this);
                if (!Steamworks.DllCheck.Test())
                    Debug.LogError("[CqwSteam] DllCheck.Test 失败：steam_api dll 缺失或版本错误。", this);
            }
            catch (Exception e)
            {
                InitFailureReason = $"Steamworks 二进制加载失败：{e.Message}（steam_api64.dll 是否随包正确安装？）";
                Debug.LogError($"[CqwSteam] {InitFailureReason}", this);
                return;
            }

            // 环境预检：Steam 客户端是否在运行
            bool steamRunning = false;
            try { steamRunning = Steamworks.SteamAPI.IsSteamRunning(); }
            catch (Exception e) { InitFailureReason = $"IsSteamRunning 检查异常：{e.Message}"; }

            if (!steamRunning && InitFailureReason == null)
            {
                InitFailureReason = "Steam 客户端未运行。SteamAPI.Init 需要正在运行的 Steam 客户端。";
                Debug.LogWarning($"[CqwSteam] 初始化跳过：{InitFailureReason}（AppID={AppId}，steam_appid.txt 占位 480 时需本机 Steam 在线）", this);
                return;
            }

            // 初始化
            try
            {
                IsInitialized = Steamworks.SteamAPI.Init();
            }
            catch (Exception e)
            {
                InitFailureReason = $"SteamAPI.Init 抛出异常：{e.Message}";
                IsInitialized = false;
            }

            if (IsInitialized)
            {
                InitFailureReason = null;
                Debug.Log($"[CqwSteam] SteamAPI 初始化成功。AppID={AppId} SteamID={SteamId} 昵称={PersonaName}", this);
            }
            else
            {
                if (InitFailureReason == null)
                    InitFailureReason = "SteamAPI.Init() 返回 false（常见原因：Steam 未运行 / AppID 无授权 / steam_appid.txt 缺失或值无效 / 未在同用户上下文运行）。";
                Debug.LogWarning($"[CqwSteam] 初始化失败：{InitFailureReason}", this);
            }
        }

        void Update()
        {
            if (IsInitialized)
                Steamworks.SteamAPI.RunCallbacks();
        }

        void OnApplicationQuit() => ShutdownSteam();

        void OnDestroy()
        {
            if (Instance == this)
            {
                ShutdownSteam();
                Instance = null;
            }
        }

        void ShutdownSteam()
        {
            if (shutdownDone || !IsInitialized) return;
            shutdownDone = true;
            Steamworks.SteamAPI.Shutdown();
            IsInitialized = false;
            Debug.Log("[CqwSteam] SteamAPI.Shutdown 完成。", this);
        }

        /// <summary>读取 steam_appid.txt（工作目录）。找不到或解析失败返回 0。</summary>
        uint ReadSteamAppId()
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path).Trim();
                    if (uint.TryParse(text, out var id))
                    {
                        if (id == 480)
                            Debug.LogWarning("[CqwSteam] steam_appid.txt 当前为占位 AppID 480 (Spacewar)，仅用于开发测试。", this);
                        return id;
                    }
                    Debug.LogWarning($"[CqwSteam] steam_appid.txt 内容无法解析为 AppID：\"{text}\"", this);
                }
                else
                {
                    Debug.LogWarning($"[CqwSteam] 未找到 steam_appid.txt（查找路径：{path}）。SteamAPI.Init 将失败。", this);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CqwSteam] 读取 steam_appid.txt 异常：{e.Message}", this);
            }
            return 0;
        }
#else
        public bool IsInitialized => false;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitFailureReason = "当前平台未启用 Steamworks（DISABLESTEAMWORKS）。";
            Debug.LogWarning($"[CqwSteam] {InitFailureReason}", this);
        }
#endif
    }
}
