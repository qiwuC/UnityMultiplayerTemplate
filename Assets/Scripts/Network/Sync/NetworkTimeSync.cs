using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 网络时间同步门面：封装 Mirror NetworkTime（服务器权威时间 + RTT 校准）。
    /// 提供倒计时/冷却 API（基于服务器时间，客户端视角已含插值校准）。
    /// 离线时 NetworkTime.time 冻结，回退到本地 unscaled 时钟保证 API 可用。
    /// </summary>
    public static class NetworkTimeSync
    {
        /// <summary>当前网络时间（服务器权威；客户端为校准估计值；离线回退本地时钟）。</summary>
        public static double Now => NetworkClient.active
            ? NetworkTime.time
            : UnityEngine.Time.unscaledTimeAsDouble;

        /// <summary>往返延迟（秒）。</summary>
        public static double Rtt => NetworkTime.rtt;

        /// <summary>RTT 抖动方差（秒）。</summary>
        public static double RttVariance => NetworkTime.rttVariance;

        /// <summary>本地与服务器时钟偏移（秒；仅客户端有意义，Host≈0）。</summary>
        public static double LocalTimelineDrift => NetworkTime.offset;

        /// <summary>指定网络时刻是否已到达（倒计时结束判定）。</summary>
        public static bool HasPassed(double networkTimestamp) => Now >= networkTimestamp;

        /// <summary>距指定网络时刻的剩余秒数（负值=已过）。</summary>
        public static double Remaining(double deadlineNetworkTime) => deadlineNetworkTime - Now;

        /// <summary>从现在起延迟 delay 秒的网络时刻（用于广播"绝对截止时间"，天然同步）。</summary>
        public static double In(double delay) => Now + delay;
    }

    /// <summary>
    /// 网络冷却计时器：基于服务器时间的冷却/技能 CD。
    /// 服务端记录 lastUse（网络时刻），各端用 Remaining() 显示一致的倒计时。
    /// </summary>
    public struct NetworkCooldown
    {
        public readonly double CooldownSeconds;
        double lastUseNetworkTime;

        public NetworkCooldown(double cooldownSeconds) : this()
        {
            CooldownSeconds = cooldownSeconds;
            lastUseNetworkTime = double.MinValue;
        }

        /// <summary>是否可用（冷却结束）。</summary>
        public bool IsReady => NetworkTimeSync.Now - lastUseNetworkTime >= CooldownSeconds;

        /// <summary>尝试使用（可用则记录时刻并返回 true）。</summary>
        public bool TryUse()
        {
            if (!IsReady) return false;
            lastUseNetworkTime = NetworkTimeSync.Now;
            return true;
        }

        /// <summary>剩余冷却秒数（0=就绪）。</summary>
        public float RemainingSeconds =>
            (float)Mathf.Max(0, (float)(CooldownSeconds - (NetworkTimeSync.Now - lastUseNetworkTime)));
    }
}
