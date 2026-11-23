using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Cqw.MultilayerFramework.Localization.EditorTools
{
    /// <summary>
    /// 本地化键管理工具：扫描/差异对比/CSV 导出/导入。
    /// 菜单入口：Tools/Multilayer/Localization/...
    /// </summary>
    public static class LocalizationKeyTool
    {
        const string LocalizationDir = "Assets/Resources/Localization";
        const string DefaultCode = "en";

        [MenuItem("Tools/Multilayer/Localization/Scan Keys In Code", priority = 100)]
        public static void ScanKeysInCode()
        {
            var keys = new System.Collections.Generic.HashSet<string>();
            // 扫描所有 .cs 文件中的 LocalizationService.Get("...") 模式
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs")) continue;
                var content = System.IO.File.ReadAllText(path);
                // 匹配 LocalizationService.Get("key") 或 GetFormatted("key"
                var matches = System.Text.RegularExpressions.Regex.Matches(content,
                    @"LocalizationService\.(?:Get|GetFormatted)\(\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match m in matches)
                    keys.Add(m.Groups[1].Value);
                // 匹配 SetLocalizedText(target, "key") 或 SetLocalized(target, "key")
                matches = System.Text.RegularExpressions.Regex.Matches(content,
                    @"(?:SetLocalizedText|SetLocalized)\([^,]+,\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match m in matches)
                    keys.Add(m.Groups[1].Value);
            }

            // 对比 en.json
            var enPath = $"{LocalizationDir}/language_{DefaultCode}.json";
            var enJson = AssetDatabase.LoadAssetAtPath<TextAsset>(enPath);
            var existing = new System.Collections.Generic.HashSet<string>();
            if (enJson != null)
            {
                var j = Newtonsoft.Json.Linq.JObject.Parse(enJson.text);
                foreach (var prop in j.Properties()) existing.Add(prop.Name);
            }

            var missingInEn = new System.Collections.Generic.List<string>();
            var extraInEn = new System.Collections.Generic.List<string>();
            foreach (var k in keys) if (!existing.Contains(k)) missingInEn.Add(k);
            foreach (var k in existing) if (!keys.Contains(k)) extraInEn.Add(k);

            Debug.Log($"[LocTool] 代码中扫描到 {keys.Count} 键，en.json 有 {existing.Count} 键。\n" +
                $"缺失于 en.json: {(missingInEn.Count > 0 ? string.Join(", ", missingInEn) : "(none)")}\n" +
                $"en.json 多余: {(extraInEn.Count > 0 ? string.Join(", ", extraInEn.Take(10)) : "(none)")}");
        }

        [MenuItem("Tools/Multilayer/Localization/Check Missing Keys (All Languages)", priority = 101)]
        public static void CheckMissingKeys()
        {
            var enPath = $"{LocalizationDir}/language_{DefaultCode}.json";
            var enAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(enPath);
            if (enAsset == null) { Debug.LogError("[LocTool] en.json 不存在。"); return; }
            var enKeys = Newtonsoft.Json.Linq.JObject.Parse(enAsset.text).Properties().Select(p => p.Name).ToHashSet();

            var issues = new System.Collections.Generic.List<string>();
            foreach (var info in LanguageRegistry.GetAll())
            {
                if (info.Code == DefaultCode) continue;
                var path = $"{LocalizationDir}/language_{info.Code}.json";
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) { issues.Add($"{info.Code}: FILE MISSING"); continue; }
                var j = Newtonsoft.Json.Linq.JObject.Parse(asset.text);
                var langKeys = j.Properties().Select(p => p.Name).ToHashSet();
                var missing = enKeys.Where(k => !langKeys.Contains(k)).ToList();
                var extra = langKeys.Where(k => !enKeys.Contains(k)).ToList();
                if (missing.Count > 0 || extra.Count > 0)
                    issues.Add($"{info.Code}: missing={string.Join(",", missing.Take(5))} extra={string.Join(",", extra.Take(5))}");
            }
            Debug.Log(issues.Count == 0
                ? "[LocTool] 所有语言键集一致 ✓"
                : $"[LocTool] 发现 {issues.Count} 个语言文件键集差异：\n{string.Join("\n", issues)}");
        }

        [MenuItem("Tools/Multilayer/Localization/Export CSV", priority = 110)]
        public static void ExportCsv()
        {
            var enPath = $"{LocalizationDir}/language_{DefaultCode}.json";
            var enAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(enPath);
            if (enAsset == null) { Debug.LogError("[LocTool] en.json 不存在。"); return; }
            var enData = Newtonsoft.Json.Linq.JObject.Parse(enAsset.text);
            var allKeys = enData.Properties().Select(p => p.Name).ToList();

            // 收集所有语言数据
            var langData = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();
            foreach (var info in LanguageRegistry.GetAll())
            {
                var path = $"{LocalizationDir}/language_{info.Code}.json";
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) continue;
                langData[info.Code] = Newtonsoft.Json.Linq.JObject.Parse(asset.text).Properties()
                    .ToDictionary(p => p.Name, p => p.Value?.ToString() ?? "");
            }

            // 生成 CSV
            var sb = new System.Text.StringBuilder();
            sb.Append("key");
            foreach (var info in LanguageRegistry.GetAll()) sb.Append($",{info.Code}");
            sb.AppendLine();
            foreach (var key in allKeys)
            {
                sb.Append(EscapeCsv(key));
                foreach (var info in LanguageRegistry.GetAll())
                {
                    var val = langData.TryGetValue(info.Code, out var d) && d.TryGetValue(key, out var v) ? v : "";
                    sb.Append($",{EscapeCsv(val)}");
                }
                sb.AppendLine();
            }

            var csvPath = "Assets/Resources/Localization/localization_export.csv";
            System.IO.File.WriteAllText(csvPath, sb.ToString(), new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"[LocTool] CSV 已导出：{csvPath}（{allKeys.Count} 键 × {LanguageRegistry.GetAll().Count} 语言）");
        }

        [MenuItem("Tools/Multilayer/Localization/Import CSV", priority = 111)]
        public static void ImportCsv()
        {
            var csvPath = "Assets/Resources/Localization/localization_export.csv";
            if (!System.IO.File.Exists(csvPath)) { Debug.LogError($"[LocTool] CSV 不存在：{csvPath}"); return; }

            var lines = System.IO.File.ReadAllLines(csvPath);
            if (lines.Length < 2) { Debug.LogError("[LocTool] CSV 内容为空。"); return; }

            // 解析 header
            var header = lines[0].Split(',');
            var codes = header.Skip(1).ToArray();

            // 读取各语言现有数据
            var langData = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();
            foreach (var code in codes)
            {
                var path = $"{LocalizationDir}/language_{code}.json";
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                langData[code] = asset != null
                    ? Newtonsoft.Json.Linq.JObject.Parse(asset.text).Properties().ToDictionary(p => p.Name, p => p.Value?.ToString() ?? "")
                    : new System.Collections.Generic.Dictionary<string, string>();
            }

            // 解析行并合并
            int updated = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                if (cols.Count < 2) continue;
                var key = cols[0];
                for (int j = 1; j < cols.Count && j - 1 < codes.Length; j++)
                {
                    var code = codes[j - 1];
                    if (string.IsNullOrEmpty(cols[j])) continue;
                    langData[code][key] = cols[j];
                    updated++;
                }
            }

            // 原子写入
            foreach (var kv in langData)
            {
                var path = $"{LocalizationDir}/language_{kv.Key}.json";
                var tmpPath = path + ".tmp";
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(kv.Value, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(tmpPath, json, new System.Text.UTF8Encoding(false));
                if (System.IO.File.Exists(path)) System.IO.File.Replace(tmpPath, path, null);
                else System.IO.File.Move(tmpPath, path);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[LocTool] CSV 导入完成：{updated} 条值已更新，{langData.Count} 个语言文件已写入。");
        }

        static string EscapeCsv(string s) =>
            s.Contains(",") || s.Contains("\"") || s.Contains("\n") ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        static System.Collections.Generic.List<string> ParseCsvLine(string line)
        {
            var result = new System.Collections.Generic.List<string>();
            bool inQuotes = false;
            var sb = new System.Text.StringBuilder();
            foreach (var c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); continue; }
                sb.Append(c);
            }
            result.Add(sb.ToString());
            return result;
        }
    }
}
