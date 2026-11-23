using System.IO;using System.Text;using UnityEditor;using UnityEngine;using Newtonsoft.Json;
namespace Cqw.MultilayerFramework.Tools
{
    public static class PanelWizard
    {
        public static string PanelName = "ShopPanel";
        public static string KeyPrefix = "shop";

        [MenuItem("Tools/Multilayer/Wizards/New UI Panel")]
        public static void Open()
        {
            var name = PanelName;
            var prefix = KeyPrefix.ToLower();
            var dir = Path.Combine(Application.dataPath, "Scripts/UI/Panels");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using TMPro;");
            sb.AppendLine("namespace Cqw.MultilayerFramework.UI");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {name}Panel : UIPanelBase");
            sb.AppendLine("    {");
            sb.AppendLine($"        public override string PanelId => \"{prefix}.panel\";");
            sb.AppendLine("        public override bool IsModal => true;");
            sb.AppendLine("        protected override void OnOpen() { }");
            sb.AppendLine("        protected override void OnClose() { }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            var fp = Path.Combine(dir, name + "Panel.cs");
            File.WriteAllText(fp, sb.ToString(), Encoding.UTF8);

            var locDir = Path.Combine(Application.dataPath, "Resources", "Localization");
            var codes = new[] { "en", "zh_cn", "zh_tw", "ja", "ko", "ru", "es", "de", "fr", "it", "pl", "tr", "pt_br" };
            foreach (var code in codes)
            {
                var lp = Path.Combine(locDir, "language_" + code + ".json");
                if (!File.Exists(lp)) continue;
                var obj = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(lp));
                if (obj[prefix + ".title"] == null) obj[prefix + ".title"] = name;
                if (obj[prefix + ".close"] == null) obj[prefix + ".close"] = "Back";
                File.WriteAllText(lp, obj.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
            }
            AssetDatabase.Refresh();
            Debug.Log($"[PanelWizard] {name}Panel.cs 已生成，请手动在 UIPanelBootstrap.CreatePanels 中添加注册。");
        }
    }
}
