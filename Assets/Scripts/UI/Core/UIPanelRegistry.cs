using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>面板注册表：面板 id → 面板实例（UIManager.RegisterPanel 时维护）。</summary>
    public static class UIPanelRegistry
    {
        static readonly Dictionary<string, UIPanelBase> panels = new Dictionary<string, UIPanelBase>();

        public static void Register(UIPanelBase panel)
        {
            if (panel == null || string.IsNullOrEmpty(panel.PanelId)) return;
            panels[panel.PanelId] = panel;
        }

        public static void Unregister(UIPanelBase panel)
        {
            if (panel == null) return;
            if (panels.TryGetValue(panel.PanelId, out var existing) && existing == panel)
                panels.Remove(panel.PanelId);
        }

        public static UIPanelBase Get(string panelId) =>
            panels.TryGetValue(panelId, out var p) ? p : null;

        public static T Get<T>(string panelId) where T : UIPanelBase =>
            Get(panelId) as T;

        public static bool TryGet(string panelId, out UIPanelBase panel) =>
            panels.TryGetValue(panelId, out panel) && panel != null;

        public static IReadOnlyCollection<UIPanelBase> All => panels.Values;

        public static void Clear() => panels.Clear();
    }
}
