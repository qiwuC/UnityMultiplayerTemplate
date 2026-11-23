using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>二次确认弹窗（单例）：Show(titleKey, messageKey, onConfirm, onCancel)。</summary>
    public class ConfirmDialog : MonoBehaviour
    {
        public static ConfirmDialog Instance { get; private set; }

        RectTransform root;
        TMP_Text titleText, messageText;
        Button confirmButton, cancelButton;

        event Action onConfirm;
        event Action onCancel;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
            Hide();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            var bg = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bg.transform;
            bgRt.SetParent(root, false);
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            var window = new GameObject("Window", typeof(RectTransform), typeof(Image));
            var wRt = (RectTransform)window.transform;
            wRt.SetParent(root, false);
            wRt.sizeDelta = new Vector2(640, 300);
            wRt.anchoredPosition = Vector2.zero;
            window.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.2f, 0.98f);

            titleText = UIBuilder.CreateText(wRt, "Title", "Confirm", 30, TMPro.TextAlignmentOptions.Center,
                new Color(1f, 0.85f, 0.4f));
            titleText.rectTransform.anchorMin = new Vector2(0, 0.68f);
            titleText.rectTransform.anchorMax = Vector2.one;
            titleText.rectTransform.offsetMin = new Vector2(24, 0); titleText.rectTransform.offsetMax = new Vector2(-24, -18);
            UIBuilder.SetLocalized(titleText, "ui.confirm.title");

            messageText = UIBuilder.CreateText(wRt, "Message", "", 24, TMPro.TextAlignmentOptions.Left);
            messageText.rectTransform.anchorMin = new Vector2(0, 0.3f);
            messageText.rectTransform.anchorMax = new Vector2(1, 0.66f);
            messageText.rectTransform.offsetMin = new Vector2(24, 0); messageText.rectTransform.offsetMax = new Vector2(-24, 0);

            confirmButton = UIBuilder.CreateButton(wRt, "Confirm", "ui.confirm.yes", out _, new Vector2(240, 56),
                new Color(0.55f, 0.25f, 0.25f));
            confirmButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.05f, 0.05f);
            confirmButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.47f, 0.24f);
            confirmButton.GetComponent<RectTransform>().offsetMin = Vector2.zero; confirmButton.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            confirmButton.onClick.AddListener(() => { Hide(); onConfirm?.Invoke(); onConfirm = null; onCancel = null; });

            cancelButton = UIBuilder.CreateButton(wRt, "Cancel", "ui.confirm.no", out _, new Vector2(240, 56),
                new Color(0.25f, 0.3f, 0.4f));
            cancelButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.53f, 0.05f);
            cancelButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.95f, 0.24f);
            cancelButton.GetComponent<RectTransform>().offsetMin = Vector2.zero; cancelButton.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            cancelButton.onClick.AddListener(() => { Hide(); onCancel?.Invoke(); onConfirm = null; onCancel = null; });
        }

        /// <summary>显示二次确认（titleKey/messageKey 为本地化键）。</summary>
        public void Show(string titleKey, string messageKey, Action onConfirm, Action onCancel = null)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;
            UIBuilder.SetLocalized(titleText, titleKey);
            UIBuilder.SetLocalized(messageText, messageKey);
            root.gameObject.SetActive(true);
            // 置于 Popup 层最上
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
            onConfirm = null; onCancel = null;
        }
    }
}
