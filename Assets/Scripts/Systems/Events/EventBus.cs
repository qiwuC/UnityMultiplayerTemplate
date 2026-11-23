using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>
    /// 事件总线（T8-02）：类型化事件、优先级降序、一次性订阅、异常隔离、订阅统计。
    /// 非主线程 Publish 记录警告；泛型静态通道特化避免事件负载装箱。
    /// </summary>
    public static class EventBus
    {
        static readonly int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        /// <summary>订阅事件（priority 越大越先收到），返回可释放句柄（Dispose 即注销）。</summary>
        public static IDisposable Subscribe<T>(Action<T> handler, int priority = 0)
            => EventBusChannel<T>.Subscribe(handler, priority);

        public static void Unsubscribe<T>(Action<T> handler) => EventBusChannel<T>.Unsubscribe(handler);

        /// <summary>一次性订阅：首次触发后自动注销。</summary>
        public static void SubscribeOnce<T>(Action<T> handler) => EventBusChannel<T>.SubscribeOnce(handler);

        public static void Publish<T>(T evt)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != mainThreadId)
                Debug.LogWarning($"[EventBus] Publish<{typeof(T).Name}> 于非主线程调用（已执行，请检查跨线程来源）。");
            EventBusChannel<T>.Publish(evt);
        }

        /// <summary>某事件类型的当前订阅数（诊断）。</summary>
        public static int SubscriberCount<T>() => EventBusChannel<T>.SubscriberCount;

        /// <summary>全部事件类型的订阅统计（诊断）。</summary>
        public static IEnumerable<(Type type, int count)> GetAllStats() => EventBusStats.All();

        /// <summary>清空所有订阅（Shutdown/测试用）。</summary>
        public static void ClearAll() => EventBusStats.ClearAll();

        /// <summary>订阅句柄：Dispose 解除订阅。</summary>
        sealed class Subscription : IDisposable
        {
            Action unsubscribe;
            bool disposed;

            public Subscription(Action unsubscribe) => this.unsubscribe = unsubscribe;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                unsubscribe?.Invoke();
                unsubscribe = null;
            }
        }
    }

    /// <summary>事件类型订阅统计注册表（内部）。</summary>
    static class EventBusStats
    {
        static readonly Dictionary<Type, Func<int>> counters = new Dictionary<Type, Func<int>>();
        static readonly List<Action> clearners = new List<Action>();

        public static void Register(Type eventType, Func<int> counter, Action clear)
        {
            counters[eventType] = counter;
            clearners.Add(clear);
        }

        public static IEnumerable<(Type type, int count)> All()
        {
            foreach (var kv in counters)
                yield return (kv.Key, kv.Value());
        }

        public static void ClearAll()
        {
            foreach (var c in clearners) c();
        }
    }

    /// <summary>泛型静态事件通道：按 T 由 JIT 特化，事件负载不装箱（内部）。</summary>
    static class EventBusChannel<T>
    {
        struct Handler
        {
            public Action<T> Action;
            public int Priority;
            public bool Once;
            public bool Removed;
        }

        static readonly List<Handler> handlers = new List<Handler>();
        static int publishDepth;

        static EventBusChannel()
        {
            EventBusStats.Register(typeof(T), () => handlers.Count, () => { handlers.Clear(); publishDepth = 0; });
        }

        public static int SubscriberCount => handlers.Count;

        public static IDisposable Subscribe(Action<T> handler, int priority)
        {
            if (handler == null) return null;
            Insert(new Handler { Action = handler, Priority = priority });
            return new Subscription(() => Unsubscribe(handler));
        }

        public static void SubscribeOnce(Action<T> handler)
        {
            if (handler == null) return;
            Insert(new Handler { Action = handler, Priority = 0, Once = true });
        }

        static void Insert(Handler h)
        {
            // 按 priority 降序插入（稳定：同优先级保持订阅顺序）
            int i = handlers.Count;
            while (i > 0 && handlers[i - 1].Priority < h.Priority) i--;
            handlers.Insert(i, h);
        }

        public static void Unsubscribe(Action<T> handler)
        {
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(handlers[i].Action, handler)) continue;
                if (publishDepth > 0)
                {
                    // 发布中：仅标记，遍历结束后移除
                    var h = handlers[i];
                    h.Removed = true;
                    handlers[i] = h;
                }
                else
                {
                    handlers.RemoveAt(i);
                }
            }
        }

        public static void Publish(T evt)
        {
            publishDepth++;
            try
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    var h = handlers[i];
                    if (h.Removed) continue;
                    if (h.Once)
                    {
                        // 先标记再调用，防止处理器内重入发布导致重复触发
                        var marked = h;
                        marked.Removed = true;
                        handlers[i] = marked;
                    }
                    try
                    {
                        h.Action?.Invoke(evt);
                    }
                    catch (Exception e)
                    {
                        // 异常隔离：单订阅者失败不影响其余订阅者
                        Debug.LogException(new Exception($"[EventBus] 订阅者处理 {typeof(T).Name} 异常（已隔离）", e));
                    }
                }
            }
            finally
            {
                publishDepth--;
                if (publishDepth == 0)
                {
                    for (int i = handlers.Count - 1; i >= 0; i--)
                        if (handlers[i].Removed)
                            handlers.RemoveAt(i);
                }
            }
        }

        sealed class Subscription : IDisposable
        {
            Action unsubscribe;
            bool disposed;

            public Subscription(Action unsubscribe) => this.unsubscribe = unsubscribe;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                unsubscribe?.Invoke();
                unsubscribe = null;
            }
        }
    }
}
