using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 消息批处理器：把高频小消息按窗口（默认 50ms）合并成批量发送，
    /// 减少网络调用次数。泛型 T 为消息负载类型。
    /// 用法：batcher.Enqueue(msg); → 每 flushInterval 触发 OnBatch(List&lt;T&gt;) 由调用方统一发送。
    /// 非网络组件：与发送通道解耦（可包 NetworkServer.SendToAll / conn.Send / 自定义消息）。
    /// </summary>
    public class MessageBatcher<T> : IDisposable
    {
        /// <summary>批量输出（每 flushInterval 触发；缓冲为空不触发）。</summary>
        public event Action<List<T>> OnBatch;

        public readonly float FlushIntervalSeconds;
        readonly List<T> buffer = new List<T>();
        readonly int maxBatchSize;
        float lastFlushTime;
        bool disposed;
        MonoBehaviour host; // 可选宿主（用其 Update 驱动；无宿主时手动 Drive）

        /// <summary>
        /// 创建批处理器。
        /// </summary>
        /// <param name="flushIntervalSeconds">合并窗口（默认 0.05s=50ms）</param>
        /// <param name="maxBatchSize">单批上限（防单帧积压过大；超出立即冲刷）</param>
        /// <param name="host">宿主 MonoBehaviour（提供 Update 驱动；null=手动 Drive()）</param>
        public MessageBatcher(float flushIntervalSeconds = 0.05f, int maxBatchSize = 256, MonoBehaviour host = null)
        {
            FlushIntervalSeconds = flushIntervalSeconds;
            this.maxBatchSize = maxBatchSize;
            lastFlushTime = Time.realtimeSinceStartup;
            this.host = host;
            if (host != null) host.StartCoroutine(DriveRoutine());
        }

        /// <summary>入队一条消息（满批立即冲刷）。</summary>
        public void Enqueue(T item)
        {
            if (disposed) return;
            buffer.Add(item);
            if (buffer.Count >= maxBatchSize) Flush();
        }

        /// <summary>立即冲刷（忽略窗口）。</summary>
        public void Flush()
        {
            if (buffer.Count == 0) return;
            var batch = new List<T>(buffer);
            buffer.Clear();
            lastFlushTime = Time.realtimeSinceStartup;
            OnBatch?.Invoke(batch);
        }

        /// <summary>手动驱动（无宿主时由外部 Update 调用）。</summary>
        public void Drive()
        {
            if (buffer.Count > 0 && Time.realtimeSinceStartup - lastFlushTime >= FlushIntervalSeconds)
                Flush();
        }

        System.Collections.IEnumerator DriveRoutine()
        {
            var wait = new WaitForSeconds(FlushIntervalSeconds);
            while (!disposed)
            {
                yield return wait;
                if (buffer.Count > 0) Flush();
            }
        }

        public int BufferedCount => buffer.Count;

        public void Dispose()
        {
            disposed = true;
            Flush(); // 关闭时冲刷残余
            OnBatch = null;
        }
    }
}
