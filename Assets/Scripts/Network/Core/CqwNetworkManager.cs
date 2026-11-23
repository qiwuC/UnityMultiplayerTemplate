using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// Project NetworkManager: persistent (DontDestroyOnLoad), applies transport layer from NetworkConfig,
    /// exposes connection lifecycle events for SessionManager / ReconnectManager to subscribe.
    /// </summary>
    [AddComponentMenu("Cqw/Network/CqwNetworkManager")]
    public class CqwNetworkManager : NetworkManager
    {
        /// <summary>Network config (assignable in Inspector; falls back to default path at runtime if null).</summary>
        [Header("Cqw Network Config")]
        [Tooltip("Network config asset. If empty, attempts to load from default path on StartServer/StartClient")]
        public NetworkConfig Config;

        public override void Awake()
        {
            if (Config == null)
                Config = FindConfig();
            // Transport must be ready before base.Awake():
            // NetworkManager.InitializeSingleton (inside base.Awake) requires transport to be set.
            if (transport == null && Config != null && !NetworkServer.active && !NetworkClient.active)
            {
                try
                {
                    var kcp = GetComponent<kcp2k.KcpTransport>() ?? gameObject.AddComponent<kcp2k.KcpTransport>();
                    var fizzy = GetComponent<Mirror.FizzySteam.FizzySteamworks>() ?? gameObject.AddComponent<Mirror.FizzySteam.FizzySteamworks>();
                    if (kcp.port == 0 && Config != null) kcp.port = Config.KcpPort;
                    switch (Config.Transport)
                    {
                        case TransportMode.Kcp: transport = kcp; break;
                        case TransportMode.FizzySteamworks: transport = fizzy; break;
                    }
                    Debug.Log($"[CqwNetwork] Awake transport pre-configured: {NetworkTransportSwitcher.DescribeActive(this)}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CqwNetwork] Awake transport setup failed: {e.Message}", this);
                }
            }
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Loads config from default asset path (Editor; build must use Inspector reference).</summary>
        public NetworkConfig FindConfig()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<NetworkConfig>(NetworkConfig.DefaultAssetPath);
#else
            return null; // Build must have explicit reference
#endif
        }

        /// <summary>Applies transport layer from config (safe when session not started).</summary>
        public void ApplyTransport()
        {
            if (Config == null || NetworkServer.active || NetworkClient.active) return;
            try
            {
                var desc = NetworkTransportSwitcher.Apply(Config, this);
                Debug.Log($"[CqwNetwork] Transport applied: {desc}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CqwNetwork] Transport apply failed: {e.Message}", this);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[CqwNetwork] Server started transport={NetworkTransportSwitcher.DescribeActive(this)} addr={networkAddress} max={maxConnections} proto={(Config != null ? Config.ProtocolVersion.ToString() : "?")}");

            // P-01: spawn session state root (NetworkPauseState pause broadcast)
            if (NetworkPauseState.Instance == null)
            {
                var sessionRoot = new GameObject("SessionStateRoot");
                sessionRoot.AddComponent<NetworkIdentity>();
                sessionRoot.AddComponent<NetworkPauseState>();
                NetworkServer.Spawn(sessionRoot);
                Debug.Log("[CqwNetwork] SessionStateRoot spawned (NetworkPauseState).");
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log($"[CqwNetwork] Client started addr={networkAddress} proto={(Config != null ? Config.ProtocolVersion.ToString() : "?")}");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Debug.Log("[CqwNetwork] Server stopped.");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            Debug.Log("[CqwNetwork] Client stopped.");
        }

        /// <summary>Server: client connection log (protocol validation handled by ProtocolAuthenticator).</summary>
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[CqwNetwork] Server: connection #{conn.connectionId} (awaiting protocol auth)");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[CqwNetwork] Server: connection #{conn.connectionId} disconnected");
            base.OnServerDisconnect(conn);
        }

        public override void OnClientDisconnect()
        {
            Debug.Log("[CqwNetwork] Client: disconnected from server.");
            base.OnClientDisconnect();
            OnClientDisconnectedBridge?.Invoke();
        }

        /// <summary>Client disconnect bridge event (ReconnectManager subscribes; Mirror static event timing unreliable).</summary>
        public event System.Action OnClientDisconnectedBridge;

        /// <summary>
        /// Server: add player using RoundRobinSpawner rotation (NetworkConfig priority -> SpawnPoint components -> Mirror StartPositions).
        /// Mirror auto-destroys player object on disconnect (default behavior).
        /// Note: Identity SyncVars must be set before AddPlayerForConnection (spawn) —
        /// spawn initial state snapshot is generated at spawn time; later changes are overwritten by spawn payload.
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var spTransform = RoundRobinSpawner.GetNext(Config);
            var pos = spTransform != null
                ? RoundRobinSpawner.PositionWithJitter(spTransform)
                : Vector3.zero;
            var rot = spTransform != null ? spTransform.rotation : Quaternion.identity;

            var player = Instantiate(playerPrefab, pos, rot);

            // ===== Configure identity before spawn (enters initial state snapshot) =====
            var identity = player.GetComponent<PlayerIdentity>();
            if (identity != null)
            {
                identity.SetNickname(PlayerNicknameCache.Get(conn));
                // SteamID: use real value if Steam available; Host/offline uses placeholder to ensure non-null (Phase 3 constraint)
                identity.SetSteamId(ResolveSteamId(conn));
                // Team: auto-assign if TeamManager exists (Phase 3 T3-07)
                var teamMgr = UnityEngine.Object.FindAnyObjectByType<Player.TeamManager>();
                if (teamMgr != null) teamMgr.AutoAssign(identity);
            }

            NetworkServer.AddPlayerForConnection(conn, player);
            Debug.Log($"[CqwNetwork] Player spawned conn#{conn.connectionId} @ {pos} steamId={identity?.SteamId} team={identity?.TeamId}");
        }

        /// <summary>Resolve player SteamID: use real ID if Steam initialized, otherwise offline placeholder (ensures non-null).</summary>
        static string ResolveSteamId(NetworkConnectionToClient conn)
        {
            var steam = Steam.CqwSteamManager.Instance;
            if (steam != null && steam.IsInitialized)
                return steam.SteamId.m_SteamID.ToString();
            // Placeholder: offline:<connectionId> (local/no Steam environment; do not modify AppID placeholder)
            return $"offline:{conn.connectionId}";
        }
    }
}
