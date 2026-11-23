#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 网络状况模拟器（仅 Editor / Development Build 编译）：
    /// 在应用层给消息注入延迟/抖动/丢包，用于本地测试弱网下的表现。
    /// 用法：把发送动作包进 Simulate(Action)，或用 ShouldDrop() 决定是否丢弃。
    /// </summary>
    public static class NetworkSimulator
    {
        [Header("模拟参数（运行时可改）")]
        public static bool Enabled;
        /// <summary>注入延迟（毫秒）。</summary>
        public static float LatencyMs = 100f;
        /// <summary>±抖动（毫秒，均匀分布）。</summary>
        public static float JitterMs = 30f;
        /// <summary>丢包率（0-1）。</summary>
        public static float DropRate = 0.05f;

        static readonly List<(float fireTime, Action action)> pending = new List<(float, Action)>();

        /// <summary>模拟一次发送：按概率丢弃，否则延迟+抖动执行。</summary>
        public static void Simulate(Action sendAction)
        {
            if (!Enabled) { sendAction?.Invoke(); return; }
            if (ShouldDrop()) { Debug.Log("[NetSim] 丢弃一条消息（模拟丢包）"); return; }
            float delay = (LatencyMs + UnityEngine.Random.Range(-JitterMs, JitterMs)) / 1000f;
            if (delay <= 0) { sendAction?.Invoke(); return; }
            pending.Add((Time.realtimeSinceStartup + delay, sendAction));
        }

        /// <summary>本条消息是否应被丢弃。</summary>
        public static bool ShouldDrop() => Enabled && UnityEngine.Random.value < DropRate;

        /// <summary>驱动延迟队列（每帧调用；由 NetworkDiagnostics 的 Update 自动驱动）。</summary>
        public static void Drive()
        {
            if (pending.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (now >= pending[i].fireTime)
                {
                    pending[i].action?.Invoke();
                    pending.RemoveAt(i);
                }
            }
        }

        /// <summary>清空待发队列与参数复位。</summary>
        public static void Reset()
        {
            pending.Clear();
            Enabled = false;
            LatencyMs = 100f;
            JitterMs = 30f;
            DropRate = 0.05f;
        }

        public static string Describe() =>
            Enabled ? $"latency={LatencyMs:F0}ms jitter=±{JitterMs:F0}ms drop={DropRate:P0} pending={pending.Count}" : "off";
    }
}
#endif
