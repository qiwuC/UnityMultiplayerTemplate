using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json;

namespace Cqw.MultilayerFramework.Input
{
    /// <summary>重绑定记录（JSON 持久化单元）。</summary>
    [Serializable]
    public class RebindRecord
    {
        public string map;
        public string action;
        public int index;
        public string path;
    }

    /// <summary>
    /// 键位重绑定（T10-02）：交互式重绑定、同图冲突检测、恢复默认、JSON 持久化
    /// （Application.persistentDataPath/rebinds.json，启动自动加载）。
    /// 持有共享 PlayerControls 实例（全框架唯一动作源）。
    /// </summary>
    public static class RebindManager
    {
        static PlayerControls controls;
        static InputActionRebindingExtensions.RebindingOperation operation;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "rebinds.json");

        /// <summary>绑定集变化（重绑定/重置/加载后广播；KeybindsPanel 刷新）。</summary>
        public static event Action OnBindingsChanged;

        /// <summary>共享动作实例（惰性创建 + 自动加载持久化覆盖）。</summary>
        public static PlayerControls Controls
        {
            get
            {
                if (controls == null)
                {
                    controls = new PlayerControls();
                    LoadFromJson(SavePath);
                }
                return controls;
            }
        }

        public static bool IsRebinding => operation != null;

        public static InputAction GetAction(string actionName)
        {
            foreach (var map in Controls.asset.actionMaps)
            {
                var action = map.FindAction(actionName, throwIfNotFound: false);
                if (action != null) return action;
            }
            return null;
        }

        /// <summary>按动作名 + 控制方案组解析绑定索引（优先非复合绑定；复合取该组首个子项）。</summary>
        public static int FindBindingIndex(string actionName, string deviceGroup)
        {
            var action = GetAction(actionName);
            if (action == null) return -1;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isComposite && !b.isPartOfComposite && b.groups == deviceGroup) return i;
            }
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isPartOfComposite && b.groups == deviceGroup) return i;
            }
            return -1;
        }

        /// <summary>
        /// 开始交互式重绑定（捕获下一次输入；Esc 取消）。
        /// onComplete(success, path 或失败原因)：冲突时返回冲突动作名并回滚本次覆盖。
        /// </summary>
        public static bool StartRebind(string actionName, int bindingIndex, Action<bool, string> onComplete)
        {
            var action = GetAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return false;

            CancelRebind();
            operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    var path = action.bindings[bindingIndex].overridePath;
                    DisposeOp();
                    if (string.IsNullOrEmpty(path)) { onComplete?.Invoke(false, "cancelled"); return; }

                    var conflict = FindConflict(action, bindingIndex, path);
                    if (!string.IsNullOrEmpty(conflict))
                    {
                        action.RemoveBindingOverride(bindingIndex);
                        onComplete?.Invoke(false, conflict);
                        return;
                    }
                    SaveToJson(SavePath);
                    OnBindingsChanged?.Invoke();
                    onComplete?.Invoke(true, path);
                })
                .OnCancel(_ => { DisposeOp(); onComplete?.Invoke(false, "cancelled"); });
            operation.Start();
            return true;
        }

        public static void CancelRebind()
        {
            if (operation == null) return;
            operation.Cancel();
            DisposeOp();
        }

        static void DisposeOp()
        {
            operation?.Dispose();
            operation = null;
        }

        /// <summary>同图冲突检测：其余动作的有效绑定中出现相同控制路径时返回其动作名。</summary>
        static string FindConflict(InputAction source, int sourceIndex, string path)
        {
            foreach (var other in source.actionMap.actions)
            {
                if (other == source) continue;
                for (int i = 0; i < other.bindings.Count; i++)
                {
                    var b = other.bindings[i];
                    var effective = string.IsNullOrEmpty(b.overridePath) ? b.path : b.overridePath;
                    if (string.Equals(effective, path, StringComparison.OrdinalIgnoreCase))
                        return other.name;
                }
            }
            return null;
        }

        public static void ResetBinding(string actionName, int bindingIndex)
        {
            var action = GetAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;
            action.RemoveBindingOverride(bindingIndex);
            SaveToJson(SavePath);
            OnBindingsChanged?.Invoke();
        }

        public static void ResetAllBindings()
        {
            foreach (var map in Controls.asset.actionMaps)
                foreach (var action in map.actions)
                    action.RemoveAllBindingOverrides();
            SaveToJson(SavePath);
            OnBindingsChanged?.Invoke();
        }

        public static void SaveToJson(string path)
        {
            try
            {
                var records = new List<RebindRecord>();
                foreach (var map in Controls.asset.actionMaps)
                {
                    for (int i = 0; i < map.actions.Count; i++)
                    {
                        var action = map.actions[i];
                        for (int b = 0; b < action.bindings.Count; b++)
                        {
                            var over = action.bindings[b].overridePath;
                            if (string.IsNullOrEmpty(over)) continue;
                            records.Add(new RebindRecord { map = map.name, action = action.name, index = b, path = over });
                        }
                    }
                }
                File.WriteAllText(path, JsonConvert.SerializeObject(records, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rebind] 保存失败：{e.Message}");
            }
        }

        public static bool LoadFromJson(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var records = JsonConvert.DeserializeObject<List<RebindRecord>>(File.ReadAllText(path));
                if (records == null) return false;
                foreach (var r in records)
                {
                    var map = Controls.asset.FindActionMap(r.map, throwIfNotFound: false);
                    var action = map != null ? map.FindAction(r.action, throwIfNotFound: false) : null;
                    if (action == null || r.index < 0 || r.index >= action.bindings.Count) continue;
                    action.ApplyBindingOverride(r.index, r.path);
                }
                OnBindingsChanged?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rebind] 加载失败：{e.Message}");
                return false;
            }
        }
    }
}
