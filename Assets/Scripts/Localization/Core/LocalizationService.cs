using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Localization
{
    /// <summary>
    /// 本地化服务（单例）：加载 JSON 键值表、缺键回退（当前语言→en→key）、语言切换、持久化。
    /// </summary>
    public static class LocalizationService
    {
        static Dictionary<string, string> currentTable = new Dictionary<string, string>();
        static Dictionary<string, string> defaultTable = new Dictionary<string, string>();
        static string currentCode = "";
        static string defaultCode = "en";
        static readonly HashSet<string> warnedKeys = new HashSet<string>();

        /// <summary>当前语言代码。</summary>
        public static string CurrentLanguage => currentCode;

        /// <summary>语言切换事件。</summary>
        public static event Action OnLanguageChanged;

        // ============ 加载 ============

        /// <summary>从 Resources/Localization/language_{code}.json 加载键值表。</summary>
        public static Dictionary<string, string> LoadLanguage(string code)
        {
            var textAsset = Resources.Load<TextAsset>($"Localization/language_{code}");
            if (textAsset == null)
            {
                Debug.LogWarning($"[Localization] 语言文件不存在：Resources/Localization/language_{code}.json");
                return new Dictionary<string, string>();
            }

            var json = textAsset.text;
            var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (dict == null) return new Dictionary<string, string>();
            return dict;
        }

        /// <summary>初始化：从 IGameSettingsService 读取语言 → 无效则系统语言 → en。</summary>
        public static void Initialize()
        {
            var settings = GameSettingsServiceLocator.Service;
            var saved = settings.GetString("game.language", "");
            if (string.IsNullOrEmpty(saved) || LanguageRegistry.TryGetInfo(saved) == null)
            {
                // 系统语言映射
                var sysLang = Application.systemLanguage;
                saved = sysLang switch
                {
                    SystemLanguage.ChineseSimplified => "zh_cn",
                    SystemLanguage.ChineseTraditional => "zh_tw",
                    SystemLanguage.Chinese => "zh_cn",
                    SystemLanguage.Japanese => "ja",
                    SystemLanguage.Korean => "ko",
                    SystemLanguage.Russian => "ru",
                    SystemLanguage.Spanish => "es",
                    SystemLanguage.German => "de",
                    SystemLanguage.French => "fr",
                    SystemLanguage.Italian => "it",
                    SystemLanguage.Polish => "pl",
                    SystemLanguage.Turkish => "tr",
                    _ => "en"
                };
            }
            SetLanguage(saved, persist: false);
        }

        /// <summary>切换语言：卸载旧表、加载新表、广播事件。persist=true 时写入 IGameSettingsService。</summary>
        public static void SetLanguage(string code, bool persist = true)
        {
            if (code == currentCode) return;
            var info = LanguageRegistry.TryGetInfo(code);
            if (info == null) { Debug.LogWarning($"[Localization] 未知语言代码：{code}"); return; }

            currentCode = code;
            currentTable = LoadLanguage(code);

            // 默认语言表（en）仅加载一次
            if (code == defaultCode)
                defaultTable = currentTable;
            else if (defaultTable.Count == 0)
                defaultTable = LoadLanguage(defaultCode);

            if (persist)
            {
                GameSettingsServiceLocator.Service.SetString("game.language", code);
                GameSettingsServiceLocator.Service.Save();
            }

            Debug.Log($"[Localization] 语言切换 → {code}（{currentTable.Count} 键）");
            OnLanguageChanged?.Invoke();
        }

        // ============ 查询 ============

        /// <summary>尝试获取（当前语言→en→false）。</summary>
        public static bool TryGet(string key, out string value)
        {
            if (currentTable.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return true;
            if (currentCode != defaultCode && defaultTable.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return true;
            value = null;
            return false;
        }

        /// <summary>获取（缺键返回 key 本身，同一 key 只警告一次）。</summary>
        public static string Get(string key)
        {
            if (TryGet(key, out var value)) return value;
            if (warnedKeys.Add(key))
                Debug.LogWarning($"[Localization] 缺键：{key}（当前={currentCode}）");
            return key;
        }

        /// <summary>获取 + 格式化（{0} {1}…）。</summary>
        public static string GetFormatted(string key, params object[] args)
        {
            var raw = Get(key);
            return args != null && args.Length > 0 ? string.Format(raw, args) : raw;
        }

        /// <summary>清空警告去重集合（编辑器调试用）。</summary>
        public static void ClearWarnings() => warnedKeys.Clear();
    }
}
