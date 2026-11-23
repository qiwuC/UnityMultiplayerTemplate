using UnityEditor;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Tools;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>Tools/Multilayer 菜单集（T11-01/02/04）：模拟器窗口、带宽监控、诊断导出、批量工具。</summary>
    public static class MultilayerMenus
    {
        [MenuItem("Tools/Multilayer/Network Simulator", priority = 100)]
        public static void OpenNetworkSimulator() => EditorWindow.GetWindow<NetworkSimulatorWindow>("Network Simulator");

        [MenuItem("Tools/Multilayer/Bandwidth Monitor", priority = 101)]
        public static void OpenBandwidthMonitor() => EditorWindow.GetWindow<BandwidthMonitorWindow>("Bandwidth Monitor");

        [MenuItem("Tools/Multilayer/Batch Tools", priority = 102)]
        public static void OpenBatchTools() => EditorWindow.GetWindow<BatchToolsWindow>("Batch Tools");

        [MenuItem("Tools/Multilayer/Diagnostics/Export Now", priority = 120)]
        public static void ExportDiagnostics() => NetworkDiagnosticsExporter.ExportCsv();

        [MenuItem("Tools/Multilayer/Diagnostics/Export Now (选中 NetworkManagerRoot)", priority = 121)]
        public static void ExportDiagnosticsHint()
        {
            var go = Selection.activeGameObject;
            if (go != null && go.GetComponent<NetworkDiagnosticsCenter>() != null)
                NetworkDiagnosticsExporter.ExportCsv();
            else
                NetworkDiagnosticsExporter.ExportCsv();
        }

        [MenuItem("Tools/Multilayer/Presets/Ideal", priority = 130)]
        public static void PresetIdeal() => NetworkSimulatorPresets.Apply(NetworkSimPreset.Ideal);

        [MenuItem("Tools/Multilayer/Presets/LAN", priority = 131)]
        public static void PresetLan() => NetworkSimulatorPresets.Apply(NetworkSimPreset.Lan);

        [MenuItem("Tools/Multilayer/Presets/Broadband", priority = 132)]
        public static void PresetBroadband() => NetworkSimulatorPresets.Apply(NetworkSimPreset.Broadband);

        [MenuItem("Tools/Multilayer/Presets/4G", priority = 133)]
        public static void Preset4G() => NetworkSimulatorPresets.Apply(NetworkSimPreset.Net4G);

        [MenuItem("Tools/Multilayer/Presets/Poor", priority = 134)]
        public static void PresetPoor() => NetworkSimulatorPresets.Apply(NetworkSimPreset.Poor);

        [MenuItem("Tools/Multilayer/Presets/Terrible", priority = 135)]
        public static void PresetTerrible() => NetworkSimulatorPresets.Apply(NetworkSimPreset.Terrible);

        [MenuItem("Tools/Multilayer/Presets/Disable", priority = 136)]
        public static void PresetDisable() => NetworkSimulatorPresets.Disable();

        [MenuItem("Tools/Multilayer/Print Command Args", priority = 140)]
        public static void PrintArgs() => Debug.Log($"[CmdArgs] {CommandLineArgs.Describe()}");
    }
}
