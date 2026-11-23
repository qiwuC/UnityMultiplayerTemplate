using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 协议版本认证器（参照 Mirror BasicAuthenticator 消息模式）：
    /// 客户端连接时发送协议版本，服务端比对 NetworkConfig.ProtocolVersion，
    /// 不匹配发送拒绝消息并断开；匹配发送接受消息完成认证。
    /// </summary>
    [AddComponentMenu("Cqw/Network/ProtocolAuthenticator")]
    public class ProtocolAuthenticator : NetworkAuthenticator
    {
        /// <summary>协议版本源：优先 NetworkConfig，Inspector 可覆盖（0 = 使用 Config）。</summary>
        [Tooltip("协议版本。0 表示运行时从 NetworkConfig 读取")]
        public int overrideProtocolVersion;

        readonly HashSet<NetworkConnectionToClient> connectionsPendingDisconnect =
            new HashSet<NetworkConnectionToClient>();

        /// <summary>认证版本号（Config 优先）。</summary>
        int Version =>
            overrideProtocolVersion != 0
                ? overrideProtocolVersion
                : (GetComponent<NetworkManager>() is CqwNetworkManager cqm && cqm.Config != null
                    ? cqm.Config.ProtocolVersion
                    : overrideProtocolVersion);

        #region 消息定义

        public struct ProtocolRequestMessage : NetworkMessage
        {
            public int protocolVersion;
        }

        public struct ProtocolResponseMessage : NetworkMessage
        {
            public byte code; // 100=接受 200=版本不匹配 201=超时/格式错误
            public string message;
            public int serverVersion;
        }

        #endregion

        #region 服务端

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<ProtocolRequestMessage>(OnProtocolRequest, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<ProtocolRequestMessage>();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            // 等待客户端的 ProtocolRequestMessage
        }

        void OnProtocolRequest(NetworkConnectionToClient conn, ProtocolRequestMessage msg)
        {
            if (connectionsPendingDisconnect.Contains(conn)) return;

            int serverVersion = Version;
            if (msg.protocolVersion == serverVersion)
            {
                conn.Send(new ProtocolResponseMessage
                {
                    code = 100, message = "Protocol OK", serverVersion = serverVersion
                });
                Debug.Log($"[ProtocolAuth] 连接 #{conn.connectionId} 协议版本 {msg.protocolVersion} 匹配，已接受。");
                ServerAccept(conn);
            }
            else
            {
                connectionsPendingDisconnect.Add(conn);
                conn.Send(new ProtocolResponseMessage
                {
                    code = 200,
                    message = $"Protocol mismatch: client={msg.protocolVersion} server={serverVersion}",
                    serverVersion = serverVersion
                });
                conn.isAuthenticated = false;
                Debug.LogWarning($"[ProtocolAuth] 连接 #{conn.connectionId} 协议版本不匹配（client={msg.protocolVersion} server={serverVersion}），拒绝并断开。");
                StartCoroutine(DelayedDisconnect(conn, 1f));
            }
        }

        IEnumerator DelayedDisconnect(NetworkConnectionToClient conn, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            ServerReject(conn);
            connectionsPendingDisconnect.Remove(conn);
        }

        #endregion

        #region 客户端

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<ProtocolResponseMessage>(OnProtocolResponse, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<ProtocolResponseMessage>();
        }

        public override void OnClientAuthenticate()
        {
            int clientVersion = Version;
            NetworkClient.Send(new ProtocolRequestMessage { protocolVersion = clientVersion });
            Debug.Log($"[ProtocolAuth] 客户端发送协议版本 {clientVersion}，等待服务端校验。");
        }

        void OnProtocolResponse(ProtocolResponseMessage msg)
        {
            if (msg.code == 100)
            {
                Debug.Log($"[ProtocolAuth] 服务端接受（server v{msg.serverVersion}）。");
                ClientAccept();
            }
            else
            {
                Debug.LogError($"[ProtocolAuth] 被拒绝：{msg.message}");
                ClientReject();
            }
        }

        #endregion
    }
}
