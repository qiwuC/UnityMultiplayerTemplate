using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.UI
{
    public class MainMenuPanel : UIPanelBase
    {
        public override string PanelId => "ui.mainMenu";

        Button hostBtn, joinBtn, continueBtn, saveBtn, settingsBtn, quitBtn;
        ISaveSlotService SaveService => SaveSlotServiceLocator.Service;

        protected override void OnOpen() { if (hostBtn == null) Build(); RefreshContinue(); }
        protected override void OnClose() { }
        public override void OnRefresh() => RefreshContinue();

        void Build()
        {
            if (hostBtn != null) return;
            UIBuilder.CreateImage(Root, "BG", new Color(0.08f, 0.09f, 0.13f, 1f)).raycastTarget = true;
            var title = UIBuilder.CreateText(Root, "Title", "", 48, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            title.rectTransform.anchorMin = new Vector2(0.2f, 0.82f); title.rectTransform.anchorMax = new Vector2(0.8f, 0.92f);
            SetLocalizedText(title, "ui.mainMenu.title");

            var area = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(Root, false);
            area.anchorMin = new Vector2(0.3f, 0.1f); area.anchorMax = new Vector2(0.7f, 0.78f);
            var lg = area.gameObject.AddComponent<VerticalLayoutGroup>();
            lg.spacing = 14; lg.childControlWidth = true; lg.childControlHeight = false;
            lg.childForceExpandWidth = true; lg.childForceExpandHeight = false;

            hostBtn = UIBuilder.CreateButton(area, "B_Host", "ui.mainMenu.host", out _, new Vector2(0, 56));
            joinBtn = UIBuilder.CreateButton(area, "B_Join", "ui.mainMenu.join", out _, new Vector2(0, 56));
            continueBtn = UIBuilder.CreateButton(area, "B_Continue", "ui.mainMenu.continue", out _, new Vector2(0, 56));
            saveBtn = UIBuilder.CreateButton(area, "B_Save", "ui.mainMenu.saveManagement", out _, new Vector2(0, 56));
            settingsBtn = UIBuilder.CreateButton(area, "B_Settings", "ui.mainMenu.settings", out _, new Vector2(0, 56));
            quitBtn = UIBuilder.CreateButton(area, "B_Quit", "ui.mainMenu.quit", out _, new Vector2(0, 56), new Color(0.55f, 0.25f, 0.25f));

            hostBtn.onClick.AddListener(OnHost);
            joinBtn.onClick.AddListener(OnJoin);
            continueBtn.onClick.AddListener(OnContinue);
            saveBtn.onClick.AddListener(() => UIManager.Instance?.OpenPanel("ui.saveSlots"));
            settingsBtn.onClick.AddListener(() => UIManager.Instance?.OpenPanel("ui.settings"));
            quitBtn.onClick.AddListener(() =>
                ConfirmDialog.Instance?.Show("ui.confirm.quitTitle", "ui.confirm.quitMessage", () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }));
        }

        void RefreshContinue()
        {
            var slot = SaveService != null ? SaveService.GetLastUsedSlot() : null;
            continueBtn.interactable = slot != null;
        }

        void OnHost()
        {
            var s = SessionManager.Instance;
            if (s == null) { Debug.LogError("[MainMenu] SessionManager missing."); return; }
            ToastManager.Instance?.Show(s.CreateSession() ? ToastLevel.Success : ToastLevel.Error,
                s.CreateSession() ? "ui.toast.hostSuccess" : "ui.toast.hostFailed");
        }

        void OnJoin()
        {
            var s = SessionManager.Instance;
            if (s == null) { Debug.LogError("[MainMenu] SessionManager missing."); return; }
            if (s.JoinSession("localhost"))
                ToastManager.Instance?.Show(ToastLevel.Info, "ui.toast.joining");
            else
                ToastManager.Instance?.Show(ToastLevel.Error, "ui.toast.joinFailed");
        }

        void OnContinue()
        {
            var slot = SaveService?.GetLastUsedSlot();
            if (slot == null) { ToastManager.Instance?.Show(ToastLevel.Warning, "ui.toast.noSaveData"); return; }
            Debug.Log("[MainMenu] Continue: loading slot " + slot.SlotIndex);
        }

        void OnSaveManagement() => UIManager.Instance?.OpenPanel("ui.saveSlots");
        void OnSettings() => UIManager.Instance?.OpenPanel("ui.settings");
    }
}
