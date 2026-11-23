using System;
using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network.Steam;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>Session state.</summary>
    public enum SessionState
    {
        Idle,       // Not in session
        Hosting,    // Acting as host (server + local client)
        Connecting, // Connecting
        InSession,  // In session (client)
        Reconnecting
    }

    /// <summary>Pause mode.</summary>
    public enum PauseMode
    {
        /// <summary>Flag only (isPaused + event), does not affect time scale, network sync continues.</summary>
        FlagOnly,
        /// <summary>Flag + Time.timeScale=0 (UI/animation using unscaled still responds, physics and regular Update stop).</summary>
        TimeScale
    }

    /// <summary>
    /// Session management: coordinates CqwNetworkManager and SteamLobbyManager.
    /// CreateSession=Host+create lobby (KCP direct or Steam P2P); JoinSession=connect (Steam mode joins lobby first).
    /// KickPlayer/PauseSession/ResumeSession only callable on server (NetworkServer.active).
    /// Pause broadcast via events; network-broadcast pause state deferred to Phase 3 (session state sync object).
    /// </summary>
    [AddComponentMenu("Cqw/Network/SessionManager")]
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("References (auto-found if empty)")]
        public CqwNetworkManager NetworkManagerRef;
        public SteamLobbyManager LobbyManager;

        [Header("Pause Config")]
        [Tooltip("Pause mode: FlagOnly=flag+event only; TimeScale=also sets timeScale to 0")]
        public PauseMode pauseMode = PauseMode.FlagOnly;

        /// <summary>Local session state (Host/Client unified).</summary>
        public SessionState State { get; private set; } = SessionState.Idle;

        /// <summary>Pause flag (server authoritative; client informed via event, network broadcast deferred to Phase 3).</summary>
        public bool IsPaused { get; private set; }

        // ---- Events ----
        public event Action<SessionState> OnSessionStateChanged;
        public event Action<bool> OnSessionPauseChanged;
        /// <summary>Intentional session leave (static broadcast, ReconnectManager listens to prevent reconnect).</summary>
        public static event Action OnSessionLeftIntentionally;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            NetworkManagerRef ??= FindAnyObjectByType<CqwNetworkManager>();
            LobbyManager ??= FindAnyObjectByType<SteamLobbyManager>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ============ Public API ============

        /// <summary>Create session (host): StartHost + (Steam mode and available) create lobby.</summary>
        public bool CreateSession(LobbyVisibility visibility = LobbyVisibility.FriendsOnly)
        {
            if (NetworkManagerRef == null) { Debug.LogError("[Session] NetworkManager not ready."); return false; }
            if (NetworkServer.active) { Debug.LogWarning("[Session] Already hosting."); return false; }

            ApplyPause(false, notify: false);
            NetworkManagerRef.StartHost();
            SetState(SessionState.Hosting);

            var config = NetworkManagerRef.Config;
            if (config != null && config.Transport == TransportMode.FizzySteamworks && LobbyManager != null)
            {
                var steam = CqwSteamManager.Instance;
                if (steam != null && steam.IsInitialized)
                {
                    LobbyManager.CreateLobby(visibility, config.MaxConnections, config.ProtocolVersion);
                    LobbyManager.OnLobbyCreated -= WriteLobbyDataOnce;
                    LobbyManager.OnLobbyCreated += WriteLobbyDataOnce;
                }
                else
                {
                    Debug.LogWarning("[Session] Steam not initialized, skipping lobby creation (local/direct session only).");
                }
            }
            return true;
        }

        void WriteLobbyDataOnce(Steamworks.CSteamID lobbyId)
        {
            LobbyManager.OnLobbyCreated -= WriteLobbyDataOnce;
            var steam = CqwSteamManager.Instance;
            if (steam == null) return;
            LobbyManager.SetSessionLobbyData(
                steam.SteamId.m_SteamID.ToString(),
                NetworkManagerRef.Config != null ? NetworkManagerRef.Config.ProtocolVersion : 0,
                steam.PersonaName ?? "Host");
        }

        /// <summary>
        /// Join session (client). KCP mode: pass IP; Steam mode: pass host SteamID string
        /// (will join lobby first to get info then connect — Phase 3 UI integration; currently used as connection address directly).
        /// </summary>
        public bool JoinSession(string address)
        {
            if (NetworkManagerRef == null) { Debug.LogError("[Session] NetworkManager not ready."); return false; }
            if (NetworkClient.active) { Debug.LogWarning("[Session] Client already in session."); return false; }

            NetworkManagerRef.networkAddress = address;
            NetworkManagerRef.StartClient();
            SetState(SessionState.Connecting);

            NetworkClient.OnConnectedEvent -= HandleClientConnected;
            NetworkClient.OnConnectedEvent += HandleClientConnected;
            return true;
        }

        /// <summary>Leave session (intentional). Host stops server + leaves lobby; client disconnects + leaves lobby.</summary>
        public bool LeaveSession()
        {
            if (NetworkManagerRef == null) return false;
            bool wasActive = NetworkServer.active || NetworkClient.active;

            if (NetworkServer.active) NetworkManagerRef.StopHost();
            else if (NetworkClient.active) NetworkManagerRef.StopClient();

            if (LobbyManager != null && LobbyManager.InLobby) LobbyManager.LeaveLobby();

            NetworkClient.OnConnectedEvent -= HandleClientConnected;
            SetState(SessionState.Idle);
            ApplyPause(false, notify: false);
            OnSessionLeftIntentionally?.Invoke();
            Debug.Log($"[Session] Left session (wasActive={wasActive}).");
            return true;
        }

        /// <summary>Kick player (server only; locates connection by netId).</summary>
        public bool KickPlayer(uint netId)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Session] KickPlayer is server-only.");
                return false;
            }
            if (NetworkServer.spawned.TryGetValue(netId, out var identity) && identity.connectionToClient != null)
            {
                Debug.Log($"[Session] Kicked player netId={netId} connId={identity.connectionToClient.connectionId}.");
                identity.connectionToClient.Disconnect();
                return true;
            }
            Debug.LogWarning($"[Session] Player connection not found for netId={netId}.");
            return false;
        }

        /// <summary>Kick player (by connection ID, server only).</summary>
        public bool KickPlayer(int connectionId)
        {
            if (!NetworkServer.active) { Debug.LogWarning("[Session] KickPlayer is server-only."); return false; }
            if (NetworkServer.connections.TryGetValue(connectionId, out var conn))
            {
                Debug.Log($"[Session] Kicked connection #{connectionId}.");
                conn.Disconnect();
                return true;
            }
            return false;
        }

        /// <summary>Pause session (server only).</summary>
        public void PauseSession()
        {
            if (!NetworkServer.active) { Debug.LogWarning("[Session] PauseSession is server-only."); return; }
            ApplyPause(true);
            Debug.Log($"[Session] Session paused (mode={pauseMode}).");
        }

        /// <summary>Resume session (server only).</summary>
        public void ResumeSession()
        {
            if (!NetworkServer.active) { Debug.LogWarning("[Session] ResumeSession is server-only."); return; }
            ApplyPause(false);
            Debug.Log("[Session] Session resumed.");
        }

        // ============ Internal ============

        void ApplyPause(bool paused, bool notify = true)
        {
            IsPaused = paused;
            // P-01: broadcast via NetworkPauseState SyncVar (client hook receives automatically)
            var pauseState = NetworkPauseState.Instance;
            if (pauseState != null && NetworkServer.active)
                pauseState.ServerSetPaused(paused);

            if (pauseMode == PauseMode.TimeScale)
                Time.timeScale = paused ? 0f : 1f;
            if (notify)
            {
                OnSessionPauseChanged?.Invoke(paused);
            }
        }

        void SetState(SessionState s)
        {
            State = s;
            OnSessionStateChanged?.Invoke(s);
        }

        void HandleClientConnected()
        {
            NetworkClient.OnConnectedEvent -= HandleClientConnected;
            SetState(SessionState.InSession);
        }
    }
}
