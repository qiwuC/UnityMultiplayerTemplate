using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cqw.MultilayerFramework.Input;
using Cqw.MultilayerFramework.Localization;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>
    /// 键位设置面板（T10-02）：8 动作 × 2 方案（键鼠/手柄）的重绑定列表，
    /// 每行：动作名 + 当前绑定 + 重新绑定 + 重置；底部全部重置 + 返回。
    /// 经 SettingsPanel"键位设置"入口打开（UIManager.OpenPanel("ui.keybinds")）。
    /// </summary>
    public class KeybindsPanel : UIPanelBase
    {
        public override string PanelId => "ui.keybinds";
        public override bool IsModal => true;

        struct Row
        {
            public string actionName;
            public int bindingIndex;
            public TMP_Text bindingLabel;
        }

        readonly List<Row> rows = new List<Row>();
        RectTransform content;
        bool built;

        static readonly (string action, string key)[] Actions =
        {
            ("Move", "ui.action.move"),
            ("Look", "ui.action.look"),
            ("Interact", "ui.action.interact"),
            ("Attack", "ui.action.attack"),
            ("Defend", "ui.action.defend"),
            ("PushToTalk", "ui.action.pushtotalk"),
            ("Jump", "ui.action.jump"),
            ("CameraSwitch", "ui.action.cameraswitch"),
        };

        static readonly string[] Groups = { "KeyboardMouse", "Gamepad" };

        protected override void OnOpen()
        {
            if (!built) Build();
            RefreshRows();
        }

        protected override void OnClose()
        {
            RebindManager.CancelRebind();
        }

        void Build()
        {
            built = true;
            UIBuilder.CreateImage(Root, "BG", new Color(0.08f, 0.09f, 0.13f, 0.97f));

            var title = UIBuilder.CreateText(Root, "Title", "", 34, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            title.rectTransform.anchorMin = new Vector2(0.1f, 0.88f);
            title.rectTransform.anchorMax = new Vector2(0.9f, 0.96f);
            SetLocalizedText(title, "ui.keybinds.title");

            var scroll = UIBuilder.CreateScrollView(Root, "Scroll", out content);
            var srt = scroll.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.08f, 0.14f);
            srt.anchorMax = new Vector2(0.92f, 0.86f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(16, 16, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            foreach (var a in Actions)
                foreach (var g in Groups)
                    AddRow(a.action, a.key, g);

            // 底部：全部重置 + 返回
            var resetAll = UIBuilder.CreateButton(Root, "Btn_ResetAll", "ui.keybinds.resetAll", out _, new Vector2(0, 44));
            var rar = resetAll.GetComponent<RectTransform>();
            rar.anchorMin = new Vector2(0.08f, 0.03f); rar.anchorMax = new Vector2(0.5f, 0.11f);
            rar.offsetMin = Vector2.zero; rar.offsetMax = Vector2.zero;
            resetAll.onClick.AddListener(() =>
            {
                RebindManager.ResetAllBindings();
                RefreshRows();
            });

            var back = UIBuilder.CreateButton(Root, "Btn_Back", "ui.keybinds.back", out _, new Vector2(0, 44));
            var brt = back.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.03f); brt.anchorMax = new Vector2(0.92f, 0.11f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            back.onClick.AddListener(Close);
        }

        void AddRow(string actionName, string displayKey, string group)
        {
            int idx = RebindManager.FindBindingIndex(actionName, group);
            if (idx < 0) return;

            var row = new GameObject("Row_" + actionName + "_" + group, typeof(RectTransform));
            row.transform.SetParent(content, false);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 32;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // 动作名（含方案后缀）
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(row.transform, false);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            nameText.fontSize = 20;
            nameText.color = Color.white;
            nameText.alignment = TMPro.TextAlignmentOptions.Left;
            SetLocalizedText(nameText, displayKey);
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 0.4f;

            // 当前绑定
            var bindGo = new GameObject("Binding", typeof(RectTransform), typeof(TextMeshProUGUI));
            bindGo.transform.SetParent(row.transform, false);
            var bindText = bindGo.GetComponent<TextMeshProUGUI>();
            bindText.fontSize = 20;
            bindText.color = new Color(0.7f, 0.85f, 1f);
            bindText.alignment = TMPro.TextAlignmentOptions.Left;
            var bindLe = bindGo.AddComponent<LayoutElement>();
            bindLe.flexibleWidth = 0.3f;

            // 重新绑定按钮
            var rebindBtn = UIBuilder.CreateButton(row.transform, "Btn_Rebind", "ui.keybinds.rebind", out _, new Vector2(0, 26));
            var rebindLe = rebindBtn.gameObject.AddComponent<LayoutElement>();
            rebindLe.flexibleWidth = 0.15f;
            var capturedAction = actionName;
            var capturedGroup = group;
            rebindBtn.onClick.AddListener(() =>
            {
                int bi = RebindManager.FindBindingIndex(capturedAction, capturedGroup);
                if (bi < 0) return;
                if (RebindManager.IsRebinding) RebindManager.CancelRebind();
                if (RebindManager.StartRebind(capturedAction, bi, (ok, msg) =>
                {
                    if (!ok && msg != "cancelled")
                        ToastManager.Instance?.Show(ToastLevel.Warning, "ui.keybinds.conflict");
                    RefreshRows();
                }))
                {
                    bindText.text = LocalizationService.Get("ui.keybinds.listening");
                }
            });

            // 重置按钮
            var resetBtn = UIBuilder.CreateButton(row.transform, "Btn_Reset", "ui.keybinds.reset", out _, new Vector2(0, 26));
            var resetLe = resetBtn.gameObject.AddComponent<LayoutElement>();
            resetLe.flexibleWidth = 0.15f;
            resetBtn.onClick.AddListener(() =>
            {
                int bi = RebindManager.FindBindingIndex(capturedAction, capturedGroup);
                if (bi >= 0) RebindManager.ResetBinding(capturedAction, bi);
                RefreshRows();
            });

            rows.Add(new Row { actionName = actionName, bindingIndex = idx, bindingLabel = bindText });
        }

        void RefreshRows()
        {
            foreach (var r in rows)
            {
                if (r.bindingLabel == null) continue;
                r.bindingLabel.text = GetBindingDisplay(r.actionName, r.bindingIndex);
            }
        }

        static string GetBindingDisplay(string actionName, int idx)
        {
            var action = RebindManager.GetAction(actionName);
            if (action == null || idx < 0 || idx >= action.bindings.Count) return "-";
            var b = action.bindings[idx];
            var path = string.IsNullOrEmpty(b.overridePath) ? b.path : b.overridePath;
            return Prettify(path);
        }

        /// <summary>"&lt;Keyboard&gt;/w" → "W"；"&lt;Gamepad&gt;/buttonSouth" → "Button South"。</summary>
        static string Prettify(string path)
        {
            if (string.IsNullOrEmpty(path)) return "-";
            var s = path;
            if (s.StartsWith("<"))
            {
                int end = s.IndexOf('>');
                if (end >= 0) s = s.Substring(end + 1).TrimStart('/');
            }
            s = s.Replace('/', ' ');
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && (char.IsLower(s[i - 1]) || s[i - 1] == ' ')) sb.Append(' ');
                sb.Append(s[i]);
            }
            s = sb.ToString().Trim();
            return s.Length <= 2 ? s.ToUpperInvariant() : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
