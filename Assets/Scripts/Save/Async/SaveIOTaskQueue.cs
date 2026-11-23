using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>
    /// 存档 IO 异步队列：后台线程串行执行，主线程回调（经 UnitySynchronizationContext）。
    /// 同一槽位并发写入自动排队。帧耗时超阈值打警告（仅 Editor/DevBuild）。
    /// </summary>
    public class SaveIOTaskQueue : MonoBehaviour
    {
        public static SaveIOTaskQueue Instance { get; private set; }

        readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
        readonly SemaphoreSlim slotLocks = new SemaphoreSlim(1, 1);
        [Tooltip("帧耗时警告阈值（ms）")]
        public float warnThresholdMs = 16f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// 异步执行后台操作，完成后主线程回调。
        /// 同一槽位串行（通过 slotLocks 全局锁；项目为 3 槽，全局串行足够简单且安全）。
        /// </summary>
        public void Enqueue(Action backgroundWork, Action onComplete = null, Action<Exception> onError = null)
        {
            Task.Run(() =>
            {
                float swStart = Time.realtimeSinceStartup;
                try
                {
                    slotLocks.Wait();
                    try { backgroundWork?.Invoke(); }
                    finally { slotLocks.Release(); }
                    if (onComplete != null) mainThreadActions.Enqueue(onComplete);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveQueue] 异步操作异常：{e}");
                    if (onError != null) mainThreadActions.Enqueue(() => onError?.Invoke(e));
                }
                float elapsed = (Time.realtimeSinceStartup - swStart) * 1000f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (elapsed > warnThresholdMs)
                    Debug.LogWarning($"[SaveQueue] 后台操作耗时 {elapsed:F1}ms（>{warnThresholdMs}ms 阈值）。");
#endif
            });
        }

        void Update()
        {
            // 主线程回调
            while (mainThreadActions.TryDequeue(out var action))
                action?.Invoke();
        }

        /// <summary>当前队列中是否有待处理项（诊断用）。</summary>
        public bool IsBusy
        {
            get
            {
                slotLocks.Wait(0);
                var available = slotLocks.CurrentCount > 0;
                if (available) slotLocks.Release();
                return !available;
            }
        }
    }
}
