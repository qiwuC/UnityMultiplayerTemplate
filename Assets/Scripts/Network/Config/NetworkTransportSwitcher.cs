using System;
using kcp2k;
using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 根据 NetworkConfig 在 NetworkManager 上安装/切换 KcpTransport 或 FizzySteamworks。
    /// 运行时（未启动会话时）与编辑器（菜单）均可用。
    /// 注意：切换必须在服务器/客户端启动前进行（Mirror 不支持会话进行中更换 Transport）。
    /// </summary>
    public static class NetworkTransportSwitcher
    {
        /// <summary>
        /// 应用 config 中声明的传输层到 NetworkManager。
        /// 返回切换结果与实际生效的 Transport 描述。
        /// </summary>
        public static string Apply(NetworkConfig config, Mirror.NetworkManager nm)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (nm == null) throw new ArgumentNullException(nameof(nm));

            if (NetworkServer.active || NetworkClient.active)
                throw new InvalidOperationException(
                    "[NetworkTransportSwitcher] 网络会话进行中无法切换传输层，请先 StopServer/StopClient。");

            switch (config.Transport)
            {
                case TransportMode.Kcp: return ApplyKcp(config, nm);
                case TransportMode.FizzySteamworks: return ApplyFizzySteamworks(config, nm);
                default: throw new ArgumentOutOfRangeException(nameof(config.Transport));
            }
        }

        /// <summary>强制切换到 KCP（直连 IP/端口）。</summary>
        public static string ApplyKcp(NetworkConfig config, Mirror.NetworkManager nm)
        {
            var kcp = GetOrAdd<KcpTransport>(nm);
            kcp.port = config.KcpPort;
            nm.transport = kcp;
            nm.maxConnections = config.MaxConnections;
            return $"KCP: port={kcp.port} (DualMode 直连)";
        }

        /// <summary>强制切换到 FizzySteamworks（Steam P2P，连接地址为房主 SteamID）。</summary>
        public static string ApplyFizzySteamworks(NetworkConfig config, Mirror.NetworkManager nm)
        {
            var fizzy = GetOrAdd<Mirror.FizzySteam.FizzySteamworks>(nm);
            nm.transport = fizzy;
            nm.maxConnections = config.MaxConnections;
            return "FizzySteamworks: Steam P2P（地址=房主 SteamID，AppID 由 steam_appid.txt/SteamAPI 决定）";
        }

        /// <summary>当前 NetworkManager 上实际安装的传输层类型名（诊断用）。</summary>
        public static string DescribeActive(Mirror.NetworkManager nm)
        {
            var t = nm != null ? nm.transport : null;
            return t == null ? "(none)" : t.GetType().Name;
        }

        static T GetOrAdd<T>(Mirror.NetworkManager nm) where T : Component
        {
            var t = nm.GetComponent<T>();
            return t != null ? t : nm.gameObject.AddComponent<T>();
        }
    }
}
