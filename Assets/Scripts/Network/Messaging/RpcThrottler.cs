using System;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// RPC 令牌桶限流器：防止高频 RPC 打爆带宽（如每帧移动同步、刷屏聊天）。
    /// 用法：在 RPC/Command 调用前 if (!throttler.TryConsume()) return;
    /// 桶容量=瞬时突发上限；填充速率=长期平均速率（个/秒）。
    /// </summary>
    [Serializable]
    public class RpcThrottler
    {
        [Tooltip("桶容量（最大突发量）")]
        public int capacity = 10;

        [Tooltip("每秒补充令牌数（长期平均速率）")]
        public float refillPerSecond = 5f;

        float tokens;
        float lastRefillTime;

        /// <summary>当前令牌数（诊断显示）。</summary>
        public float Tokens { get { Refill(); return tokens; } }

        public RpcThrottler(int capacity = 10, float refillPerSecond = 5f)
        {
            this.capacity = capacity;
            this.refillPerSecond = refillPerSecond;
            tokens = capacity;
            lastRefillTime = Time.realtimeSinceStartup;
        }

        /// <summary>尝试消耗 1 个令牌（成功=true 允许发送）。</summary>
        public bool TryConsume()
        {
            Refill();
            if (tokens >= 1f)
            {
                tokens -= 1f;
                return true;
            }
            return false;
        }

        /// <summary>尝试消耗 n 个令牌。</summary>
        public bool TryConsume(int n)
        {
            Refill();
            if (tokens >= n)
            {
                tokens -= n;
                return true;
            }
            return false;
        }

        /// <summary>强制重置为满桶。</summary>
        public void Reset()
        {
            tokens = capacity;
            lastRefillTime = Time.realtimeSinceStartup;
        }

        void Refill()
        {
            var now = Time.realtimeSinceStartup;
            float elapsed = now - lastRefillTime;
            if (elapsed <= 0f) return;
            tokens = Mathf.Min(capacity, tokens + elapsed * refillPerSecond);
            lastRefillTime = now;
        }
    }
}
