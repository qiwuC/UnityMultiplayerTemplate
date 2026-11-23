using UnityEditor;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Audio;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>NetworkConfig 自定义 Inspector（T11-05）：分组 + 校验提示。</summary>
    [CustomEditor(typeof(NetworkConfig))]
    public class NetworkConfigInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            var cfg = (NetworkConfig)target;
            if (cfg.KcpPort < 1 || cfg.KcpPort > 65535)
                EditorGUILayout.HelpBox($"KCP 端口 {cfg.KcpPort} 越界（1-65535）。", MessageType.Error);
            if (cfg.MaxConnections < 1)
                EditorGUILayout.HelpBox("MaxConnections 至少为 1。", MessageType.Error);
            if (cfg.SteamAppId == 480)
                EditorGUILayout.HelpBox("SteamAppId=480（Valve 测试占位）。发布前替换为真实 AppId。", MessageType.Warning);
            if (cfg.ReconnectDelay < 0.1f)
                EditorGUILayout.HelpBox("ReconnectDelay 建议 ≥ 0.1s。", MessageType.Warning);
            DrawDefaultInspector();
        }
    }

    /// <summary>AudioMixerController 自定义 Inspector（T11-05）：实时音量读数 + 快速测试。</summary>
    [CustomEditor(typeof(AudioMixerController))]
    public class AudioMixerControllerInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            var c = (AudioMixerController)target;
            DrawDefaultInspector();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("当前音量", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Master", c.GetMasterVolume().ToString("P0"));
            EditorGUILayout.LabelField("Music", c.GetMusicVolume().ToString("P0"));
            EditorGUILayout.LabelField("SFX", c.GetSFXVolume().ToString("P0"));
            EditorGUILayout.LabelField("Voice", c.GetVoiceVolume().ToString("P0"));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("快速测试", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全部 0%")) SetAll(c, 0f);
                if (GUILayout.Button("全部 50%")) SetAll(c, 0.5f);
                if (GUILayout.Button("全部 100%")) SetAll(c, 1f);
            }
        }

        static void SetAll(AudioMixerController c, float v)
        {
            c.SetMasterVolume(v);
            c.SetMusicVolume(v);
            c.SetSFXVolume(v);
            c.SetVoiceVolume(v);
        }
    }

    /// <summary>TeamConfig 自定义 Inspector（T11-05）：反射绘制所有 Color 属性为色板预览（不依赖具体字段名）。</summary>
    [CustomEditor(typeof(TeamConfig))]
    public class TeamConfigInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var it = serializedObject.GetIterator();
            bool enter = true;
            var colorProps = new System.Collections.Generic.List<string>();
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.propertyType == SerializedPropertyType.Color)
                    colorProps.Add(it.name);
            }

            DrawDefaultInspector();

            if (colorProps.Count == 0) return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("队伍颜色预览", EditorStyles.boldLabel);
            foreach (var name in colorProps)
            {
                var p = serializedObject.FindProperty(name);
                if (p == null) continue;
                var r = EditorGUILayout.GetControlRect(GUILayout.Height(18));
                var label = new Rect(r.x, r.y, 120, r.height);
                var swatch = new Rect(r.x + 124, r.y, r.width - 124, r.height);
                EditorGUI.LabelField(label, name);
                EditorGUI.DrawRect(swatch, p.colorValue);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
