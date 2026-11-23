using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cqw.MultilayerFramework.UI
{
    public enum ToastLevel { Info, Success, Warning, Error }
    /// <summary>Toast 提示管理（Toast 层常驻）：屏幕角落短提示，自动消失，多条堆叠，队列上限。</summary>
    public class ToastManager : MonoBehaviour
    {
        public static ToastManager Instance { get; private set; }

        [Tooltip("单条 Toast 显示时长（秒）")]
        public float defaultDuration = 2.5f;
        [Tooltip("最大同时显示条数")]
        public int maxVisible = 5;

        RectTransform toastLayer;
        RectTransform container;
        readonly List<ToastItem> active = new List<ToastItem>();

        static readonly Color[] levelColors =
        {
            new Color(0.35f, 0.6f, 0.95f),  // Info
            new Color(0.3f, 0.75f, 0.4f),   // Success
            new Color(0.9f, 0.7f, 0.25f),   // Warning
            new Color(0.85f, 0.3f, 0.3f)    // Error
        };

        class ToastItem
        {
            public GameObject root;
            public float remaining;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            var uiMgr = GetComponent<UIManager>();
            uiMgr.Boot();
            toastLayer = uiMgr.GetLayer("Toast");
            BuildContainer();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void BuildContainer()
        {
            if (toastLayer == null) return;
            var go = new GameObject("ToastContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            container = (RectTransform)go.transform;
            container.SetParent(toastLayer, false);
            container.anchorMin = new Vector2(1f, 0f);
            container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(1f, 0.5f);
            container.offsetMin = new Vector2(-460, 80);
            container.offsetMax = new Vector2(-20, -80);
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.LowerRight;
            vlg.spacing = 10;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
        }

        /// <summary>显示 Toast（messageKey 为本地化键）。</summary>
        public void Show(ToastLevel level, string messageKey, float? duration = null)
        {
            if (container == null) return;
            if (active.Count >= maxVisible)
            {
                var oldest = active[0];
                if (oldest.root != null) Destroy(oldest.root);
                active.RemoveAt(0);
            }

            var go = new GameObject($"Toast_{level}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(container, false);
            go.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.95f);

            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            var barRt = (RectTransform)bar.transform;
            barRt.SetParent(rt, false);
            barRt.anchorMin = Vector2.zero; barRt.anchorMax = new Vector2(0.02f, 1f);
            barRt.offsetMin = Vector2.zero; barRt.offsetMax = Vector2.zero;
            bar.GetComponent<Image>().color = levelColors[(int)level % levelColors.Length];
            bar.GetComponent<Image>().raycastTarget = false;

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 14, 12, 12);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var tRt = (RectTransform)textGo.transform;
            tRt.SetParent(rt, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 24;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            UIBuilder.SetLocalized(text, messageKey);

            active.Add(new ToastItem { root = go, remaining = duration ?? defaultDuration });
            go.transform.SetAsLastSibling();
        }

        void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].remaining -= Time.unscaledDeltaTime;
                if (active[i].remaining <= 0f)
                {
                    if (active[i].root != null) Destroy(active[i].root);
                    active.RemoveAt(i);
                }
            }
        }
    }
}
