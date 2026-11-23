using System;
using System.Collections;
using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network.Steam;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// Client auto-reconnect: on unexpected disconnect, retries based on NetworkConfig (AutoReconnect/ReconnectDelay/ReconnectMaxAttempts)
    /// and restores nickname; intentional leave (SessionManager.LeaveSession / MarkIntentionalLeave) does not trigger.
    /// Mounted as MonoBehaviour, receives disconnect notification via CqwNetworkManager.OnClientDisconnectBridge.
    /// </summary>
    [AddComponentMenu("Cqw/Network/ReconnectManager")]
    public class ReconnectManager : MonoBehaviour
    {
        /// <summary>Reconnect attempt started (arg = attempt number, starting from 1).</summary>
        public event Action<int> OnReconnectAttempt;
        /// <summary>Reconnect succeeded.</summary>
        public event Action OnReconnectSuccess;
        /// <summary>Reconnect definitively failed (exceeded max attempts).</summary>
        public event Action<int> OnReconnectFailed;
        /// <summary>Unexpected disconnect (reconnect flow about to start).</summary>
        public event Action OnUnexpectedDisconnect;

        public bool IsReconnecting { get; private set; }
        public int AttemptsUsed { get; private set; }

        CqwNetworkManager nm;
        NetworkConfig Config => nm != null ? nm.Config : null;
        string lastAddress;
        bool intentionalLeave;
        Coroutine reconnectRoutine;

        void Awake()
        {
            nm = GetComponent<CqwNetworkManager>();
            if (nm == null) nm = FindAnyObjectByType<CqwNetworkManager>();
            if (nm != null) nm.OnClientDisconnectedBridge += HandleDisconnected;
        }

        void OnDestroy()
        {
            if (nm != null) nm.OnClientDisconnectedBridge -= HandleDisconnected;
        }

        void OnEnable() => SessionManager.OnSessionLeftIntentionally += MarkIntentionalLeave;
        void OnDisable() => SessionManager.OnSessionLeftIntentionally -= MarkIntentionalLeave;

        /// <summary>Call before client initiates connection: records target address (reconnect target).</summary>
        public void RememberTarget(string address) => lastAddress = address;

        /// <summary>Triggered by SessionManager event on intentional leave; can also be called manually.</summary>
        public void MarkIntentionalLeave() => intentionalLeave = true;

        void HandleDisconnected()
        {
            // Host stop (server also running) = intentional; only client standalone disconnect triggers reconnect
            bool wasHost = NetworkServer.active;
            if (intentionalLeave || wasHost)
            {
                intentionalLeave = false;
                return;
            }

            // Re-entrancy guard: failure disconnect inside reconnect loop does not restart the loop
            if (IsReconnecting) return;

            var cfg = Config;
            if (cfg == null || !cfg.AutoReconnect)
            {
                Debug.Log("[Reconnect] Auto-reconnect not enabled, aborting.");
                return;
            }

            OnUnexpectedDisconnect?.Invoke();
            if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);
            reconnectRoutine = StartCoroutine(ReconnectLoop(cfg));
        }

        IEnumerator ReconnectLoop(NetworkConfig cfg)
        {
            IsReconnecting = true;
            AttemptsUsed = 0;
            string nickname = PlayerNicknameCache.GetLocal();

            while (AttemptsUsed < cfg.ReconnectMaxAttempts)
            {
                AttemptsUsed++;
                // Delay + random jitter (prevents reconnect avalanche)
                float delay = cfg.ReconnectDelay * (0.75f + UnityEngine.Random.value * 0.5f);
                Debug.Log($"[Reconnect] Attempt {AttemptsUsed}/{cfg.ReconnectMaxAttempts}: trying {lastAddress} in {delay:F1}s...");
                OnReconnectAttempt?.Invoke(AttemptsUsed);
                yield return new WaitForSeconds(delay);

                if (intentionalLeave) { intentionalLeave = false; break; }

                nm.networkAddress = string.IsNullOrEmpty(lastAddress) ? "localhost" : lastAddress;
                nm.StartClient();

                // Wait for connection result (max 10s; another disconnect during wait = this attempt failed)
                float t = 0;
                bool failed = false;
                while (t < 10f)
                {
                    if (NetworkClient.isConnected) break;
                    // KCP connection failure triggers OnClientDisconnect -> bridge arrives with intentionalLeave=false
                    // and NetworkClient.active is already false at that point
                    if (!NetworkClient.active) { failed = true; break; }
                    t += Time.deltaTime;
                    yield return null;
                }

                if (!failed && NetworkClient.isConnected)
                {
                    Debug.Log($"[Reconnect] Reconnected (attempt {AttemptsUsed}), restoring nickname: {nickname}");
                    IsReconnecting = false;
                    OnReconnectSuccess?.Invoke();
                    // Nickname restore: wait for local player to spawn then send Command
                    StartCoroutine(RestoreNicknameWhenReady(nickname));
                    yield break;
                }

                if (NetworkClient.active) nm.StopClient();
                Debug.LogWarning($"[Reconnect] Attempt {AttemptsUsed} failed.");
                yield return new WaitForSeconds(0.3f);
            }

            IsReconnecting = false;
            Debug.LogError($"[Reconnect] Exceeded max reconnect attempts ({cfg.ReconnectMaxAttempts}), giving up. UI should notify user.");
            OnReconnectFailed?.Invoke(AttemptsUsed);
        }

        IEnumerator RestoreNicknameWhenReady(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) yield break;
            // Wait for local player to spawn (max 10s)
            float t = 0;
            while (t < 10f && (NetworkClient.localPlayer == null || !NetworkClient.localPlayer.GetComponent<PlayerIdentity>()))
            {
                t += Time.deltaTime;
                yield return null;
            }
            var identity = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<PlayerIdentity>() : null;
            if (identity != null)
            {
                identity.CmdRestoreNickname(nickname);
                Debug.Log($"[Reconnect] Nickname restore request sent: {nickname}");
            }
            else Debug.LogWarning("[Reconnect] Local player not ready, nickname handled by OnServerAddPlayer cache fallback.");
        }
    }
}
