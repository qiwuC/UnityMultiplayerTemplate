using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cqw.MultilayerFramework.Localization;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>
    /// UI 面板基类：生命周期（OnOpen/OnClose/OnRefresh）、模态/返回语义、本地化便捷方法。
    /// </summary>
    public abstract class UIPanelBase : MonoBehaviour
    {
        public abstract string PanelId { get; }
        public virtual bool IsModal => false;
        public virtual bool CloseOnBack => true;

        public RectTransform Root { get; private set; }
        public bool IsOpen { get; private set; }

        public event Action<UIPanelBase> Opened;
        public event Action<UIPanelBase> Closed;

        protected virtual void Awake()
        {
            if (Root == null) Root = GetComponent<RectTransform>();
        }

        public void Open()
        {
            if (Root == null) Root = GetComponent<RectTransform>();
            gameObject.SetActive(true);
            IsOpen = true;
            OnOpen();
            Opened?.Invoke(this);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnClose();
            gameObject.SetActive(false);
            Closed?.Invoke(this);
        }

        public virtual void OnRefresh() { }

        protected abstract void OnOpen();
        protected abstract void OnClose();

        // ============ 本地化接入点 ============

        /// <summary>
        /// 本地化便捷方法：给 TMP 文本设置本地化键（经 LocalizationKeyHolder 预留）。
        /// 阶段七本地化系统落地后按 key 查表；本阶段显示可读占位（键最后一段标题化）。
        /// </summary>
        protected void SetLocalizedText(TMP_Text target, string key, params object[] args)
        {
            if (target == null || string.IsNullOrEmpty(key)) return;
            var holder = target.GetComponent<LocalizationKeyHolder>();
            if (holder == null) holder = target.gameObject.AddComponent<LocalizationKeyHolder>();
            holder.localizationKey = key;
            // 真实本地化查询（缺键时回退→en→DisplayNameFromKey 兜底）
            target.text = Localization.LocalizationService.TryGet(key, out var val)
                ? val
                : DisplayNameFromKey(key);
        }

        /// <summary>键 → 可读占位文本（"ui.menu.hostGame" → "Host Game"，camelCase 转标题）。</summary>
        public static string DisplayNameFromKey(string key)
        {
            var last = key.Substring(key.LastIndexOf('.') + 1);
            var sb = new System.Text.StringBuilder(last.Length + 4);
            bool wordStart = true;
            for (int i = 0; i < last.Length; i++)
            {
                var c = last[i];
                if (c == '-' || c == '_') { sb.Append(' '); wordStart = true; continue; }
                if (wordStart) { sb.Append(char.ToUpper(c)); wordStart = false; }
                else
                {
                    // camelCase：小写→大写边界处插入空格
                    if (char.IsUpper(c) && i > 0 && char.IsLower(last[i - 1])) sb.Append(' ');
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 本地化或标题占位（阶段九交互提示等非面板文本的统一入口；缺键回退 DisplayNameFromKey）。
        /// 新增方法（不改变既有 API 签名）。
        /// </summary>
        public static string LocalizedOrTitle(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            return Localization.LocalizationService.TryGet(key, out var val) ? val : DisplayNameFromKey(key);
        }
    }
}
