using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>调度句柄（T8-04）：可取消。</summary>
    public sealed class ScheduleHandle
    {
        public bool Cancelled { get; internal set; }
        public bool Completed { get; internal set; }

        internal double fireTime;
        internal float interval;
        internal int remainingRepeats; // -1 = 无限
        internal bool repeating;
        internal Action action;
    }

    /// <summary>
    /// 调度器（T8-04）：延迟/重复/下一帧/取消；由 SchedulerRunner（MonoBehaviour）帧驱动。
    /// 时间源 unscaledTime（UseUnscaledTime=false 切换 scaledTime）。
    /// </summary>
    public static class Scheduler
    {
        public static bool UseUnscaledTime = true;

        static readonly List<ScheduleHandle> pending = new List<ScheduleHandle>();
        internal static SchedulerRunner runner;

        /// <summary>延迟 seconds 秒执行（返回句柄可取消）。</summary>
        public static ScheduleHandle Delay(float seconds, Action action)
        {
            EnsureRunner();
            var h = new ScheduleHandle { action = action, fireTime = Now() + Math.Max(0f, seconds) };
            pending.Add(h);
            return h;
        }

        /// <summary>每 intervalSeconds 秒重复执行；repeatCount&lt;0 无限。</summary>
        public static ScheduleHandle Repeat(float intervalSeconds, Action action, int repeatCount = -1)
        {
            EnsureRunner();
            var h = new ScheduleHandle
            {
                action = action,
                interval = Math.Max(0.001f, intervalSeconds),
                remainingRepeats = repeatCount,
                repeating = true,
                fireTime = Now() + Math.Max(0.001f, intervalSeconds),
            };
            pending.Add(h);
            return h;
        }

        /// <summary>下一帧执行。</summary>
        public static ScheduleHandle NextFrame(Action action) => Delay(0f, action);

        public static void Cancel(ScheduleHandle handle)
        {
            if (handle == null) return;
            handle.Cancelled = true;
        }

        public static void CancelAll()
        {
            foreach (var h in pending) h.Cancelled = true;
            pending.Clear();
        }

        /// <summary>当前待执行任务数（诊断）。</summary>
        public static int PendingCount => pending.Count;

        internal static double Now() => UseUnscaledTime ? Time.unscaledTimeAsDouble : (double)Time.time;

        /// <summary>帧驱动（SchedulerRunner.Update 调用）。</summary>
        internal static void Tick()
        {
            double now = Now();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var h = pending[i];
                if (h.Cancelled) { pending.RemoveAt(i); continue; }
                if (now < h.fireTime) continue;

                if (h.repeating)
                {
                    h.fireTime += h.interval;
                    if (h.remainingRepeats > 0 && --h.remainingRepeats == 0)
                    {
                        pending.RemoveAt(i);
                        h.Completed = true;
                    }
                }
                else
                {
                    pending.RemoveAt(i);
                    h.Completed = true;
                }

                try { h.action?.Invoke(); }
                catch (Exception e)
                {
                    Debug.LogException(new Exception("[Scheduler] 任务执行异常（已隔离）", e));
                }
            }
        }

        internal static void AttachRunner(SchedulerRunner r)
        {
            if (runner != null && runner != r)
            {
                if (r != null) UnityEngine.Object.Destroy(r.gameObject);
                return;
            }
            runner = r;
        }

        static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("Systems_SchedulerRunner");
            go.AddComponent<SchedulerRunner>();
        }
    }

    /// <summary>Scheduler 帧驱动器（DontDestroyOnLoad）。</summary>
    [AddComponentMenu("Cqw/Systems/SchedulerRunner")]
    [DefaultExecutionOrder(-150)]
    public class SchedulerRunner : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Scheduler.AttachRunner(this);
        }

        void Update() => Scheduler.Tick();
    }

    /// <summary>
    /// 冷却管理器（T8-04）：Start/IsReady/Remaining/Clear。
    /// 时间源与 Scheduler 一致（unscaled/scaled 可配置）。
    /// </summary>
    public static class CooldownManager
    {
        public static bool UseUnscaledTime = true;

        static readonly Dictionary<string, double> endTimeByKey = new Dictionary<string, double>();

        public static void Start(string key, float durationSeconds)
        {
            endTimeByKey[key] = (UseUnscaledTime ? Time.unscaledTimeAsDouble : (double)Time.time) + Math.Max(0f, durationSeconds);
        }

        public static bool IsReady(string key) => Remaining(key) <= 0f;

        public static float Remaining(string key)
        {
            double now = UseUnscaledTime ? Time.unscaledTimeAsDouble : (double)Time.time;
            return endTimeByKey.TryGetValue(key, out var end) ? (float)Math.Max(0, end - now) : 0f;
        }

        public static void Clear(string key) => endTimeByKey.Remove(key);
        public static void ClearAll() => endTimeByKey.Clear();
        public static int Count => endTimeByKey.Count;
    }
}
