using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>
    /// UI 管理器（单例）：Canvas 层级构建、面板注册/打开/关闭、栈式返回、互斥显示。
    /// 挂在 UIRoot 对象上（DontDestroyOnLoad，跨场景复用同一套 Canvas）。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        /// <summary>Canvas 根（各层级为其子 Canvas）。</summary>
        public RectTransform CanvasRoot { get; private set; }

        readonly System.Collections.Generic.Stack<UIPanelBase> panelStack = new System.Collections.Generic.Stack<UIPanelBase>();
        bool booted;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start() => Boot();

        /// <summary>构建 Canvas 层级（幂等）。</summary>
        public void Boot()
        {
            if (booted) return;
            booted = true;

            CanvasRoot = CreateLayerCanvas("UICanvasRoot", 0).GetComponent<RectTransform>();
            CreateLayer("Background", UICanvasLayers.Background);
            CreateLayer("Main", UICanvasLayers.Main);
            CreateLayer("Panel", UICanvasLayers.Panel);
            CreateLayer("Popup", UICanvasLayers.Popup);
            CreateLayer("Toast", UICanvasLayers.Toast);
            CreateLayer("Loading", UICanvasLayers.Loading);
            Debug.Log("[UI] Canvas 层级构建完成。");
        }
        RectTransform CreateLayerCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        RectTransform CreateLayer(string name, int sortingOrder)
        {
            var layerGo = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)layerGo.transform;
            rt.SetParent(CanvasRoot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var canvas = layerGo.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            layerGo.AddComponent<GraphicRaycaster>();
            return rt;
        }

        /// <summary>取层级 RectTransform（面板挂接用）。</summary>
        public RectTransform GetLayer(string layerName)
        {
            Boot();
            if (CanvasRoot == null) return null;
            var t = CanvasRoot.Find(layerName);
            return t as RectTransform;
        }

        /// <summary>注册面板（挂接到指定层并注册 id）。</summary>
        public T RegisterPanel<T>(T panel, string layerName = "Panel") where T : UIPanelBase
        {
            Boot();
            var layer = GetLayer(layerName);
            if (layer != null)
            {
                var rt = panel.GetComponent<RectTransform>();
                if (rt != null) rt.SetParent(layer, false);
            }
            panel.gameObject.SetActive(false);
            UIPanelRegistry.Register(panel);
            return panel;
        }

        /// <summary>按 id 打开面板（入栈；互斥：模态面板打开时关闭栈顶非模态）。</summary>
        public void OpenPanel(string panelId)
        {
            var panel = UIPanelRegistry.Get(panelId);
            if (panel == null) { Debug.LogWarning($"[UI] 面板不存在：{panelId}"); return; }

            if (panel.IsModal && panelStack.Count > 0 && panelStack.Peek() != panel)
            {
                var top = panelStack.Peek();
                if (top.CloseOnBack) { panelStack.Pop(); top.Close(); }
            }

            if (panelStack.Count > 0 && panelStack.Peek() == panel) return;
            panelStack.Push(panel);
            panel.Open();
            panel.OnRefresh();
        }

        /// <summary>按 id 关闭面板（出栈）。</summary>
        public void ClosePanel(string panelId)
        {
            var panel = UIPanelRegistry.Get(panelId);
            if (panel == null) return;
            if (panelStack.Count > 0 && panelStack.Peek() == panel) panelStack.Pop();
            panel.Close();
        }

        /// <summary>栈式返回：关闭栈顶。</summary>
        public void CloseTop(UIPanelBase except = null)
        {
            while (panelStack.Count > 0)
            {
                var top = panelStack.Peek();
                if (top == except) return;
                panelStack.Pop();
                top.Close();
                return;
            }
        }

        /// <summary>返回键处理：关闭栈顶（供 Esc/Back 按键调用）。</summary>
        public void HandleBack()
        {
            while (panelStack.Count > 0)
            {
                var top = panelStack.Peek();
                if (top.CloseOnBack) { CloseTop(); return; }
                panelStack.Pop();
            }
        }

        /// <summary>工具：设置可见性。</summary>
        public static void SetVisible(Component uiComponent, bool visible)
        {
            if (uiComponent != null) uiComponent.gameObject.SetActive(visible);
        }

        /// <summary>工具：设置 CanvasGroup 交互与透明。</summary>
        public static void SetInteractable(CanvasGroup group, bool interactable)
        {
            if (group == null) return;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
            group.alpha = interactable ? 1f : 0.5f;
        }
    }
}
