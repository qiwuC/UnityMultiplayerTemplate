using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.UI
{
    public enum ChatChannel { Global, Team }

    public struct ChatBroadcastMessage : NetworkMessage
    {
        public string SenderName;
        public int Channel;
        public string Text;
    }

    public struct ChatRequestMessage : NetworkMessage
    {
        public int Channel;
        public string Text;
    }

    public static class ChatNetwork
    {
        public static event Action<ChatBroadcastMessage> OnChatReceived;
        static bool registered;

        public static void EnsureRegistered()
        {
            if (registered) return;
            registered = true;
            if (NetworkServer.active)
                NetworkServer.RegisterHandler<ChatRequestMessage>(OnServerChatRequest, false);
            if (NetworkClient.active)
                NetworkClient.RegisterHandler<ChatBroadcastMessage>(OnClientChatBroadcast, false);
        }

        public static void ClientSend(ChatChannel channel, string text)
        {
            if (!NetworkClient.active || !NetworkClient.isConnected)
            {
                Debug.LogWarning("[Chat] Not connected, cannot send.");
                return;
            }
            NetworkClient.Send(new ChatRequestMessage { Channel = (int)channel, Text = text });
        }

        static void OnServerChatRequest(NetworkConnectionToClient conn, ChatRequestMessage msg, int channelId)
        {
            var identity = PlayerRegistry.GetByConnection(conn);
            var senderName = identity != null ? identity.DisplayName : "Player_" + conn.connectionId;
            var senderTeam = identity != null ? identity.TeamId : -1;
            BroadcastToReceivers(senderName, (ChatChannel)msg.Channel, msg.Text, senderTeam, conn);
        }

        static void BroadcastToReceivers(string senderName, ChatChannel channel, string text, int senderTeam, NetworkConnectionToClient senderConn)
        {
            var msg = new ChatBroadcastMessage { SenderName = senderName, Channel = (int)channel, Text = text };
            if (channel == ChatChannel.Global)
            {
                NetworkServer.SendToAll(msg);
            }
            else
            {
                foreach (var m in PlayerRegistry.GetByTeam(senderTeam))
                    if (m != null && m.connectionToClient != null)
                        m.connectionToClient.Send(msg);
            }
        }

        static void OnClientChatBroadcast(ChatBroadcastMessage msg)
        {
            OnChatReceived?.Invoke(msg);
        }

        public static void ServerBroadcast(string senderName, ChatChannel channel, string text, int senderTeam)
        {
            var msg = new ChatBroadcastMessage { SenderName = senderName, Channel = (int)channel, Text = text };
            OnChatReceived?.Invoke(msg);
            NetworkServer.SendToAll(msg);
        }
    }
}
