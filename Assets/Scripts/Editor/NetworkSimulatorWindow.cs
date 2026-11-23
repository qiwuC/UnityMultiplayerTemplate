using UnityEditor;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Tools;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>网络模拟器窗口（T11-01）：预设一键套用 + 参数微调 + 实时状态。</summary>
    public class NetworkSimulatorWindow : EditorWindow
    {
        NetworkSimPreset preset = NetworkSimPreset.Lan;

        public static void Open() => GetWindow<NetworkSimulatorWindow>("Network Simulator");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "注入延迟/抖动/丢包到应用层发送（仅 Editor/DevBuild 生效）。" +
                "驱动由 NetworkDiagnosticsCenter.Update 自动完成。", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                preset = (NetworkSimPreset)EditorGUILayout.EnumPopup("预设", preset);
                if (GUILayout.Button("Apply", GUILayout.Width(70)))
                {
                    NetworkSimulatorPresets.Apply(preset);
                    Debug.Log($"[NetSim] 预设已应用：{preset}");
                }
            }

            NetworkSimulator.Enabled = EditorGUILayout.Toggle("启用", NetworkSimulator.Enabled);
            NetworkSimulator.LatencyMs = EditorGUILayout.Slider("延迟 (ms)", NetworkSimulator.LatencyMs, 0f, 1000f);
            NetworkSimulator.JitterMs = EditorGUILayout.Slider("抖动 ±(ms)", NetworkSimulator.JitterMs, 0f, 300f);
            NetworkSimulator.DropRate = EditorGUILayout.Slider("丢包率", NetworkSimulator.DropRate, 0f, 0.5f);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Disable")) NetworkSimulatorPresets.Disable();
                if (GUILayout.Button("Reset")) NetworkSimulator.Reset();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(NetworkSimulator.Describe());

            Repaint();
        }
    }
}
