using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.UI
{
    public class PausePanel : UIPanelBase
    {
        public override string PanelId => "ui.pause";
        public override bool IsModal => true;

        protected override void OnOpen() { if (resumeBtn == null) Build(); }
        protected override void OnClose() { }

        Button resumeBtn;

        void Build()
        {
            UIBuilder.CreateImage(Root, "BG", new Color(0, 0, 0, 0.7f)).raycastTarget = true;
            var t = UIBuilder.CreateText(Root, "T", "", 40, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            t.rectTransform.anchorMin = new Vector2(0.25f, 0.72f); t.rectTransform.anchorMax = new Vector2(0.75f, 0.82f);
            SetLocalizedText(t, "ui.pause.title");

            var area = new GameObject("B", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(Root, false);
            area.anchorMin = new Vector2(0.32f, 0.15f); area.anchorMax = new Vector2(0.68f, 0.68f);
            var lg = area.gameObject.AddComponent<VerticalLayoutGroup>();
            lg.spacing = 14; lg.childControlWidth = true; lg.childControlHeight = false;
            lg.childForceExpandWidth = true; lg.childForceExpandHeight = false;

            resumeBtn = UIBuilder.CreateButton(area, "B_Resume", "ui.pause.resume", out _, new Vector2(0, 52));
            var setBtn = UIBuilder.CreateButton(area, "B_Set", "ui.pause.settings", out _, new Vector2(0, 52));
            var retBtn = UIBuilder.CreateButton(area, "B_Return", "ui.pause.returnToLobby", out _, new Vector2(0, 52));
            var quitBtn = UIBuilder.CreateButton(area, "B_Quit", "ui.pause.quitToDesktop", out _, new Vector2(0, 52), new Color(0.55f, 0.25f, 0.25f));

            resumeBtn.onClick.AddListener(() =>
            { SessionManager.Instance?.ResumeSession(); UIManager.Instance?.ClosePanel(PanelId); });
            setBtn.onClick.AddListener(() => UIManager.Instance?.OpenPanel("ui.settings"));
            retBtn.onClick.AddListener(() =>
                ConfirmDialog.Instance?.Show("ui.confirm.returnTitle", "ui.confirm.returnMessage", () =>
                { FindAnyObjectByType<CqwNetworkSceneManager>()?.LoadScene("MainMenu"); UIManager.Instance?.ClosePanel(PanelId); }));
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
    }
}

