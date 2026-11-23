using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>传输层模式：KCP（局域网/直连 IP）或 FizzySteamworks（Steam P2P 中继）。</summary>
    public enum TransportMode
    {
        Kcp,
        FizzySteamworks
    }

    /// <summary>网络核心配置（ScriptableObject）。运行时只读，修改需在编辑器进行。</summary>
    [CreateAssetMenu(fileName = "NetworkConfig", menuName = "Cqw/Network/NetworkConfig", order = 0)]
    public class NetworkConfig : ScriptableObject
    {
        [Header("传输层")]
        [Tooltip("当前传输层：KCP 直连或 Steam P2P（FizzySteamworks）")]
        public TransportMode Transport = TransportMode.Kcp;

        [Header("KCP")]
        [Tooltip("KCP 监听端口")]
        public ushort KcpPort = 7777;

        [Header("Steam")]
        [Tooltip("Steam AppID。0 表示读取 steam_appid.txt（占位 480）。此处仅为记录，实际初始化由 SteamAPI 决定")]
        public uint SteamAppId = 480;

        [Header("服务器")]
        [Tooltip("最大同时连接数")]
        public int MaxConnections = 16;
        [Tooltip("协议版本。不匹配的客户端将在认证阶段被拒绝")]
        public int ProtocolVersion = 1;

        [Header("断线重连")]
        [Tooltip("是否启用意外断线自动重连")]
        public bool AutoReconnect = true;
        [Tooltip("两次重连尝试之间的基础延时（秒），含随机抖动")]
        public float ReconnectDelay = 3f;
        [Tooltip("最大重连尝试次数，超过后停止并通知 UI")]
        public int ReconnectMaxAttempts = 5;

        [Header("生成点")]
        [Tooltip("玩家生成点（空则回退 NetworkManager 注册的 StartPositions）")]
        public List<Transform> SpawnPoints = new List<Transform>();

        /// <summary>默认配置资产路径。</summary>
        public const string DefaultAssetPath = "Assets/ScriptableObjects/NetworkConfig.asset";

        /// <summary>编辑器校验：ProtocolVersion 必须与 ProtocolVersion 当前值一致。</summary>
        void OnValidate()
        {
            if (MaxConnections < 1) MaxConnections = 1;
            if (KcpPort == 0) KcpPort = 7777;
            if (ReconnectDelay < 0.1f) ReconnectDelay = 0.1f;
            if (ReconnectMaxAttempts < 0) ReconnectMaxAttempts = 0;
        }
    }
}
