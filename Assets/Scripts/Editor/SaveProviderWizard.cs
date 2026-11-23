using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>New Save Provider 向导：生成 ISaveDataProvider 实现模板。</summary>
    public class NewSaveProviderWizard : EditorWindow
    {
        string _providerId = "";
        int _version = 1;

        [MenuItem("Tools/Multilayer/Wizards/New Save Provider", priority = 202)]
        public static void Open() => GetWindow<NewSaveProviderWizard>("New Save Provider");

        void OnGUI()
        {
            EditorGUILayout.LabelField("New Save Provider Wizard", EditorStyles.boldLabel);
            _providerId = EditorGUILayout.TextField("Provider ID (小写)", _providerId);
            _version = EditorGUILayout.IntField("Provider Version", _version);
            EditorGUILayout.Space(8);

            if (GUILayout.Button("Generate"))
            {
                if (string.IsNullOrEmpty(_providerId))
                {
                    EditorUtility.DisplayDialog("缺少参数", "请填写 ProviderId。", "OK");
                    return;
                }
                Generate(_providerId.Trim(), _version);
            }
        }

        internal static string Generate(string providerId, int version)
        {
            var dir = Path.Combine(Application.dataPath, "Scripts/Save/Providers");
            Directory.CreateDirectory(dir);
            var className = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(providerId).Replace("_", "");
            var filePath = Path.Combine(dir, className + "Provider.cs");
            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine($"namespace Cqw.MultilayerFramework.Save");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>{providerId} 存档 Provider（模板生成）。</summary>");
            sb.AppendLine($"    public class {className}Provider : ISaveDataProvider");
            sb.AppendLine("    {");
            sb.AppendLine($"        public string ProviderId => \"{providerId}\";");
            sb.AppendLine($"        public int Version => {version};");
            sb.AppendLine("        public Dictionary<string, string> CaptureState()");
            sb.AppendLine("        {");
            sb.AppendLine("            var r = new Dictionary<string, string>();");
            sb.AppendLine("            // TODO: 序列化状态到 r");
            sb.AppendLine("            return r;");
            sb.AppendLine("        }");
            sb.AppendLine("        public void RestoreState(Dictionary<string, string> data, int fromVersion)");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: 从 data 恢复状态；fromVersion < Version 时自行迁移");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"[SaveProviderWizard] {className}Provider.cs 已生成。请在 SaveSystemBootstrap 中注册。");
            return filePath;
        }
    }
}
