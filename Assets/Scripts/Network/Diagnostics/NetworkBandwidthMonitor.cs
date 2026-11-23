using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 网络带宽监视器：基于 Mirror NetworkStatistics 组件（同对象自动采集）采样发送/接收速率。
    /// 每 sampleInterval 更新属性并触发 OnSampled 事件（UI/HUD 订阅）。
    /// </summary>
    [AddComponentMenu("Cqw/Network/NetworkBandwidthMonitor")]
    [RequireComponent(typeof(NetworkStatistics))]
    public class NetworkBandwidthMonitor : MonoBehaviour
    {
        [Tooltip("采样间隔（秒）")]
        public float sampleInterval = 1f;

        /// <summary>采样事件（sendBps, recvBps, sendKbps, recvKbps）。</summary>
        public event System.Action<long, long, long, long> OnSampled;

        /// <summary>当前发送速率（字节/秒）。</summary>
        public long SendBytesPerSecond { get; private set; }
        /// <summary>当前接收速率（字节/秒）。</summary>
        public long ReceiveBytesPerSecond { get; private set; }
        public long SendKbps => SendBytesPerSecond * 8 / 1024;
        public long ReceiveKbps => ReceiveBytesPerSecond * 8 / 1024;
        public int ServerConnectionCount => NetworkServer.connections.Count;

        NetworkStatistics stats;
        float lastSampleTime;

        void Awake() => stats = GetComponent<NetworkStatistics>();

        void Update()
        {
            if (stats == null) return;
            if (Time.unscaledTime - lastSampleTime < sampleInterval) return;
            lastSampleTime = Time.unscaledTime;

            // NetworkStatistics 的 interval 字段即本采样周期的速率快照
            SendBytesPerSecond = stats.serverIntervalSentBytes != 0 || stats.clientIntervalSentBytes != 0
                ? stats.serverIntervalSentBytes + stats.clientIntervalSentBytes
                : 0;
            ReceiveBytesPerSecond = stats.serverIntervalReceivedBytes + stats.clientIntervalReceivedBytes;

            OnSampled?.Invoke(SendBytesPerSecond, ReceiveBytesPerSecond, SendKbps, ReceiveKbps);
        }

        /// <summary>人类可读诊断行。</summary>
        public string Describe() =>
            $"↑{SendBytesPerSecond / 1024f:F1}KB/s ↓{ReceiveBytesPerSecond / 1024f:F1}KB/s" +
            (NetworkServer.active ? $" (server,{ServerConnectionCount} conns)" : NetworkClient.active ? " (client)" : " (offline)");
    }
}
