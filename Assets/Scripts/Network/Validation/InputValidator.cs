using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 服务端输入校验器（B-01）：频率限流 + 聊天长度 + 所有权校验。
    /// 与 RpcThrottler 职责区分：RpcThrottler 降低带宽（无条件合并）；InputValidator 安全拦截（有条件拒绝）。
    /// 内部维护每连接每 actionKey 的最近调用时间戳与违规计数。
    /// Mirror 96 NetworkConnection 无公共 connectionId → 用 identity.connectionToClient 或 GetHashCode 作为串接标识。
    /// </summary>
    public static class InputValidator
    {
        static ValidationConfig _config;
        static readonly Dictionary<string, double> _lastCall = new();
        static readonly Dictionary<int, int> _violations = new();

        public static void Configure(ValidationConfig config) => _config = config;

        static int ChatMaxLen => _config != null ? _config.chatMaxLength : 128;
        static float ChatDelay => _config != null ? _config.chatMinInterval : 0.5f;
        static float InteractDelay => _config != null ? _config.interactMinInterval : 0.2f;
        static int VioThreshold => _config != null ? _config.violationThreshold : 10;

        static string Key(NetworkConnection conn, string actionKey) => conn.GetHashCode() + ":" + actionKey;

        public static bool ValidateRate(NetworkConnection conn, string actionKey, float minInterval)
        {
            double now = Time.unscaledTimeAsDouble;
            string key = Key(conn, actionKey);
            if (_lastCall.TryGetValue(key, out var last) && now - last < minInterval)
                return Violate(conn.GetHashCode(), actionKey + " 超频");
            _lastCall[key] = now;
            return true;
        }

        public static bool ValidateChat(NetworkConnection conn, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return Violate(conn.GetHashCode(), "空消息");
            if (message.Length > ChatMaxLen)
            {
                Debug.LogWarning($"[InputValidator] 消息超长({message.Length}>{ChatMaxLen})→截断");
                return true;
            }
            return ValidateRate(conn, "chat", ChatDelay);
        }

        public static bool ValidateOwnership(NetworkIdentity identity, NetworkConnection sender)
        {
            if (identity == null || sender == null) return false;
            if (identity.connectionToClient != sender)
                return Violate(sender.GetHashCode(), $"越权 {identity.netId}");
            return true;
        }

        static bool Violate(int key, string reason)
        {
            Debug.LogWarning($"[InputValidator] conn={key} {reason}");
            _violations.TryGetValue(key, out int c);
            _violations[key] = ++c;
            if (c >= VioThreshold && _config != null && _config.violationPolicy == ValidationConfig.ViolationPolicy.Kick)
            {
                Debug.LogWarning($"[InputValidator] 违规 {c} 次 → 断开 conn={key}");
                foreach (var kv in NetworkServer.connections)
                    if (kv.Value.GetHashCode() == key) { kv.Value.Disconnect(); break; }
            }
            return false;
        }
    }
}
