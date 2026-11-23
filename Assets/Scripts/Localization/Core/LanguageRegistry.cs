using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Localization
{
    /// <summary>语言元数据。</summary>
    [Serializable]
    public class LanguageInfo
    {
        public string Code;           // BCP-47 简写，如 en、zh_cn
        public string DisplayNameKey; // 本地化键，如 language.zh_cn.display
        public string LocalName;      // 本地名称（如 "简体中文"、"English"）
    }

    /// <summary>语言元数据注册表（静态）。</summary>
    public static class LanguageRegistry
    {
        static readonly List<LanguageInfo> languages = new List<LanguageInfo>
        {
            new LanguageInfo { Code = "en",    DisplayNameKey = "language.en.display",    LocalName = "English" },
            new LanguageInfo { Code = "zh_cn", DisplayNameKey = "language.zh_cn.display", LocalName = "简体中文" },
            new LanguageInfo { Code = "zh_tw", DisplayNameKey = "language.zh_tw.display", LocalName = "繁體中文" },
            new LanguageInfo { Code = "ja",    DisplayNameKey = "language.ja.display",    LocalName = "日本語" },
            new LanguageInfo { Code = "ko",    DisplayNameKey = "language.ko.display",    LocalName = "한국어" },
            new LanguageInfo { Code = "ru",    DisplayNameKey = "language.ru.display",    LocalName = "Русский" },
            new LanguageInfo { Code = "es",    DisplayNameKey = "language.es.display",    LocalName = "Español" },
            new LanguageInfo { Code = "pt_br", DisplayNameKey = "language.pt_br.display", LocalName = "Português (BR)" },
            new LanguageInfo { Code = "de",    DisplayNameKey = "language.de.display",    LocalName = "Deutsch" },
            new LanguageInfo { Code = "fr",    DisplayNameKey = "language.fr.display",    LocalName = "Français" },
            new LanguageInfo { Code = "pl",    DisplayNameKey = "language.pl.display",    LocalName = "Polski" },
            new LanguageInfo { Code = "tr",    DisplayNameKey = "language.tr.display",    LocalName = "Türkçe" },
            new LanguageInfo { Code = "it",    DisplayNameKey = "language.it.display",    LocalName = "Italiano" },
        };

        public static string DefaultCode => "en";

        public static IReadOnlyList<LanguageInfo> GetAll() => languages;

        public static LanguageInfo GetDefault() =>
            languages.Find(l => l.Code == DefaultCode);

        public static LanguageInfo TryGetInfo(string code) =>
            string.IsNullOrEmpty(code) ? null : languages.Find(l => l.Code == code);
    }
}
