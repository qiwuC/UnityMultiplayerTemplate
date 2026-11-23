using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cqw.MultilayerFramework.Player;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>鑱婂ぉ闈㈡澘锛氭秷鎭垪琛?+ 杈撳叆妗?+ 鍙戦€?+ 棰戦亾鍒囨崲銆?/summary>
    public class ChatPanel : UIPanelBase
    {
        public override string PanelId => "ui.chat";

        RectTransform messagesContent;
        Button sendBtn;
        TMP_InputField inputField;
        ChatChannel currentChannel = ChatChannel.Global;
        readonly HashSet<string> localEchoes = new HashSet<string>();

        protected override void OnOpen()
        {
            if (messagesContent == null) Build();
            ChatNetwork.EnsureRegistered();
            ChatNetwork.OnChatReceived += HandleChatReceived;
        }

        protected override void OnClose()
        {
            ChatNetwork.OnChatReceived -= HandleChatReceived;
            SetPlayerMovementEnabled(true);
        }

        void Build()
        {
            var bg = UIBuilder.CreateImage(Root, "BG", new Color(0, 0, 0, 0.35f));
            bg.raycastTarget = true;

            var scroll = UIBuilder.CreateScrollView(Root, "Messages", out messagesContent);
            (scroll.GetComponent<RectTransform>()).anchorMin = new Vector2(0.02f, 0.22f);
            (scroll.GetComponent<RectTransform>()).anchorMax = new Vector2(0.4f, 0.75f);
            (scroll.GetComponent<RectTransform>()).offsetMin = Vector2.zero; (scroll.GetComponent<RectTransform>()).offsetMax = Vector2.zero;
            var vlg = messagesContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2; vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            // 杈撳叆妗?            inputField = UIBuilder.CreateInputField(Root, "ChatInput", new Vector2(380, 40), "Type a message鈥?);
            inputField.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.15f);
            inputField.GetComponent<RectTransform>().anchorMax = new Vector2(0.32f, 0.2f);
            inputField.GetComponent<RectTransform>().offsetMin = Vector2.zero; inputField.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            inputField.onFocusSelectAll = false;

            // 鍙戦€佹寜閽?            var sendBtn = UIBuilder.CreateButton(Root, "Btn_Send", "ui.chat.send", out _, new Vector2(80, 40));
            sendBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.33f, 0.15f);
            sendBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.38f, 0.2f);
            sendBtn.GetComponent<RectTransform>().offsetMin = Vector2.zero; sendBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            sendBtn.onClick.AddListener(SendCurrent);

            // 棰戦亾鍒囨崲
            var chanBtn = UIBuilder.CreateButton(Root, "Btn_Channel", "ui.chat.global", out _, new Vector2(80, 40));
            chanBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.09f);
            chanBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.08f, 0.14f);
            chanBtn.GetComponent<RectTransform>().offsetMin = Vector2.zero; chanBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            chanBtn.onClick.AddListener(ToggleChannel);

            // Enter 鍙戦€?            inputField.onValueChanged.AddListener(_ => { });
        }

        void ToggleChannel()
        {
            currentChannel = currentChannel == ChatChannel.Global ? ChatChannel.Team : ChatChannel.Global;
            var btn = Root.Find("Btn_Channel")?.GetComponent<TMP_Text>();
            if (btn != null)
                SetLocalizedText(btn, currentChannel == ChatChannel.Global ? "ui.chat.global" : "ui.chat.team");
        }

        void SendCurrent()
        {
            var text = inputField.text?.Trim();
            inputField.text = "";
            if (string.IsNullOrEmpty(text)) return; // 绌烘秷鎭涪寮?            if (text.Length > 200) text = text.Substring(0, 200); // 瓒呴暱鎴柇

            var localPlayer = PlayerRegistry.GetLocalPlayer();
            var senderName = localPlayer != null ? localPlayer.DisplayName : "Unknown";
            var senderTeam = localPlayer != null ? localPlayer.TeamId : -1;

            // 鏈湴鍥炴樉锛堥伩鍏嶇瓑寰呭線杩旓級
            LocalEcho(senderName, currentChannel, text);

            if (NetworkServer.active)
            {
                // Host锛氱洿鎺ユ湇鍔＄骞挎挱
                ChatNetwork.ServerBroadcast(senderName, currentChannel, text, senderTeam);
            }
            else if (NetworkClient.active)
            {
                ChatNetwork.ClientSend(currentChannel, text);
            }
        }

        void LocalEcho(string sender, ChatChannel channel, string text)
        {
            var echoKey = $"{sender}:{text}";
            if (localEchoes.Contains(echoKey)) return; // 鏈嶅姟鍣ㄥ箍鎾凡鍥炴樉锛屽幓閲?            localEchoes.Add(echoKey);
            AppendMessage(sender, channel, text);
        }

        void HandleChatReceived(ChatBroadcastMessage msg)
        {
            AppendMessage(msg.SenderName, (ChatChannel)msg.Channel, msg.Text);
        }

        void AppendMessage(string sender, ChatChannel channel, string text)
        {
            if (messagesContent == null) return;
            var prefix = channel == ChatChannel.Team ? "[Team] " : "";
            var txt = UIBuilder.CreateText(messagesContent, $"Msg_{messagesContent.childCount}", $"{prefix}{sender}: {text}", 20);
            var le = txt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 24;
            // 闄愬埗娑堟伅鏉℃暟
            if (messagesContent.childCount > 50)
                DestroyImmediate(messagesContent.GetChild(0).gameObject);
        }

        void SetPlayerMovementEnabled(bool enabled)
        {
            var lp = PlayerRegistry.GetLocalPlayer();
            if (lp != null)
            {
                var ctrl = lp.GetComponent<PlayerController>();
                if (ctrl != null) ctrl.InputEnabled = enabled;
            }
        }

        /// <summary>杈撳叆妗嗚仛鐒︾姸鎬佸彉鍖栵紙鐢卞閮?Update 妫€娴嬪苟璋冪敤锛岀鐢?鍚敤绉诲姩锛夈€?/summary>
        public void OnInputFocusChanged(bool focused) => SetPlayerMovementEnabled(!focused);

        public bool IsInputFocused => inputField != null && inputField.isFocused;
    }
}



