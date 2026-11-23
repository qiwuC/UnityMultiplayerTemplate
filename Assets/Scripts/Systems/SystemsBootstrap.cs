using System.Collections.Generic;
using UnityEngine;
using Cqw.MultilayerFramework.UI;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>
    /// Common systems bootstrap (T8-10): Initializes GameLogger → GameSettings → Scheduler → EventBus →
    /// ResourceManager → ObjectPool → DataRegistry → EffectManager in order; Shutdown cleans up in reverse.
    /// Auto-created via RuntimeInitializeOnLoadMethod(BeforeSceneLoad), runs before scene-level
    /// LocalizationBootstrap / AudioSystemBootstrap / UIPanelBootstrap (each subsystem initializes independently).
    /// InitOrder is for debugging; CommandLineArgs (Phase 11) applies after this bootstrap.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [AddComponentMenu("Cqw/Systems/SystemsBootstrap")]
    public class SystemsBootstrap : MonoBehaviour
    {
        public static SystemsBootstrap Instance { get; private set; }

        /// <summary>Initialization order record (debugging).</summary>
        public static readonly List<string> InitOrder = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null) return;
            InitOrder.Clear();
            var go = new GameObject("SystemsRoot");
            go.AddComponent<SystemsBootstrap>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 1. GameLogger (earliest, idempotent; AutoInit already called)
            GameLogger.Initialize(LoadLoggerConfig());

            // B-01: Inject validator config
            InputValidator.Configure(UnityEditor.AssetDatabase.LoadAssetAtPath<Cqw.MultilayerFramework.Network.ValidationConfig>("Assets/ScriptableObjects/ValidationConfig.asset"));
            InitOrder.Add("GameLogger");

            // 2. GameSettings (JSON backend, replaces PlayerPrefs placeholder; migration happens here)
            var settings = new JsonGameSettingsService();
            GameSettingsServiceLocator.Set(settings);
            InitOrder.Add("GameSettings");

            // 3. Scheduler (frame driver; Delay internally ensures runner is idempotent)
            var runnerGo = EnsureChild("Systems_SchedulerRunner");
            if (runnerGo.GetComponent<SchedulerRunner>() == null) runnerGo.AddComponent<SchedulerRunner>();
            InitOrder.Add("Scheduler");

            // 4. EventBus (static; clears leftover subscriptions from previous domain)
            EventBus.ClearAll();
            InitOrder.Add("EventBus");

            // 5. ResourceManager
            ResourceManager.EnsureExists();
            InitOrder.Add("ResourceManager");

            // 6. ObjectPool
            ObjectPoolService.EnsureExists();
            InitOrder.Add("ObjectPool");

            // 7. DataRegistry (static Hub, lazily created; only records order here)
            InitOrder.Add("DataRegistry");

            // 8. EffectManager
            EffectManager.EnsureExists();
            InitOrder.Add("EffectManager");

            // Apply settings (quality/resolution/audio/language; scene-level Bootstraps unaffected — they re-read the service)
            JsonGameSettingsService.ApplyAll();

            GameLogger.LogInfo($"[Systems] Bootstrap complete: {string.Join(" -> ", InitOrder)}");
        }

        GameObject EnsureChild(string name)
        {
            var child = transform.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }

        GameLoggerConfig LoadLoggerConfig()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameLoggerConfig>("Assets/ScriptableObjects/GameLoggerConfig.asset");
#else
            return null;
#endif
        }

        /// <summary>Reverse-order cleanup (testing/reset; normal exit handled by each service's quitting handler).</summary>
        public void Shutdown()
        {
            GameLogger.LogInfo("[Systems] Shutdown (reverse order):");
            for (int i = InitOrder.Count - 1; i >= 0; i--)
            {
                switch (InitOrder[i])
                {
                    case "EffectManager":
                        if (EffectManager.Instance != null)
                        {
                            EffectManager.Instance.ClearPools();
                            Destroy(EffectManager.Instance.gameObject);
                        }
                        break;
                    case "ObjectPool":
                        ObjectPoolService.Instance?.ClearAll();
                        break;
                    case "ResourceManager":
                        ResourceManager.Instance?.ReleasePreloads();
                        break;
                    case "Scheduler":
                        Scheduler.CancelAll();
                        CooldownManager.ClearAll();
                        break;
                    case "GameSettings":
                        (GameSettingsServiceLocator.Service as JsonGameSettingsService)?.FlushNow();
                        break;
                }
                GameLogger.LogInfo($"[Systems] - {InitOrder[i]}");
            }
        }
    }
}
