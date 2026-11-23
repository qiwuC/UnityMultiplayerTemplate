using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>网络质量分级。</summary>
    public enum NetworkQuality
    {
        Good,     // RTT<80ms 且丢包<2%
        Fair,     // RTT<200ms 且丢包<8%
        Poor,     // RTT<400ms 且丢包<15%
        Critical  // 更差
    }

    /// <summary>
    /// 网络诊断中心：延迟/丢包采样、质量分级、降级策略建议、诊断日志导出。
    /// 同时驱动 NetworkSimulator 的延迟队列（仅 Editor/DevBuild 存在该模块）。
    /// 注意：命名避让 Mirror 的 NetworkDiagnostics（消息统计类）。
    /// </summary>
    [AddComponentMenu("Cqw/Network/NetworkDiagnosticsCenter")]
    public class NetworkDiagnosticsCenter : MonoBehaviour
    {
        public static NetworkDiagnosticsCenter Instance { get; private set; }

        [Tooltip("采样间隔（秒）")]
        public float sampleInterval = 2f;

        /// <summary>质量变化事件（新等级）。</summary>
        public event Action<NetworkQuality> OnQualityChanged;
        /// <summary>降级策略建议事件（等级+建议动作文本）。</summary>
        public event Action<NetworkQuality, string> OnDegradeAdvice;

        /// <summary>当前质量等级。</summary>
        public NetworkQuality Quality { get; private set; } = NetworkQuality.Good;
        /// <summary>当前 RTT（毫秒；Mirror 平滑值）。</summary>
        public double RttMs => NetworkTime.rtt * 1000.0;
        /// <summary>估算丢包率（基于 KCP/可靠性缺失样本的近似——Mirror 无原生丢包计数，这里用 RTT 抖动近似）。</summary>
        public float EstimatedLossPercent { get; private set; }
        /// <summary>历史采样数。</summary>
        public int SampleCount { get; private set; }

        float lastSampleTime;
        double lastRtt;
        readonly Queue<double> rttWindow = new Queue<double>(16);
        readonly StringBuilder logBuffer = new StringBuilder(4096);
        const int MaxLogLines = 500;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Log($"[Diag] 初始化 RTT={RttMs:F0}ms");
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NetworkSimulator.Drive(); // 模拟器延迟队列驱动
#endif
            if (Time.unscaledTime - lastSampleTime < sampleInterval) return;
            lastSampleTime = Time.unscaledTime;
            Sample();
        }

        void Sample()
        {
            if (!NetworkClient.active) return;
            SampleCount++;

            double rtt = RttMs;
            rttWindow.Enqueue(rtt);
            if (rttWindow.Count > 16) rttWindow.Dequeue();

            // 抖动 → 近似丢包估算（抖动越大越可能丢包重传；真实丢包需传输层暴露，KCP 内部有 stats 可后续接）
            double jitter = Math.Abs(rtt - lastRtt);
            lastRtt = rtt;
            EstimatedLossPercent = Mathf.Clamp01((float)(jitter / 400.0)) * 15f; // 粗略近似

            var newQuality = Classify(rtt, EstimatedLossPercent);
            if (newQuality != Quality)
            {
                Quality = newQuality;
                Log($"[Diag] 质量变化 → {Quality} (RTT={rtt:F0}ms loss≈{EstimatedLossPercent:F1}%)");
                OnQualityChanged?.Invoke(Quality);

                var advice = GetDegradeAdvice(Quality);
                if (advice != null)
                {
                    Log($"[Diag] 降级策略：{advice}");
                    OnDegradeAdvice?.Invoke(Quality, advice);
                }
            }
        }

        NetworkQuality Classify(double rttMs, float lossPercent)
        {
            if (rttMs < 80 && lossPercent < 2) return NetworkQuality.Good;
            if (rttMs < 200 && lossPercent < 8) return NetworkQuality.Fair;
            if (rttMs < 400 && lossPercent < 15) return NetworkQuality.Poor;
            return NetworkQuality.Critical;
        }

        /// <summary>降级策略建议（按等级）。</summary>
        public string GetDegradeAdvice(NetworkQuality q)
        {
            switch (q)
            {
                case NetworkQuality.Good: return null;
                case NetworkQuality.Fair: return "可继续游戏；建议 UI 提示网络一般";
                case NetworkQuality.Poor: return "建议：降低同步频率（NetworkTransform syncInterval×2）、关闭远端玩家特效";
                case NetworkQuality.Critical: return "建议：暂停发送非关键 RPC、显示重连提示、目标 syncInterval×4";
                default: return null;
            }
        }

        void Log(string line)
        {
            logBuffer.AppendLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
            if (logBuffer.Length > MaxLogLines * 80) logBuffer.Remove(0, logBuffer.Length / 2); // 防爆
        }

        /// <summary>导出诊断日志到文件（默认 Application.persistentDataPath/network-diagnostics.log）。</summary>
        public string ExportLog(string filePath = null)
        {
            try
            {
                filePath ??= Path.Combine(Application.persistentDataPath, "network-diagnostics.log");
                var sb = new StringBuilder();
                sb.AppendLine($"# Network Diagnostics @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# Quality={Quality} RTT={RttMs:F1}ms loss≈{EstimatedLossPercent:F1}% samples={SampleCount}");
                if (NetworkClient.active) sb.AppendLine($"# connected={NetworkClient.isConnected} addr={NetworkManager.singleton.networkAddress}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                sb.AppendLine($"# Simulator: {NetworkSimulator.Describe()}");
#endif
                sb.AppendLine();
                sb.Append(logBuffer);
                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"[Diag] 日志已导出：{filePath}");
                return filePath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Diag] 导出失败：{e.Message}");
                return null;
            }
        }

        /// <summary>一键诊断摘要（HUD/日志用）。</summary>
        public string Describe() =>
            $"quality={Quality} rtt={RttMs:F0}ms loss≈{EstimatedLossPercent:F1}% samples={SampleCount}";
    }
}
