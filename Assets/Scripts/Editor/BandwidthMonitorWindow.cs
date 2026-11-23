using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>带宽监控窗口（T11-02）：实时发送/接收 KB/s + 60 秒滚动条图。数据源 NetworkBandwidthMonitor（NetworkManagerRoot 上）。</summary>
    public class BandwidthMonitorWindow : EditorWindow
    {
        const int MaxSamples = 60;
        readonly Queue<int> send = new Queue<int>();
        readonly Queue<int> recv = new Queue<int>();
        double nextSample;
        NetworkBandwidthMonitor monitor;

        public static void Open() => GetWindow<BandwidthMonitorWindow>("Bandwidth Monitor");

        void OnEnable() => EditorApplication.update += Sample;
        void OnDisable() => EditorApplication.update -= Sample;

        void Sample()
        {
            if (EditorApplication.timeSinceStartup < nextSample) return;
            nextSample = EditorApplication.timeSinceStartup + 1.0;
            if (monitor == null) monitor = Object.FindAnyObjectByType<NetworkBandwidthMonitor>();
            if (monitor == null) return;
            send.Enqueue((int)monitor.SendKbps);
            recv.Enqueue((int)monitor.ReceiveKbps);
            while (send.Count > MaxSamples) send.Dequeue();
            while (recv.Count > MaxSamples) recv.Dequeue();
            Repaint();
        }

        void OnGUI()
        {
            if (monitor == null) monitor = Object.FindAnyObjectByType<NetworkBandwidthMonitor>();
            if (monitor == null)
            {
                EditorGUILayout.HelpBox("未找到 NetworkBandwidthMonitor（NetworkManagerRoot 上，需运行中）。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("发送", $"{monitor.SendKbps} Kbps");
            EditorGUILayout.LabelField("接收", $"{monitor.ReceiveKbps} Kbps");
            EditorGUILayout.LabelField("状态", monitor.Describe());
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("发送（60s）");
            DrawBars(send, Color.green);
            EditorGUILayout.LabelField("接收（60s）");
            DrawBars(recv, Color.cyan);
        }

        static void DrawBars(Queue<int> samples, Color color)
        {
            var rect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.12f));
            var arr = samples.ToArray();
            if (arr.Length == 0) return;
            float max = 1;
            foreach (var v in arr) max = Mathf.Max(max, v);
            float w = rect.width / MaxSamples;
            for (int i = 0; i < arr.Length; i++)
            {
                float h = Mathf.Clamp01(arr[i] / max) * (rect.height - 2);
                EditorGUI.DrawRect(new Rect(rect.x + i * w, rect.yMax - h, Mathf.Max(1, w - 1), h), color);
            }
        }
    }
}
