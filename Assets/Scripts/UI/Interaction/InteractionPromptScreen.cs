using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>交互提示数据。</summary>
    public class InteractionPromptData
    {
        public string Key;         // 本地化键
        public string IconSprite;  // 按键图标（键名占位）
        public bool ShowProgress;  // 是否显示进度环
    }

    /// <summary>
    /// 交互提示（屏幕固定模式）：按键图标 + 提示文本 + 进度环（可选）。
    /// 阶段九接入检测逻辑后调用 Show/Hide/SetProgress。
    /// </summary>
    [AddComponentMenu("Cqw/UI/InteractionPromptScreen")]
    public class InteractionPromptScreen : MonoBehaviour
    {
        TMP_Text promptText;
        Image iconImage, progressRing;
        RectTransform progressRect;
        TMP_Text iconText;

        IInputDeviceProvider deviceProvider;

        void Awake()
        {
            Build();
            Hide();
        }

        public void SetDeviceProvider(IInputDeviceProvider provider) => deviceProvider = provider;

        void Build()
        {
            var root = GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.35f, 0.6f);
            root.anchorMax = new Vector2(0.65f, 0.7f);
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            var bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);

            iconImage = CreateIcon(root);
            promptText = CreateLabel(root);
            progressRing = CreateProgressRing(root);
            progressRect = progressRing != null ? progressRing.rectTransform : null;
        }

        Image CreateIcon(RectTransform parent)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.02f, 0.15f); rt.anchorMax = new Vector2(0.15f, 0.85f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.8f);
            return img;
        }

        TMP_Text CreateLabel(RectTransform parent)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.18f, 0.15f); rt.anchorMax = new Vector2(0.85f, 0.85f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 24; tmp.color = Color.white; tmp.alignment = TMPro.TextAlignmentOptions.Left;
            return tmp;
        }

        Image CreateProgressRing(RectTransform parent)
        {
            var go = new GameObject("Progress", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.87f, 0.15f); rt.anchorMax = new Vector2(0.98f, 0.85f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.3f, 0.8f, 0.4f, 0.8f);
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillAmount = 0;
            return img;
        }

        /// <summary>显示交互提示。</summary>
        public void Show(InteractionPromptData data)
        {
            gameObject.SetActive(true);
            if (data == null) return;

            // 文本（本地化：缺键回退键名标题化）
            if (promptText != null)
                promptText.text = UIPanelBase.LocalizedOrTitle(data.Key);

            // 按键图标（按设备类型显示对应按键字符）
            if (iconImage != null)
            {
                var device = deviceProvider?.GetCurrentDeviceType() ?? InputDeviceType.KeyboardMouse;
                EnsureIconText();
                if (iconText != null)
                    iconText.text = device == InputDeviceType.Gamepad ? "X" : "E";
            }

            // 进度环
            if (progressRing != null)
            {
                progressRing.gameObject.SetActive(data.ShowProgress);
                progressRing.fillAmount = 0;
            }
        }

        void EnsureIconText()
        {
            if (iconText != null) return;
            var go = new GameObject("IconText", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(iconImage != null ? iconImage.transform : (Transform)transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            iconText = go.GetComponent<TextMeshProUGUI>();
            iconText.fontSize = 20;
            iconText.color = Color.white;
            iconText.alignment = TMPro.TextAlignmentOptions.Center;
            iconText.raycastTarget = false;
        }

        /// <summary>隐藏提示。</summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>设置进度（0-1；仅 ShowProgress=true 时可见）。</summary>
        public void SetProgress(float t)
        {
            if (progressRing != null) progressRing.fillAmount = Mathf.Clamp01(t);
        }
    }
}
