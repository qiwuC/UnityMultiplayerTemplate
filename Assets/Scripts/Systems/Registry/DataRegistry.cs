using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>重复 ID 处理策略（T8-05）。</summary>
    public enum DuplicateIdPolicy
    {
        /// <summary>报错并拒绝注册（默认）。</summary>
        Error,
        /// <summary>覆盖旧条目。</summary>
        Overwrite
    }

    /// <summary>
    /// 数据注册表（T8-05）：按 ID 注册/查询/注销 + OnRegistered/OnUnregistered 事件。
    /// T 需有 string Id 属性，或构造时传入 idSelector。
    /// </summary>
    public class DataRegistry<T>
    {
        readonly Func<T, string> idSelector;
        readonly Dictionary<string, T> items = new Dictionary<string, T>();

        /// <summary>重复 ID 策略（默认 Error）。</summary>
        public DuplicateIdPolicy Policy { get; set; } = DuplicateIdPolicy.Error;

        public int Count => items.Count;
        public IEnumerable<string> Ids => items.Keys;

        /// <summary>注册成功事件。</summary>
        public event Action<T> OnRegistered;
        /// <summary>注销事件（Clear 时逐项触发）。</summary>
        public event Action<T> OnUnregistered;

        public DataRegistry(Func<T, string> idSelector = null)
        {
            if (idSelector != null)
            {
                this.idSelector = idSelector;
                return;
            }
            var prop = typeof(T).GetProperty("Id");
            if (prop == null || prop.PropertyType != typeof(string))
                throw new ArgumentException($"[DataRegistry<{typeof(T).Name}>] T 需有 string Id 属性或传入 idSelector。");
            this.idSelector = item => (string)prop.GetValue(item);
        }

        /// <summary>注册（返回是否成功；重复 ID 按 Policy 处理）。</summary>
        public bool Register(T item)
        {
            if (item == null) { Debug.LogError($"[DataRegistry<{typeof(T).Name}>] 注册失败：item 为空。"); return false; }
            var id = idSelector(item);
            if (string.IsNullOrEmpty(id)) { Debug.LogError($"[DataRegistry<{typeof(T).Name}>] 注册失败：ID 为空。"); return false; }

            if (items.ContainsKey(id))
            {
                if (Policy == DuplicateIdPolicy.Error)
                {
                    Debug.LogError($"[DataRegistry<{typeof(T).Name}>] 重复 ID：{id}（Policy=Error，拒绝注册）");
                    return false;
                }
                Debug.LogWarning($"[DataRegistry<{typeof(T).Name}>] 重复 ID：{id}（Policy=Overwrite，已覆盖）");
            }
            items[id] = item;
            OnRegistered?.Invoke(item);
            return true;
        }

        public bool Unregister(string id)
        {
            if (string.IsNullOrEmpty(id) || !items.TryGetValue(id, out var item)) return false;
            items.Remove(id);
            OnUnregistered?.Invoke(item);
            return true;
        }

        public bool TryGet(string id, out T item)
        {
            if (!string.IsNullOrEmpty(id) && items.TryGetValue(id, out item)) return true;
            item = default;
            return false;
        }

        /// <summary>按 ID 取（不存在返回 default）。</summary>
        public T Get(string id) => items.TryGetValue(id, out var v) ? v : default;

        /// <summary>全部条目（只读快照视图）。</summary>
        public IReadOnlyCollection<T> GetAll() => items.Values;

        public void Clear()
        {
            var snapshot = new List<T>(items.Values);
            items.Clear();
            foreach (var item in snapshot) OnUnregistered?.Invoke(item);
        }
    }

    /// <summary>注册表中心（T8-05）：按类型访问全局 DataRegistry&lt;T&gt;（惰性创建）。</summary>
    public static class DataRegistryHub
    {
        static readonly Dictionary<Type, object> registries = new Dictionary<Type, object>();

        /// <summary>获取（不存在则创建；T 无 Id 属性时需传 idSelector）。</summary>
        public static DataRegistry<T> Get<T>(Func<T, string> idSelector = null)
        {
            if (registries.TryGetValue(typeof(T), out var existing))
                return (DataRegistry<T>)existing;
            var registry = new DataRegistry<T>(idSelector);
            registries[typeof(T)] = registry;
            return registry;
        }

        /// <summary>是否已存在该类型注册表。</summary>
        public static bool Exists<T>() => registries.ContainsKey(typeof(T));

        /// <summary>清空全部（Shutdown 用）。</summary>
        public static void ClearAll()
        {
            foreach (var kv in registries)
            {
                var clear = kv.Value.GetType().GetMethod("Clear");
                clear?.Invoke(kv.Value, null);
            }
            registries.Clear();
        }
    }
}
