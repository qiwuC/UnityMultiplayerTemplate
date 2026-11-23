using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cqw.MultilayerFramework.Localization;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>UI 构建辅助：代码创建 uGUI 基础控件（全部走 TMP 文本）。</summary>
    public static class UIBuilder
    {
        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image CreateImage(Transform parent, string name, Color color, bool raycastTarget = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return img;
        }

        public static TMP_Text CreateText(Transform parent, string name, string text, int fontSize = 24,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color ?? Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button CreateButton(Transform parent, string name, string labelKey, out TMP_Text label,
            Vector2 size, Color? bg = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = bg ?? new Color(0.18f, 0.2f, 0.26f, 1f);

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.35f, 0.45f);
            colors.pressedColor = new Color(0.12f, 0.14f, 0.18f);
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            btn.colors = colors;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26;
            label.color = Color.white;
            label.raycastTarget = false;
            SetLocalized(label, labelKey);

            return btn;
        }

        public static Slider CreateSlider(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var slider = go.GetComponent<Slider>();

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(rt, false);
            bgRt.anchorMin = new Vector2(0, 0.35f); bgRt.anchorMax = new Vector2(1, 0.65f);
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f);
            bgGo.GetComponent<Image>().raycastTarget = false;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            var faRt = (RectTransform)fillArea.transform;
            faRt.SetParent(rt, false);
            faRt.anchorMin = new Vector2(0, 0.35f); faRt.anchorMax = new Vector2(1, 0.65f);
            faRt.offsetMin = new Vector2(6, 0); faRt.offsetMax = new Vector2(-6, 0);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fRt = (RectTransform)fill.transform;
            fRt.SetParent(faRt, false);
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.35f, 0.6f, 0.95f);
            fill.GetComponent<Image>().raycastTarget = false;

            var handleArea = new GameObject("Handle Area", typeof(RectTransform));
            var haRt = (RectTransform)handleArea.transform;
            haRt.SetParent(rt, false);
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10, 0); haRt.offsetMax = new Vector2(-10, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var hRt = (RectTransform)handle.transform;
            hRt.SetParent(haRt, false);
            hRt.sizeDelta = new Vector2(16, 0);
            handle.GetComponent<Image>().color = Color.white;

            slider.fillRect = fRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1;
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string name, Vector2 size, out TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.26f, 1f);

            var toggle = go.GetComponent<Toggle>();
            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            var cRt = (RectTransform)check.transform;
            cRt.SetParent(rt, false);
            cRt.anchorMin = new Vector2(0, 0.2f); cRt.anchorMax = new Vector2(0.12f, 0.8f);
            cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;
            check.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.45f);
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = check.GetComponent<Image>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lRt = (RectTransform)labelGo.transform;
            lRt.SetParent(rt, false);
            lRt.anchorMin = new Vector2(0.16f, 0); lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero; lRt.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 24;
            label.color = Color.white;
            return toggle;
        }

        public static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 size, string[] options)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.26f, 1f);
            var dd = go.GetComponent<TMP_Dropdown>();

            var label = CreateText(rt, "Label", "", 22, TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0, 0.2f); label.rectTransform.anchorMax = new Vector2(0.85f, 0.8f);
            dd.captionText = label;

            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var tRt = (RectTransform)template.transform;
            tRt.SetParent(rt, false);
            tRt.anchorMin = new Vector2(0, 0); tRt.anchorMax = Vector2.one;
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0, 2f);
            template.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.2f);
            template.SetActive(false);
            dd.template = tRt;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var vRt = (RectTransform)viewport.transform;
            vRt.SetParent(tRt, false);
            vRt.anchorMin = Vector2.zero; vRt.anchorMax = Vector2.one;
            vRt.offsetMin = Vector2.zero; vRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.05f);

            var content = new GameObject("Content", typeof(RectTransform));
            var ctRt = (RectTransform)content.transform;
            ctRt.SetParent(vRt, false);
            ctRt.anchorMin = new Vector2(0, 1); ctRt.anchorMax = Vector2.one;
            ctRt.pivot = new Vector2(0.5f, 1f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            var iRt = (RectTransform)item.transform;
            iRt.SetParent(ctRt, false);
            iRt.anchorMin = new Vector2(0, 0.5f); iRt.anchorMax = Vector2.one;
            iRt.pivot = new Vector2(0.5f, 0.5f);
            var itemToggle = item.GetComponent<Toggle>();
            var itemLabel = CreateText(iRt, "Item Label", "", 20, TextAlignmentOptions.Left);
            itemLabel.rectTransform.anchorMin = Vector2.zero; itemLabel.rectTransform.anchorMax = Vector2.one;
            itemLabel.rectTransform.offsetMin = new Vector2(8, 0); itemLabel.rectTransform.offsetMax = new Vector2(-8, 0);
            dd.itemText = itemLabel;
            // itemTemplate 由 TMP_Dropdown 内部管理；此处仅设置 itemText 与 template

            if (options != null) dd.AddOptions(new System.Collections.Generic.List<string>(options));
            dd.RefreshShownValue();
            return dd;
        }

        public static TMP_InputField CreateInputField(Transform parent, string name, Vector2 size, string placeholder = "")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.18f, 1f);
            var input = go.GetComponent<TMP_InputField>();

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var tRt = (RectTransform)textGo.transform;
            tRt.SetParent(rt, false);
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(10, 4); tRt.offsetMax = new Vector2(-10, -4);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 22; tmp.color = Color.white;
            input.textComponent = tmp;

            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            var phRt = (RectTransform)phGo.transform;
            phRt.SetParent(rt, false);
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10, 4); phRt.offsetMax = new Vector2(-10, -4);
            var ph = phGo.GetComponent<TextMeshProUGUI>();
            ph.fontSize = 22; ph.color = new Color(1, 1, 1, 0.35f); ph.text = placeholder;
            input.placeholder = ph;

            return input;
        }

        public static ScrollRect CreateScrollView(Transform parent, string name, out RectTransform contentRect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.25f);
            var scroll = go.GetComponent<ScrollRect>();

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vRt = (RectTransform)viewport.transform;
            vRt.SetParent(rt, false);
            vRt.anchorMin = Vector2.zero; vRt.anchorMax = Vector2.one;
            vRt.offsetMin = Vector2.zero; vRt.offsetMax = Vector2.zero;

            contentRect = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            contentRect.SetParent(vRt, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0, 0);

            scroll.viewport = vRt;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24;
            return scroll;
        }

        /// <summary>设置本地化键到 TMP 文本（经 LocalizationKeyHolder）。</summary>
        public static void SetLocalized(TMP_Text target, string key)
        {
            if (target == null || string.IsNullOrEmpty(key)) return;
            var holder = target.GetComponent<LocalizationKeyHolder>();
            if (holder == null) holder = target.gameObject.AddComponent<LocalizationKeyHolder>();
            holder.localizationKey = key;
            target.text = Localization.LocalizationService.TryGet(key, out var val)
                ? val
                : UIPanelBase.DisplayNameFromKey(key);
        }
    }
}
