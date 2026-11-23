#if UNITY_EDITOR
using UnityEngine;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>网络模拟预设（T11-01）：一键套用 NetworkSimulator 参数。</summary>
    public enum NetworkSimPreset
    {
        Ideal,      // 0ms / 0% / 0ms
        Lan,        // 5ms / 0% / 1ms
        Broadband,  // 40ms / 0.5% / 5ms
        Net4G,      // 80ms / 2% / 15ms
        Poor,       // 200ms / 5% / 50ms
        Terrible    // 500ms / 15% / 150ms
    }

    public static class NetworkSimulatorPresets
    {
        /// <summary>预设参数表：latency / jitter / drop。</summary>
        public static readonly (float latency, float jitter, float drop)[] Table =
        {
            (0f,   0f,   0f),    // Ideal
            (5f,   0f,   0.01f), // Lan
            (40f,  0f,   0.005f),// Broadband
            (80f,  15f,  0.02f), // Net4G
            (200f, 50f,  0.05f), // Poor
            (500f, 150f, 0.15f), // Terrible
        };

        public static void Apply(NetworkSimPreset preset)
        {
            var p = Table[(int)preset];
            NetworkSimulator.Enabled = true;
            NetworkSimulator.LatencyMs = p.latency;
            NetworkSimulator.JitterMs = p.jitter;
            NetworkSimulator.DropRate = p.drop;
        }

        public static void Disable() => NetworkSimulator.Enabled = false;
    }
}
#endif
