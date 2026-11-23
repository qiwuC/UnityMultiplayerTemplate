using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;
using Cqw.MultilayerFramework.Localization;

namespace Cqw.MultilayerFramework.Tools
{
    /// <summary>批量工具窗口（T11-05）：TMP 字体批量替换 + LocalizationKeyHolder 批量添加。</summary>
    public class BatchToolsWindow : EditorWindow
    {
        int fontIndex;
        string keyPrefix = "ui.custom";
        Vector2 scroll;

        public static void Open() => GetWindow<BatchToolsWindow>("Batch Tools");

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // ---- 批量 TMP 字体 ----
            EditorGUILayout.LabelField("批量替换 TMP 字体", EditorStyles.boldLabel);
            var fonts = AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .Where(f => f != null).ToList();
            if (fonts.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到 TMP_FontAsset（先导入 TMP Essential Resources）。", MessageType.Info);
            }
            else
            {
                fontIndex = EditorGUILayout.Popup("目标字体", Mathf.Clamp(fontIndex, 0, fonts.Count - 1), fonts.Select(f => f.name).ToArray());
                EditorGUILayout.HelpBox("范围：当前场景全部 TextMeshProUGUI/TextMeshPro（含未激活）。", MessageType.Info);
                if (GUILayout.Button("替换场景全部 TMP 文本字体"))
                {
                    var font = fonts[Mathf.Clamp(fontIndex, 0, fonts.Count - 1)];
                    int n = 0;
                    foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        Undo.RecordObject(t, "Batch TMP Font");
                        t.font = font;
                        EditorUtility.SetDirty(t);
                        n++;
                    }
                    Debug.Log($"[BatchTools] 已替换 {n} 个 TMP 文本字体 → {font.name}。");
                }
            }

            EditorGUILayout.Space(10);

            // ---- 批量 LocalizationKeyHolder ----
            EditorGUILayout.LabelField("批量添加 LocalizationKeyHolder", EditorStyles.boldLabel);
            keyPrefix = EditorGUILayout.TextField("键前缀", keyPrefix);
            EditorGUILayout.HelpBox("为选中的每个 TMP 文本添加 LocalizationKeyHolder；键 = 前缀.对象名（camelCase）。", MessageType.Info);
            using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button($"为选中对象添加（当前选中 {Selection.gameObjects.Length}）"))
                {
                    int n = 0;
                    foreach (var go in Selection.gameObjects)
                    {
                        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (t.GetComponent<LocalizationKeyHolder>() != null) continue;
                            var holder = t.gameObject.AddComponent<LocalizationKeyHolder>();
                            var name = t.gameObject.name;
                            var sb = new System.Text.StringBuilder(name.Length);
                            sb.Append(char.ToLowerInvariant(name[0]));
                            for (int i = 1; i < name.Length; i++)
                            {
                                var c = name[i];
                                if (char.IsUpper(c) && char.IsLower(name[i - 1])) sb.Append(char.ToLowerInvariant(c));
                                else if (c == ' ' || c == '_') sb.Append('.');
                                else sb.Append(c);
                            }
                            holder.localizationKey = $"{keyPrefix}.{sb}";
                            EditorUtility.SetDirty(t);
                            n++;
                        }
                    }
                    Debug.Log($"[BatchTools] 已为 {n} 个 TMP 文本添加 LocalizationKeyHolder。");
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
