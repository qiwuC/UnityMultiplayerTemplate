#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
using Cqw.MultilayerFramework.Network.Steam;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>大厅可见性（数值与 Steamworks ELobbyType 一一对应）。</summary>
    public enum LobbyVisibility
    {
        InvitesOnly = 0,  // k_ELobbyTypePrivate：仅邀请
        FriendsOnly = 1,  // k_ELobbyTypeFriendsOnly：好友可见可加入
        Public = 2        // k_ELobbyTypePublic：公开可见可加入（注意 3 是 Invisible）
    }

    /// <summary>
    /// Steam 大厅管理：创建/加入/离开/邀请，可见性枚举映射，
    /// 大厅数据（房主 SteamID、协议版本、房主昵称），成员变更事件。
    /// 所有操作前置检查 CqwSteamManager.IsInitialized，未初始化时给出明确错误并返回 false。
    /// </summary>
    [AddComponentMenu("Cqw/Network/SteamLobbyManager")]
    public class SteamLobbyManager : MonoBehaviour
    {
        /// <summary>当前大厅（不在大厅时为 Nil）。</summary>
        public Steamworks.CSteamID CurrentLobby { get; private set; } = new Steamworks.CSteamID();

        public bool InLobby => CurrentLobby != Steamworks.CSteamID.Nil;

        // ---- 事件 ----
        public event Action<Steamworks.CSteamID> OnLobbyCreated;
        public event Action<Steamworks.CSteamID> OnLobbyJoined;
        public event Action OnLobbyLeft;
        /// <summary>成员变更（变更掩码：1=进入 2=离开 8=断线 16=踢出 等）。</summary>
        public event Action<Steamworks.CSteamID, Steamworks.EChatMemberStateChange> OnLobbyMemberChanged;
        /// <summary>大厅创建失败（含 Steam 结果码）。</summary>
        public event Action<Steamworks.EResult> OnLobbyCreateFailed;
        /// <summary>加入大厅失败（含响应码）。</summary>
        public event Action<uint> OnLobbyJoinFailed;

#if !DISABLESTEAMWORKS
        // Callback：被动通知（成员变更、数据更新、自动进入大厅）
        Steamworks.Callback<Steamworks.LobbyChatUpdate_t> chatUpdateCallback;
        Steamworks.Callback<Steamworks.LobbyEnter_t> enterCallback;

        // CallResult：主动调用结果（CreateLobby / JoinLobby 的 SteamAPICall_t）
        Steamworks.CallResult<Steamworks.LobbyCreated_t> createCall;
        Steamworks.CallResult<Steamworks.LobbyEnter_t> joinCall;

        int pendingProtocolVersion;

        void OnEnable()
        {
            var steam = CqwSteamManager.Instance;
            if (steam == null || !steam.IsInitialized)
            {
                Debug.LogWarning("[SteamLobby] CqwSteamManager 未初始化，大厅回调不注册（原因：" +
                    (steam == null ? "实例不存在" : steam.InitFailureReason) + "）。");
                return;
            }

            chatUpdateCallback = Steamworks.Callback<Steamworks.LobbyChatUpdate_t>.Create(OnChatUpdate);
            enterCallback = Steamworks.Callback<Steamworks.LobbyEnter_t>.Create(OnLobbyEnter);
        }

        void OnDisable()
        {
            chatUpdateCallback?.Dispose();
            enterCallback?.Dispose();
            chatUpdateCallback = null;
            enterCallback = null;
        }

        void EnsureCallResults()
        {
            createCall ??= new Steamworks.CallResult<Steamworks.LobbyCreated_t>(OnCreateResult);
            joinCall ??= new Steamworks.CallResult<Steamworks.LobbyEnter_t>(OnJoinResult);
        }

        // ============ 公开 API ============

        /// <summary>创建大厅（异步：成功→OnLobbyCreated，失败→OnLobbyCreateFailed）。</summary>
        public bool CreateLobby(LobbyVisibility visibility, int maxMembers, int protocolVersion)
        {
            if (!EnsureSteam()) return false;
            if (InLobby) { Debug.LogWarning("[SteamLobby] 已在大厅中，请先离开再创建。"); return false; }

            EnsureCallResults();
            pendingProtocolVersion = protocolVersion;
            var type = (Steamworks.ELobbyType)(int)visibility;
            var call = Steamworks.SteamMatchmaking.CreateLobby(type, maxMembers);
            createCall.Set(call);
            Debug.Log($"[SteamLobby] 请求创建大厅：可见性={visibility}({type}) 上限={maxMembers} 协议=v{protocolVersion}…");
            return true;
        }

        /// <summary>加入大厅（异步：成功→OnLobbyJoined，失败→OnLobbyJoinFailed）。</summary>
        public bool JoinLobby(Steamworks.CSteamID lobbyId)
        {
            if (!EnsureSteam()) return false;
            if (InLobby) { Debug.LogWarning("[SteamLobby] 已在大厅中，请先离开再加入。"); return false; }

            EnsureCallResults();
            var call = Steamworks.SteamMatchmaking.JoinLobby(lobbyId);
            joinCall.Set(call);
            Debug.Log($"[SteamLobby] 请求加入大厅 {lobbyId}…");
            return true;
        }

        /// <summary>离开当前大厅（同步）。</summary>
        public bool LeaveLobby()
        {
            if (!EnsureSteam()) return false;
            if (!InLobby) return false;

            Steamworks.SteamMatchmaking.LeaveLobby(CurrentLobby);
            Debug.Log($"[SteamLobby] 已离开大厅 {CurrentLobby}。");
            CurrentLobby = new Steamworks.CSteamID();
            OnLobbyLeft?.Invoke();
            return true;
        }

        /// <summary>打开 Steam 邀请覆盖层（向好友邀请进入当前大厅）。</summary>
        public bool OpenInviteOverlay()
        {
            if (!EnsureSteam()) return false;
            if (!InLobby) { Debug.LogWarning("[SteamLobby] 不在大厅中，无法打开邀请覆盖。"); return false; }

            Steamworks.SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
            return true;
        }

        /// <summary>写入会话关键大厅数据：房主 SteamID / 协议版本 / 房主昵称。</summary>
        public bool SetSessionLobbyData(string hostSteamId, int protocolVersion, string hostNickname)
        {
            if (!EnsureSteam() || !InLobby) return false;
            bool ok = Steamworks.SteamMatchmaking.SetLobbyData(CurrentLobby, "host", hostSteamId ?? "")
                   & Steamworks.SteamMatchmaking.SetLobbyData(CurrentLobby, "protocol", protocolVersion.ToString())
                   & Steamworks.SteamMatchmaking.SetLobbyData(CurrentLobby, "hostname", hostNickname ?? "Host");
            return ok;
        }

        /// <summary>读取大厅协议版本（缺失返回 int.MinValue）。</summary>
        public int GetLobbyProtocolVersion(Steamworks.CSteamID lobbyId)
        {
            if (!EnsureSteam()) return int.MinValue;
            return int.TryParse(Steamworks.SteamMatchmaking.GetLobbyData(lobbyId, "protocol"), out var v)
                ? v : int.MinValue;
        }

        /// <summary>读取房主 SteamID 字符串（缺失返回 null）。</summary>
        public string GetLobbyHost(Steamworks.CSteamID lobbyId)
        {
            if (!EnsureSteam()) return null;
            var raw = Steamworks.SteamMatchmaking.GetLobbyData(lobbyId, "host");
            return string.IsNullOrEmpty(raw) ? null : raw;
        }

        // ============ 回调 ============

        // CreateLobby 的 CallResult
        void OnCreateResult(Steamworks.LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != Steamworks.EResult.k_EResultOK)
            {
                Debug.LogError($"[SteamLobby] 大厅创建失败：{result.m_eResult}");
                OnLobbyCreateFailed?.Invoke(result.m_eResult);
                return;
            }
            CurrentLobby = new Steamworks.CSteamID(result.m_ulSteamIDLobby);
            Debug.Log($"[SteamLobby] 大厅创建成功：{CurrentLobby}");
            // 创建者自动进入大厅（后续 OnLobbyEnter 触发 Joined 事件）
            OnLobbyCreated?.Invoke(CurrentLobby);
        }

        // JoinLobby 的 CallResult
        void OnJoinResult(Steamworks.LobbyEnter_t result, bool ioFailure)
        {
            HandleLobbyEnter(result, ioFailure);
        }

        // Callback：CreateLobby 后自动进入大厅的通知
        void OnLobbyEnter(Steamworks.LobbyEnter_t result)
        {
            HandleLobbyEnter(result, false);
        }

        void HandleLobbyEnter(Steamworks.LobbyEnter_t result, bool ioFailure)
        {
            if (ioFailure || result.m_EChatRoomEnterResponse != (uint)Steamworks.EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.LogError($"[SteamLobby] 进入大厅失败：code={result.m_EChatRoomEnterResponse}");
                OnLobbyJoinFailed?.Invoke(result.m_EChatRoomEnterResponse);
                return;
            }
            CurrentLobby = new Steamworks.CSteamID(result.m_ulSteamIDLobby);
            Debug.Log($"[SteamLobby] 已进入大厅 {CurrentLobby}（权限={result.m_rgfChatPermissions}）");
            OnLobbyJoined?.Invoke(CurrentLobby);
        }

        void OnChatUpdate(Steamworks.LobbyChatUpdate_t result)
        {
            var change = (Steamworks.EChatMemberStateChange)result.m_rgfChatMemberStateChange;
            Debug.Log($"[SteamLobby] 成员变更：lobby={result.m_ulSteamIDLobby} user={result.m_ulSteamIDUserChanged} change={change}");
            OnLobbyMemberChanged?.Invoke(new Steamworks.CSteamID(result.m_ulSteamIDUserChanged), change);
        }

        bool EnsureSteam()
        {
            var steam = CqwSteamManager.Instance;
            if (steam != null && steam.IsInitialized) return true;
            Debug.LogWarning("[SteamLobby] 操作取消：Steam 未初始化（" +
                (steam == null ? "CqwSteamManager 不存在" : steam.InitFailureReason) + "）");
            return false;
        }
#endif
    }
}
