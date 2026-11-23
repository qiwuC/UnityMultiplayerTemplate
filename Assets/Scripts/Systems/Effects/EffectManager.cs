using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>特效播放句柄（T8-07）：Stop/SetPosition/SetFollow。</summary>
    public class EffectHandle
    {
        public bool IsActive { get; internal set; }
        public GameObject Instance { get; internal set; }
        /// <summary>跟随目标（销毁后自动回收）。</summary>
        public Transform Follow { get; internal set; }

        internal EffectManager Manager { get; set; }
        internal ObjectPool<GameObject> SourcePool { get; set; }
        internal float RecycleAt = -1f; // realtimeSinceStartup；-1 = 不按时间回收

        public void Stop() => Manager?.Recycle(this);

        public void SetPosition(Vector3 pos)
        {
            if (Instance != null) Instance.transform.position = pos;
        }

        public void SetRotation(Quaternion rot)
        {
            if (Instance != null) Instance.transform.rotation = rot;
        }

        public void SetFollow(Transform target) => Follow = target;
    }

    /// <summary>
    /// 特效管理器（T8-07）：按 prefab 分池（复用 T8-01 ObjectPool）、播放/自动回收/跟随。
    /// duration&gt;0 按指定时长回收；duration&lt;=0 按 ParticleSystem 时长（main.duration + startLifetime 最大值）估算。
    /// </summary>
    [AddComponentMenu("Cqw/Systems/EffectManager")]
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance { get; private set; }

        class PoolEntry
        {
            public ObjectPool<GameObject> Pool;
            public GameObject Prefab;
            public float DefaultDuration = 2f;
        }

        readonly Dictionary<GameObject, PoolEntry> pools = new Dictionary<GameObject, PoolEntry>();
        readonly List<EffectHandle> active = new List<EffectHandle>();

        /// <summary>活跃特效数（统计）。</summary>
        public int ActiveCount => active.Count;
        /// <summary>全部池中空闲实例数（统计）。</summary>
        public int PooledCount
        {
            get
            {
                int n = 0;
                foreach (var p in pools.Values) n += p.Pool.PooledCount;
                return n;
            }
        }

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
        public static EffectManager EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Systems_EffectManager");
            return go.AddComponent<EffectManager>();
        }

        /// <summary>播放特效（返回句柄；prefab 为空返回 null）。</summary>
        public EffectHandle Play(GameObject prefab, Vector3 pos, Quaternion rot, float duration = 0f, Transform follow = null)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[Effect] Play(prefab=null) 已忽略。");
                return null;
            }

            var entry = GetPool(prefab);
            var handle = new EffectHandle { Manager = this, SourcePool = entry.Pool };
            var go = entry.Pool.Get();
            go.transform.SetPositionAndRotation(pos, rot);

            handle.Instance = go;
            handle.Follow = follow;
            handle.IsActive = true;
            if (duration <= 0f) duration = EstimateDuration(go, entry.DefaultDuration);
            handle.RecycleAt = Time.realtimeSinceStartup + Mathf.Max(0.05f, duration);
            active.Add(handle);
            return handle;
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var h = active[i];
                if (h == null || !h.IsActive) { active.RemoveAt(i); continue; }

                // 注意：h.Follow 是 Unity 假 null（销毁后 != null 为 false）
                // 需用 is 判型后再判空，避免销毁目标导致整块跳过
                if (h.Follow is Transform f)
                {
                    if (f == null)
                    {
                        // 跟随目标已销毁 → 自动回收
                        Recycle(h);
                        continue;
                    }
                    if (h.Instance != null) h.Instance.transform.position = f.position;
                }

                if (h.RecycleAt >= 0 && now >= h.RecycleAt)
                    Recycle(h);
            }
        }

        internal void Recycle(EffectHandle h)
        {
            if (h == null || !h.IsActive) return;
            h.IsActive = false;
            active.Remove(h);
            h.Follow = null;
            var go = h.Instance;
            h.Instance = null;
            if (go != null && h.SourcePool != null) h.SourcePool.Release(go);
        }

        PoolEntry GetPool(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out var entry)) return entry;
            var prefabRef = prefab;
            entry = new PoolEntry { Prefab = prefabRef };
            entry.Pool = ObjectPoolService.Create(
                createFunc: () => Instantiate(prefabRef),
                onGet: go =>
                {
                    go.SetActive(true);
                    PlayParticles(go);
                },
                onRelease: go => go.SetActive(false),
                onDestroy: go => Destroy(go),
                maxSize: 64,
                idleTimeoutSeconds: 60f);
            pools[prefabRef] = entry;
            return entry;
        }

        static void PlayParticles(GameObject go)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Play(true);
        }

        static float EstimateDuration(GameObject go, float fallback)
        {
            float max = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                float t = main.duration + Mathf.Max(0f, main.startLifetime.constantMax) + Mathf.Max(0f, main.startDelay.constantMax);
                if (t > max) max = t;
            }
            return max > 0f ? max : fallback;
        }

        /// <summary>回收全部活跃特效（场景切换/Shutdown 用）。</summary>
        public void StopAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Recycle(active[i]);
        }

        /// <summary>清空全部池（Shutdown 用）。</summary>
        public void ClearPools()
        {
            StopAll();
            foreach (var p in pools.Values) p.Pool.Clear();
            pools.Clear();
        }
    }
}
