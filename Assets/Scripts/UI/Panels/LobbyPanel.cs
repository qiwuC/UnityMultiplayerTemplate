using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.UI
{
    public class LobbyPanel : UIPanelBase
    {
        public override string PanelId => "ui.lobby";
        RectTransform listContent;
        Button startBtn, readyBtn;
        bool isReady;

        void Start()
        { PlayerRegistry.OnPlayerRegistered += _ => RefreshList(); PlayerRegistry.OnPlayerUnregistered += _ => RefreshList(); }
        void OnDestroy()
        { PlayerRegistry.OnPlayerRegistered -= _ => RefreshList(); PlayerRegistry.OnPlayerUnregistered -= _ => RefreshList(); }

        protected override void OnOpen() { if (startBtn == null) Build(); RefreshList(); }
        protected override void OnClose() { }

        void Build()
        {
            UIBuilder.CreateImage(Root, "BG", new Color(0.08f, 0.09f, 0.13f, 0.97f)).raycastTarget = true;
            var t = UIBuilder.CreateText(Root, "T", "", 36, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            t.rectTransform.anchorMin = new Vector2(0.1f, 0.86f); t.rectTransform.anchorMax = new Vector2(0.9f, 0.95f);
            SetLocalizedText(t, "ui.lobby.title");

            var sc = UIBuilder.CreateScrollView(Root, "List", out listContent);
            var scRt = sc.GetComponent<RectTransform>();
            scRt.anchorMin = new Vector2(0.08f, 0.25f); scRt.anchorMax = new Vector2(0.92f, 0.82f);
            scRt.offsetMin = Vector2.zero; scRt.offsetMax = Vector2.zero;
            listContent.gameObject.AddComponent<VerticalLayoutGroup>().spacing = 4;

            readyBtn = UIBuilder.CreateButton(Root, "B_Ready", "ui.lobby.ready", out _, new Vector2(0, 50));
            readyBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.08f, 0.16f); readyBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.45f, 0.22f);
            readyBtn.onClick.AddListener(() => { isReady = !isReady; CheckGate(); });
            startBtn = UIBuilder.CreateButton(Root, "B_Start", "ui.lobby.start", out _, new Vector2(0, 50), new Color(0.25f, 0.55f, 0.25f));
            startBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.55f, 0.16f); startBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.92f, 0.22f);
            startBtn.interactable = false; startBtn.onClick.AddListener(StartGame);
            var leaveBtn = UIBuilder.CreateButton(Root, "B_Leave", "ui.lobby.leave", out _, new Vector2(0, 44), new Color(0.55f, 0.25f, 0.25f));
            leaveBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.3f, 0.05f); leaveBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.7f, 0.12f);
            leaveBtn.onClick.AddListener(() => ConfirmDialog.Instance?.Show("ui.confirm.leaveTitle", "ui.confirm.leaveMessage", () => SessionManager.Instance?.LeaveSession()));
        }

        void RefreshList()
        {
            if (listContent == null) return;
            foreach (Transform c in listContent) Destroy(c.gameObject);
            foreach (var p in PlayerRegistry.GetAll())
            {
                if (p == null) continue;
                var txt = UIBuilder.CreateText(listContent, "E" + p.netId, p.DisplayName + " | " + p.CharacterType + " | " + (p.SteamId.Length > 12 ? p.SteamId.Substring(0, 12) + "…" : p.SteamId) + " | T:" + p.TeamId, 22);
                txt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            }
        }

        void CheckGate() { startBtn.interactable = NetworkServer.active && isReady; }
        void StartGame()
        { var sm = FindAnyObjectByType<CqwNetworkSceneManager>(); if (sm != null) sm.LoadScene("GameScene"); }
    }
}

