using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>池统计接口（T8-01）。</summary>
    public interface IObjectPoolStats
    {
        /// <summary>已借出未归还数量。</summary>
        int ActiveCount { get; }
        /// <summary>池内空闲数量。</summary>
        int PooledCount { get; }
        /// <summary>累计创建总数。</summary>
        int TotalCreated { get; }
        /// <summary>历史峰值借出数。</summary>
        int PeakActive { get; }
    }

    /// <summary>通用对象池接口。</summary>
    public interface IObjectPool<T> where T : class
    {
        T Get();
        void Release(T item);
        void Clear();
    }

    /// <summary>
    /// 纯 C# 对象池（T8-01）：Get/Release/Prewarm/Clear + 统计 + 空闲超时回收。
    /// Unity 对象请用 UnityObjectPool&lt;T&gt;（基于 UnityEngine.Pool.ObjectPool 封装）或由 ObjectPoolService 创建。
    /// </summary>
    public class ObjectPool<T> : IObjectPool<T>, IObjectPoolStats where T : class
    {
        readonly Func<T> createFunc;
        readonly Action<T> onGet;
        readonly Action<T> onRelease;
        readonly Action<T> onDestroy;
        readonly int maxSize;
        readonly Stack<(T item, float releaseTime)> idle = new Stack<(T, float)>();

        public int ActiveCount { get; private set; }
        public int PooledCount => idle.Count;
        public int TotalCreated { get; private set; }
        public int PeakActive { get; private set; }

        /// <summary>空闲超时（秒；&lt;=0 不自动回收，由 ObjectPoolService 周期调用 PruneIdle）。</summary>
        public float IdleTimeoutSeconds { get; set; }

        public ObjectPool(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null,
            Action<T> onDestroy = null, int maxSize = 1024, float idleTimeoutSeconds = 0f)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            this.onGet = onGet;
            this.onRelease = onRelease;
            this.onDestroy = onDestroy;
            this.maxSize = Math.Max(0, maxSize);
            IdleTimeoutSeconds = idleTimeoutSeconds;
        }

        public T Get()
        {
            T item;
            if (idle.Count > 0)
            {
                item = idle.Pop().item;
            }
            else
            {
                item = createFunc();
                TotalCreated++;
            }
            ActiveCount++;
            if (ActiveCount > PeakActive) PeakActive = ActiveCount;
            onGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            if (ActiveCount > 0) ActiveCount--;
            if (idle.Count < maxSize)
            {
                onRelease?.Invoke(item);
                idle.Push((item, Time.realtimeSinceStartup));
            }
            else
            {
                onDestroy?.Invoke(item);
            }
        }

        /// <summary>预创建 count 个实例入池（不改变统计的借出数）。</summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && idle.Count < maxSize; i++)
            {
                var item = createFunc();
                TotalCreated++;
                onRelease?.Invoke(item);
                idle.Push((item, Time.realtimeSinceStartup));
            }
        }

        public void Clear()
        {
            while (idle.Count > 0)
                onDestroy?.Invoke(idle.Pop().item);
        }

        /// <summary>回收空闲超过超时时间的实例（返回回收数量；仅 IdleTimeoutSeconds&gt;0 时生效）。</summary>
        public int PruneIdle(float now)
        {
            if (IdleTimeoutSeconds <= 0f || idle.Count == 0) return 0;
            var expired = new List<T>();
            foreach (var (item, releaseTime) in idle)
                if (now - releaseTime > IdleTimeoutSeconds)
                    expired.Add(item);
            foreach (var item in expired)
            {
                onDestroy?.Invoke(item);
                // Stack 中移除指定元素
                var remaining = new Stack<(T, float)>();
                while (idle.Count > 0)
                {
                    var top = idle.Pop();
                    if (!EqualityComparer<T>.Default.Equals(top.item, item)) remaining.Push(top);
                }
                while (remaining.Count > 0) idle.Push(remaining.Pop());
            }
            return expired.Count;
        }
    }

    /// <summary>
    /// Unity 对象池（T8-01）：基于 UnityEngine.Pool.ObjectPool&lt;T&gt; 封装，提供统一统计。
    /// 默认销毁动作为 Object.Destroy。
    /// </summary>
    public class UnityObjectPool<T> : IObjectPool<T>, IObjectPoolStats where T : Object
    {
        readonly UnityEngine.Pool.ObjectPool<T> inner;

        public int ActiveCount => inner.CountActive;
        public int PooledCount => inner.CountInactive;
        public int TotalCreated => inner.CountAll;
        public int PeakActive { get; private set; }

        public UnityObjectPool(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null,
            Action<T> onDestroy = null, int maxSize = 1024, bool collectionCheck = true)
        {
            inner = new UnityEngine.Pool.ObjectPool<T>(
                createFunc,
                item => { TrackPeak(); onGet?.Invoke(item); },
                item => { if (item != null) onRelease?.Invoke(item); },
                item =>
                {
                    if (item != null) onDestroy?.Invoke(item);
                    else Object.Destroy(item);
                },
                collectionCheck,
                maxSize);
        }

        void TrackPeak()
        {
            if (inner.CountActive + 1 > PeakActive) PeakActive = inner.CountActive + 1;
        }

        public T Get() => inner.Get();

        public void Release(T item) => inner.Release(item);

        public void Clear() => inner.Clear();

        /// <summary>预创建 count 个实例入池。</summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var item = inner.Get();
                inner.Release(item);
            }
        }
    }

    /// <summary>对象池句柄（T8-01）：using 语法自动 Release。</summary>
    public sealed class ObjectPoolHandle<T> : IDisposable where T : class
    {
        readonly IObjectPool<T> pool;
        public T Item { get; }

        public ObjectPoolHandle(IObjectPool<T> pool, T item)
        {
            this.pool = pool;
            Item = item;
        }

        public void Dispose()
        {
            if (Item != null) pool?.Release(Item);
        }
    }

    /// <summary>
    /// 对象池服务（T8-01）：统一创建/注册各类池，周期执行空闲回收。
    /// 注册的池需实现 IObjectPoolStats；空闲回收仅对 IdleTimeoutSeconds&gt;0 的纯 C# 池生效。
    /// </summary>
    [AddComponentMenu("Cqw/Systems/ObjectPoolService")]
    public class ObjectPoolService : MonoBehaviour
    {
        public static ObjectPoolService Instance { get; private set; }

        [Tooltip("空闲回收扫描间隔（秒）")]
        public float pruneInterval = 5f;

        readonly List<(IObjectPoolStats stats, Action<float> prune)> registered = new List<(IObjectPoolStats, Action<float>)>();
        float nextPruneTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Time.realtimeSinceStartup < nextPruneTime) return;
            nextPruneTime = Time.realtimeSinceStartup + pruneInterval;
            float now = Time.realtimeSinceStartup;
            for (int i = registered.Count - 1; i >= 0; i--)
                registered[i].prune?.Invoke(now);
        }

        /// <summary>确保服务存在（自动创建 DontDestroyOnLoad 宿主）。</summary>
        public static ObjectPoolService EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Systems_ObjectPoolService");
            return go.AddComponent<ObjectPoolService>();
        }

        /// <summary>创建纯 C# 对象池并注册空闲回收。</summary>
        public static ObjectPool<T> Create<T>(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null,
            Action<T> onDestroy = null, int maxSize = 1024, float idleTimeoutSeconds = 0f) where T : class
        {
            EnsureExists();
            var pool = new ObjectPool<T>(createFunc, onGet, onRelease, onDestroy, maxSize, idleTimeoutSeconds);
            Instance.RegisterPool(pool, () => pool.PruneIdle(Time.realtimeSinceStartup));
            return pool;
        }

        /// <summary>创建 Unity 对象池（UnityEngine.Pool 封装）。</summary>
        public static UnityObjectPool<T> CreateUnity<T>(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null,
            Action<T> onDestroy = null, int maxSize = 1024) where T : Object
        {
            EnsureExists();
            var pool = new UnityObjectPool<T>(createFunc, onGet, onRelease, onDestroy, maxSize);
            Instance.RegisterPool(pool, null); // Unity 池无空闲时间戳，不做超时回收
            return pool;
        }

        /// <summary>注册已有池（统计与空闲回收）。</summary>
        public void RegisterPool(IObjectPoolStats stats, Action prune = null)
        {
            registered.Add((stats, prune != null ? _ => prune() : (Action<float>)null));
        }

        /// <summary>清空全部注册池（Shutdown 用；实例本身由宿主销毁）。</summary>
        public void ClearAll()
        {
            registered.Clear();
        }
    }
}
