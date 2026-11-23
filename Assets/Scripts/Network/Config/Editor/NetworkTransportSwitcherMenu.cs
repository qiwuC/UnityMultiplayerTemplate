#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network.EditorTools
{
    /// <summary>编辑器工具：一键切换传输层并保存 NetworkManager 预制体。</summary>
    public static class NetworkTransportSwitcherMenu
    {
        [MenuItem("Cqw/Network/切换传输层/KCP (直连)", priority = 10)]
        public static void SwitchToKcp() => Switch(TransportMode.Kcp);

        [MenuItem("Cqw/Network/切换传输层/FizzySteamworks (Steam P2P)", priority = 11)]
        public static void SwitchToFizzySteamworks() => Switch(TransportMode.FizzySteamworks);

        [MenuItem("Cqw/Network/传输层状态报告", priority = 20)]
        public static void ReportState()
        {
            var (config, nm) = Load();
            if (config == null || nm == null)
            {
                Debug.LogWarning("[CqwNetwork] NetworkConfig.asset 或场景中的 NetworkManager 不存在。");
                return;
            }
            Debug.Log($"[CqwNetwork] 配置 Transport={config.Transport} | NetworkManager 当前 transport={NetworkTransportSwitcher.DescribeActive(nm)} | playerPrefab={(nm.playerPrefab ? nm.playerPrefab.name : "null")}");
        }

        static (NetworkConfig config, Mirror.NetworkManager nm) Load()
        {
            var config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(NetworkConfig.DefaultAssetPath);
            var nm = Object.FindAnyObjectByType<Mirror.NetworkManager>();
            return (config, nm);
        }

        static void Switch(TransportMode mode)
        {
            var (config, nm) = Load();
            if (config == null || nm == null)
            {
                Debug.LogWarning("[CqwNetwork] NetworkConfig.asset 或场景中的 NetworkManager 不存在，无法切换。");
                return;
            }

            config.Transport = mode;
            EditorUtility.SetDirty(config);
            var result = NetworkTransportSwitcher.Apply(config, nm);
            EditorUtility.SetDirty(nm);
            EditorUtility.SetDirty(nm.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CqwNetwork] 已切换传输层 → {result}");
        }
    }
}
#endif
