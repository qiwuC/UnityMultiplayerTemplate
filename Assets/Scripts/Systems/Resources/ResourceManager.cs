using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>资源句柄（T8-06）：Asset/RefCount/IsValid。</summary>
    public class ResourceHandle<T> where T : Object
    {
        /// <summary>资源路径（Resources 相对路径，无扩展名）。</summary>
        public string Path { get; internal set; }
        /// <summary>已加载资源（未加载/已卸载为 null）。</summary>
        public T Asset { get; internal set; }
        public int RefCount { get; internal set; }
        public bool IsValid => Asset != null;

        internal ResourceManager Manager { get; set; }

        /// <summary>增加引用。</summary>
        public void Retain() => RefCount++;

        /// <summary>减少引用；归零后由 ResourceManager 延迟卸载。</summary>
        public void Release() => Manager?.ReleaseHandle(this);

        public override string ToString() => $"ResourceHandle<{typeof(T).Name}>({Path}, refs={RefCount}, valid={IsValid})";
    }

    /// <summary>
    /// 资源管理器（T8-06）：Resources 异步加载 + 引用计数 + 延迟卸载（Addressables 接口预留，本阶段 Resources 占位）。
    /// 同一 (类型, 路径) 多次 LoadAsync 仅实际加载一次（pending 去重 + 句柄复用）；
    /// Release 到 0 后 idleSeconds 延迟卸载（期间再次 Load 直接复活）。
    /// </summary>
    [AddComponentMenu("Cqw/Systems/ResourceManager")]
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Tooltip("引用计数归零后延迟卸载秒数")]
        public float idleSeconds = 30f;

        class Entry
        {
            public Type assetType;
            public string path;
            public Object asset;
            public bool loading;
            public Coroutine loadRoutine;
            public double unloadAt; // realtimeSinceStartup；0 = 未调度
            public readonly List<object> waiters = new List<object>(); // Action<ResourceHandle<T>>
        }

        readonly Dictionary<(Type, string), Entry> entries = new Dictionary<(Type, string), Entry>();
        readonly Dictionary<(Type, string), object> handles = new Dictionary<(Type, string), object>();
        readonly List<object> preloadHandles = new List<object>(); // 预加载持有（常驻直到 ReleasePreloads）
        readonly List<(Type, string)> unloadScratch = new List<(Type, string)>();

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

        /// <summary>确保管理器存在。</summary>
        public static ResourceManager EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Systems_ResourceManager");
            return go.AddComponent<ResourceManager>();
        }

        // ============ 加载 ============

        /// <summary>
        /// 异步加载（引用计数 +1）。同一资源并发加载去重；已加载直接复用并回调。
        /// onLoaded 在主线程回调（加载失败时 Asset=null 仍回调）。
        /// </summary>
        public ResourceHandle<T> LoadAsync<T>(string path, Action<ResourceHandle<T>> onLoaded = null) where T : Object
        {
            EnsureExists();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[Resource] LoadAsync 路径为空。");
                return null;
            }

            var key = (typeof(T), path);
            if (!handles.TryGetValue(key, out var objHandle))
            {
                objHandle = new ResourceHandle<T> { Path = path, Manager = this };
                handles[key] = objHandle;
            }
            var handle = (ResourceHandle<T>)objHandle;

            if (entries.TryGetValue(key, out var entry) && !entry.loading && entry.asset != null)
            {
                // 已在内存：直接复用
                handle.Asset = (T)entry.asset;
                handle.RefCount++;
                entry.unloadAt = 0;
                onLoaded?.Invoke(handle);
                return handle;
            }

            handle.RefCount++;
            if (entry == null)
            {
                entry = new Entry { assetType = typeof(T), path = path, loading = true };
                entries[key] = entry;
            }
            else if (entry.unloadAt > 0)
            {
                // 卸载等待期内复活
                entry.unloadAt = 0;
            }

            if (onLoaded != null) entry.waiters.Add(onLoaded);

            if (entry.loading && entry.loadRoutine == null && entry.asset == null)
                entry.loadRoutine = StartCoroutine(LoadRoutine<T>(key, entry));
            else if (!entry.loading && entry.asset != null && entry.waiters.Count > 0)
                FlushWaiters<T>(key, entry); // 理论不达（已加载分支先返回），防御

            return handle;
        }

        IEnumerator LoadRoutine<T>((Type type, string path) key, Entry entry) where T : Object
        {
            var req = Resources.LoadAsync<T>(entry.path);
            while (!req.isDone) yield return null;

            entry.asset = req.asset;
            entry.loading = false;
            entry.loadRoutine = null;

            if (entry.asset == null)
                GameLogger.LogWarning($"[Resource] 加载失败：{entry.path}（{typeof(T).Name}）");
            else
                GameLogger.LogDebug($"[Resource] 已加载：{entry.path}（{typeof(T).Name}）");

            if (handles.TryGetValue(key, out var h) && h is ResourceHandle<T> typed)
                typed.Asset = (T)entry.asset;

            FlushWaiters<T>(key, entry);
        }

        void FlushWaiters<T>((Type type, string path) key, Entry entry) where T : Object
        {
            if (entry.waiters.Count == 0) return;
            handles.TryGetValue(key, out var h);
            var typed = h as ResourceHandle<T>;
            var waiters = new List<object>(entry.waiters);
            entry.waiters.Clear();
            foreach (var w in waiters)
                (w as Action<ResourceHandle<T>>)?.Invoke(typed);
        }

        /// <summary>预加载：加载并常驻持有（直到 ReleasePreloads）。</summary>
        public void PreloadAsync<T>(IEnumerable<string> paths, Action onCompleted = null) where T : Object
        {
            StartCoroutine(PreloadRoutine<T>(paths, onCompleted));
        }

        IEnumerator PreloadRoutine<T>(IEnumerable<string> paths, Action onCompleted) where T : Object
        {
            int remaining = 0;
            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                remaining++;
                LoadAsync<T>(p, handle =>
                {
                    if (handle != null && !preloadHandles.Contains(handle))
                        preloadHandles.Add(handle);
                    remaining--;
                });
            }
            while (remaining > 0) yield return null;
            onCompleted?.Invoke();
        }

        /// <summary>释放全部预加载持有。</summary>
        public void ReleasePreloads()
        {
            foreach (var h in preloadHandles)
            {
                switch (h)
                {
                    case ResourceHandle<Object> o: ReleaseHandle(o); break;
                    case ResourceHandle<Texture2D> t: ReleaseHandle(t); break;
                    case ResourceHandle<AudioClip> a: ReleaseHandle(a); break;
                    case ResourceHandle<GameObject> g: ReleaseHandle(g); break;
                    case ResourceHandle<Material> m: ReleaseHandle(m); break;
                    case ResourceHandle<Sprite> s: ReleaseHandle(s); break;
                    case ResourceHandle<TextAsset> x: ReleaseHandle(x); break;
                }
            }
            preloadHandles.Clear();
        }

        // ============ 引用计数 ============

        public void Retain<T>(ResourceHandle<T> handle) where T : Object => handle?.Retain();

        public void ReleaseHandle<T>(ResourceHandle<T> handle) where T : Object
        {
            if (handle == null) return;
            handle.RefCount--;
            if (handle.RefCount > 0) return;
            if (handle.RefCount < 0)
            {
                handle.RefCount = 0;
                return;
            }

            var key = (typeof(T), handle.Path);
            if (entries.TryGetValue(key, out var entry) && entry.asset != null && idleSeconds >= 0f)
                entry.unloadAt = Time.realtimeSinceStartup + idleSeconds;
        }

        void Update()
        {
            if (entries.Count == 0) return;
            double now = Time.realtimeSinceStartup;
            unloadScratch.Clear();
            foreach (var kv in entries)
            {
                var e = kv.Value;
                if (e.loading || e.asset == null || e.unloadAt <= 0 || now < e.unloadAt) continue;
                unloadScratch.Add(kv.Key);
            }
            foreach (var key in unloadScratch)
            {
                var entry = entries[key];
                GameLogger.LogDebug($"[Resource] 引用为 0 且超时 → 卸载：{entry.path}");
                if (entry.asset != null) Resources.UnloadAsset(entry.asset);
                entry.asset = null;
                entry.unloadAt = 0;
                entries.Remove(key);
                handles.Remove(key);
            }
        }

        /// <summary>统计（诊断）：条目数 / 常驻句柄数 / 预加载数。</summary>
        public (int entries, int handles, int preloads) GetStats() => (entries.Count, handles.Count, preloadHandles.Count);
    }
}
