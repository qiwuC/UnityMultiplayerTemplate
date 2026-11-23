using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>
    /// 玩家注册表：生成/销毁回调维护（不扫描场景），双端可用。
    /// PlayerIdentity 注册自身；查询按 Team/SteamID/连接/本地玩家。
    /// </summary>
    public static class PlayerRegistry
    {
        static readonly List<PlayerIdentity> players = new List<PlayerIdentity>();
        static readonly object lockObj = new object();

        /// <summary>玩家注册（生成时调用）。</summary>
        public static event Action<PlayerIdentity> OnPlayerRegistered;
        /// <summary>玩家注销（销毁时调用）。</summary>
        public static event Action<PlayerIdentity> OnPlayerUnregistered;

        /// <summary>注册玩家（幂等）。</summary>
        public static void Register(PlayerIdentity player)
        {
            if (player == null) return;
            lock (lockObj)
            {
                if (players.Contains(player)) return;
                players.Add(player);
            }
            OnPlayerRegistered?.Invoke(player);
        }

        /// <summary>注销玩家（幂等）。</summary>
        public static void Unregister(PlayerIdentity player)
        {
            if (player == null) return;
            bool removed;
            lock (lockObj) { removed = players.Remove(player); }
            if (removed) OnPlayerUnregistered?.Invoke(player);
        }

        /// <summary>全部玩家快照（外部安全遍历）。</summary>
        public static List<PlayerIdentity> GetAll()
        {
            lock (lockObj) { return new List<PlayerIdentity>(players); }
        }

        /// <summary>玩家总数。</summary>
        public static int Count { get { lock (lockObj) { return players.Count; } } }

        /// <summary>本地玩家（isLocalPlayer；无则 null）。</summary>
        public static PlayerIdentity GetLocalPlayer()
        {
            lock (lockObj)
            {
                foreach (var p in players)
                    if (p != null && p.isLocalPlayer) return p;
            }
            return null;
        }

        /// <summary>按 SteamID 查询（无则 null）。</summary>
        public static PlayerIdentity GetBySteamId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return null;
            lock (lockObj)
            {
                foreach (var p in players)
                    if (p != null && p.SteamId == steamId) return p;
            }
            return null;
        }

        /// <summary>按网络连接查询（无则 null）。</summary>
        public static PlayerIdentity GetByConnection(NetworkConnection conn)
        {
            if (conn == null) return null;
            lock (lockObj)
            {
                foreach (var p in players)
                    if (p != null && p.connectionToClient == conn) return p;
            }
            return null;
        }

        /// <summary>按队伍查询（快照列表）。</summary>
        public static List<PlayerIdentity> GetByTeam(int teamId)
        {
            var result = new List<PlayerIdentity>();
            lock (lockObj)
            {
                foreach (var p in players)
                    if (p != null && p.TeamId == teamId) result.Add(p);
            }
            return result;
        }

        /// <summary>清空（场景切换/停止时由外部调用）。</summary>
        public static void Clear()
        {
            lock (lockObj) { players.Clear(); }
        }
    }
}
