using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 昵称内存缓存（供断线重连/重新加入时恢复昵称，T2-08 使用）。
    /// 服务端按 connectionId 缓存；本地玩家昵称经 PlayerPrefs 持久化。
    /// </summary>
    public static class PlayerNicknameCache
    {
        const string PrefsKey = "Cqw.LocalPlayerNickname";

        static readonly Dictionary<int, string> byConnection = new Dictionary<int, string>();

        // ---- 本地玩家 ----

        public static string GetLocal()
        {
            return PlayerPrefs.GetString(PrefsKey, "");
        }

        public static void SetLocal(string nickname)
        {
            PlayerPrefs.SetString(PrefsKey, nickname ?? "");
            PlayerPrefs.Save();
        }

        // ---- 服务端按连接缓存 ----

        public static void Set(NetworkConnectionToClient conn, string nickname)
        {
            if (conn == null) return;
            if (string.IsNullOrWhiteSpace(nickname)) byConnection.Remove(conn.connectionId);
            else byConnection[conn.connectionId] = nickname;
        }

        public static string Get(NetworkConnectionToClient conn)
        {
            if (conn == null) return null;
            return byConnection.TryGetValue(conn.connectionId, out var n) ? n : null;
        }

        public static void Remove(NetworkConnectionToClient conn)
        {
            if (conn != null) byConnection.Remove(conn.connectionId);
        }
    }
}
