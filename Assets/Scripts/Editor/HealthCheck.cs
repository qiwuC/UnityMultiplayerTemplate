using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Player;
using Cqw.MultilayerFramework.Interaction;
using Cqw.MultilayerFramework.Tools;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>
    /// 框架健康检查（T12/B-05）：一键运行 A-05 冒烟自检（编辑器可验证部分），
    /// 结果输出到 Console + persistentDataPath/health_check.txt。
    /// 菜单：Tools/Multilayer/Health Check。
    /// </summary>
    public static class HealthCheck
    {
        [MenuItem("Tools/Multilayer/Health Check", priority = 150)]
        public static void Run()
        {
            var r = new List<string>();
            r.Add("=== 框架健康检查 " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            // 编译：能走到这里说明编译通过
            r.Add("compile=OK(0 errors)");

            // Build Settings
            var scenes = EditorBuildSettings.scenes;
            r.Add("build=" + scenes.Length + "scenes(" +
                string.Join(",", System.Linq.Enumerable.Select(scenes, s => System.IO.Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : ":off"))) + ")");

            // 网络（编辑态：不启 Host，只查配置）
            var nm = Object.FindAnyObjectByType<CqwNetworkManager>(FindObjectsInactive.Include);
            r.Add("nm=" + (nm != null ? "found" : "MISSING"));
            var cfg = nm != null ? nm.Config : null;
            r.Add("config=" + (cfg != null ? "found" : "MISSING"));

            // 交互物
            var lamps = Object.FindObjectsByType<Interaction.TestLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var pickups = Object.FindObjectsByType<Interaction.TestPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            r.Add("interactables=lamps:" + lamps.Length + " pickups:" + pickups.Length);

            // UIRoot
            var uiRoot = GameObject.Find("UIRoot");
            r.Add("uiroot=" + (uiRoot != null ? "found" : "MISSING"));
            var uiMgr = uiRoot != null ? uiRoot.GetComponent<UI.UIManager>() : null;
            r.Add("uimgr=" + (uiMgr != null ? "found" : "MISSING"));

            // 本地化键
            var locDir = Path.Combine(Application.dataPath, "Resources", "Localization");
            if (Directory.Exists(locDir))
            {
                var files = Directory.GetFiles(locDir, "language_*.json");
                r.Add("loc=" + files.Length + " languages");
            }

            // 日志
            var logDir = Path.Combine(Application.persistentDataPath, "Logs");
            var todayLog = Path.Combine(logDir, "game_" + System.DateTime.Now.ToString("yyyyMMdd") + ".log");
            r.Add("log=" + (File.Exists(todayLog) ? "today" : "none"));

            // 设置
            var settingsPath = Path.Combine(Application.persistentDataPath, "settings.json");
            r.Add("settings=" + (File.Exists(settingsPath) ? "found" : "first-run"));

            r.Add("=== 完成（详细运行时验证见 REGRESSION.md） ===");

            var output = string.Join("\n", r.ToArray());
            Debug.Log(output);

            var outPath = Path.Combine(Application.persistentDataPath, "health_check.txt");
            File.WriteAllText(outPath, output);
            Debug.Log("[HealthCheck] 结果已写入：" + outPath);
        }
    }
}
