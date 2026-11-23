using UnityEngine;
using TMPro;

namespace Cqw.MultilayerFramework.Localization
{
    /// <summary>
    /// 为 TMP 文本对象预留本地化键。
    /// 本地化系统落地后：OnEnable 注册语言切换回调，语言变化时自动刷新文本。
    /// </summary>
    [AddComponentMenu("Cqw/Localization/Localization Key Holder")]
    public class LocalizationKeyHolder : MonoBehaviour
    {
        [Tooltip("本地化键，例如 ui.mainMenu.host")]
        public string localizationKey;

        [Tooltip("格式化参数（可选，{0} {1}…）")]
        public string[] formatArgs;

        TMPro.TMP_Text cachedText;
        bool subscribed;

        void OnEnable()
        {
            LocalizationService.OnLanguageChanged += Refresh;
            subscribed = true;
            Refresh();
        }

        void OnDisable()
        {
            if (subscribed)
            {
                LocalizationService.OnLanguageChanged -= Refresh;
                subscribed = false;
            }
        }

        /// <summary>刷新关联的 TMP 文本（本地化查询 + 格式化）。</summary>
        public void Refresh()
        {
            if (cachedText == null) cachedText = GetComponent<TMPro.TMP_Text>();
            if (cachedText == null || string.IsNullOrEmpty(localizationKey)) return;

            cachedText.text = formatArgs != null && formatArgs.Length > 0
                ? LocalizationService.GetFormatted(localizationKey, formatArgs)
                : LocalizationService.Get(localizationKey);
        }

        /// <summary>设置键并立即刷新。</summary>
        public void SetKey(string key)
        {
            localizationKey = key;
            Refresh();
        }
    }
}
