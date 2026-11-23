using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mirror;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.UI;
using Cqw.MultilayerFramework.Audio;
using Cqw.MultilayerFramework.Interaction;
using Cqw.MultilayerFramework.Interaction.Samples;

/// <summary>SampleScene 构建器（编辑器专用）：一键生成示例场景基础设施与样例对象。</summary>
public static class SampleSceneBuilder
{
    [MenuItem("Tools/Multilayer/Build Sample Scene", priority = 160)]
    public static void BuildFromMenu() => Build();

    public static string Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 地面
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(6, 1, 6);

        // 4 个 SpawnPoint
        for (int i = 0; i < 4; i++)
        {
            var sp = new GameObject("SpawnPoint_" + (i + 1));
            sp.transform.position = new Vector3(i % 2 == 0 ? -8 : 8, 1, i < 2 ? 8 : -8);
            sp.AddComponent<SpawnPoint>();
        }

        // UIRoot
        var ui = new GameObject("UIRoot");
        ui.AddComponent<UIManager>();
        ui.AddComponent<ToastManager>();
        ui.AddComponent<AudioSystemBootstrap>();
        ui.AddComponent<UIPanelBootstrap>();

        // NetworkManagerRoot
        var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkManagerRoot.prefab");
        if (nmPrefab != null) PrefabUtility.InstantiatePrefab(nmPrefab);

        // 2 个 Sample_Button
        var btnData = AssetDatabase.LoadAssetAtPath<InteractableData>("Assets/ScriptableObjects/Interaction/SampleButtonData.asset");
        for (int i = 0; i < 2; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SampleButton_" + (i + 1);
            go.transform.position = new Vector3(i == 0 ? -3 : 3, 0.5f, 5);
            go.AddComponent<NetworkIdentity>();
            go.AddComponent<SampleButtonSync>();
            var b = go.AddComponent<Sample_Button>();
            b.data = btnData;
        }

        // 2 个 Sample_Chest
        var chestData = AssetDatabase.LoadAssetAtPath<InteractableData>("Assets/ScriptableObjects/Interaction/SampleChestData.asset");
        for (int i = 0; i < 2; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SampleChest_" + (i + 1);
            go.transform.position = new Vector3(i == 0 ? -3 : 3, 0.5f, -5);
            go.AddComponent<NetworkIdentity>();
            go.AddComponent<SampleChestSync>();
            var c = go.AddComponent<Sample_Chest>();
            c.data = chestData;
        }

        // SampleChestCounter
        var counter = new GameObject("SampleChestCounter");
        counter.AddComponent<NetworkIdentity>();
        counter.AddComponent<SampleChestCounter>();

        // 保存 + Build Settings
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SampleScene.unity");
        var bs = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!bs.Exists(s => s.path.Contains("SampleScene")))
            bs.Add(new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true));
        EditorBuildSettings.scenes = bs.ToArray();
        AssetDatabase.Refresh();

        var msg = $"SampleScene done roots={scene.rootCount} build={bs.Count}";
        Debug.Log("[SampleSceneBuilder] " + msg);
        return msg;
    }
}
