using System.IO;
using System.Text;
using UnityEngine;
using Mirror;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>
    /// 网络诊断导出（T11-04）：读取 NetworkDiagnosticsCenter / NetworkBandwidthMonitor 公共数据，
    /// 导出 CSV 到 persistentDataPath/Diagnostics/diagnostics_{timestamp}.csv（不修改阶段二文件）。
    /// </summary>
    public static class NetworkDiagnosticsExporter
    {
        public static string ExportCsv()
        {
            var dir = Path.Combine(Application.persistentDataPath, "Diagnostics");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"diagnostics_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("time,quality,rttMs,estimatedLossPercent,samples,sendKbps,recvKbps,serverConnections,connected");
            var diag = Object.FindAnyObjectByType<NetworkDiagnosticsCenter>();
            var bw = Object.FindAnyObjectByType<NetworkBandwidthMonitor>();
            var quality = diag != null ? diag.Quality.ToString() : "N/A";
            var rtt = diag != null ? diag.RttMs.ToString("F1") : "N/A";
            var loss = diag != null ? diag.EstimatedLossPercent.ToString("F1") : "N/A";
            var samples = diag != null ? diag.SampleCount.ToString() : "0";
            var send = bw != null ? bw.SendKbps.ToString() : "0";
            var recv = bw != null ? bw.ReceiveKbps.ToString() : "0";
            var conns = NetworkServer.active ? NetworkServer.connections.Count.ToString() : "0";
            var connected = NetworkClient.active ? NetworkClient.isConnected.ToString() : "False";
            sb.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss},{quality},{rtt},{loss},{samples},{send},{recv},{conns},{connected}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[DiagExport] 已导出：{path}");
            return path;
        }
    }
}
