using UnityEngine;
using UnityEngine.UI;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>UI 面板引导器：挂 UIManager 同对象，Boot 后自动创建并注册全部面板。</summary>
    [RequireComponent(typeof(UIManager))]
    [AddComponentMenu("Cqw/UI/UIPanelBootstrap")]
    public class UIPanelBootstrap : MonoBehaviour
    {
        void Start()
        {
            var mgr = GetComponent<UIManager>();
            mgr.Boot();
            CreatePanels(mgr);
            ChatNetwork.EnsureRegistered();
            Debug.Log("[UI] 全部面板注册完成。");
        }

        void CreatePanels(UIManager mgr)
        {
            // Panel 层
            CreateAndRegister<MainMenuPanel>(mgr, "Panel");
            CreateAndRegister<SettingsPanel>(mgr, "Panel");
            CreateAndRegister<LobbyPanel>(mgr, "Panel");
            CreateAndRegister<PausePanel>(mgr, "Panel");
            CreateAndRegister<SaveSlotPanel>(mgr, "Panel");
            CreateAndRegister<HudPanel>(mgr, "Panel");
            CreateAndRegister<ChatPanel>(mgr, "Panel");
            CreateAndRegister<KeybindsPanel>(mgr, "Panel");

            // Popup 层
            if (ConfirmDialog.Instance == null)
            {
                var go = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(ConfirmDialog));
                go.transform.SetParent(mgr.GetLayer("Popup"), false);
            }

            // Loading 层
            CreateAndRegister<LoadingPanel>(mgr, "Loading");

            // Main 层：交互提示（屏幕固定）
            var ipGo = new GameObject("InteractionPromptScreen", typeof(RectTransform), typeof(InteractionPromptScreen), typeof(Image));
            ipGo.transform.SetParent(mgr.GetLayer("Main"), false);
            var ips = ipGo.GetComponent<InteractionPromptScreen>();
            ips.SetDeviceProvider(new DefaultInputDeviceProvider(null));
        }

        void CreateAndRegister<T>(UIManager mgr, string layer) where T : UIPanelBase
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(mgr.GetLayer(layer), false);
            mgr.RegisterPanel(go.GetComponent<T>(), layer);
        }
    }
}
