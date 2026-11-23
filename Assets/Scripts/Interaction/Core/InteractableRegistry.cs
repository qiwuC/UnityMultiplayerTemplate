using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>
    /// 可交互物注册表（T9-02）：注册/注销 + 范围查询 + 事件。
    /// 目标：避免每帧全场景扫描（Physics.OverlapSphere / FindObjectsOfType）。
    /// 查询为全表距离过滤（数量级小；如需空间划分可在 QueryRange 内替换实现，接口不变）。
    /// </summary>
    public static class InteractableRegistry
    {
        static readonly List<IInteractable> items = new List<IInteractable>();
        static readonly List<IInteractable> queryScratch = new List<IInteractable>();

        /// <summary>注册事件。</summary>
        public static event Action<IInteractable> OnRegistered;
        /// <summary>注销事件。</summary>
        public static event Action<IInteractable> OnUnregistered;

        /// <summary>当前注册数（诊断）。</summary>
        public static int Count => items.Count;

        public static void Register(IInteractable item)
        {
            if (item == null || items.Contains(item)) return;
            items.Add(item);
            OnRegistered?.Invoke(item);
        }

        public static void Unregister(IInteractable item)
        {
            if (item == null || !items.Remove(item)) return;
            OnUnregistered?.Invoke(item);
        }

        /// <summary>
        /// 范围查询：返回中心 radius 内的可交互物（只读快照，下次查询前有效）。
        /// 防御：已销毁未注销的条目（Unity 假 null）顺带清理。
        /// </summary>
        public static IReadOnlyList<IInteractable> QueryRange(Vector3 center, float radius)
        {
            queryScratch.Clear();
            float r2 = radius * radius;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it is Object unityObj && unityObj == null)
                {
                    items.RemoveAt(i);
                    continue;
                }
                var comp = it as Component;
                if (comp != null && (comp.transform.position - center).sqrMagnitude <= r2)
                    queryScratch.Add(it);
            }
            return queryScratch;
        }

        /// <summary>清空注册表（场景切换/测试用；正常生命周期由 OnDisable 驱动）。</summary>
        public static void Clear()
        {
            items.Clear();
        }
    }
}
