using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>
    /// 自动存档调度器：定时 + 退出保底 + Host 权威。
    /// 从 IGameSettingsService 读取 AutoSaveEnabled 与 AutoSaveIntervalMinutes。
    /// </summary>
    [AddComponentMenu("Cqw/Save/AutoSaveScheduler")]
    public class AutoSaveScheduler : MonoBehaviour
    {
        public static AutoSaveScheduler Instance { get; private set; }

        float timer;

        bool AutoSaveEnabled => GameSettingsServiceLocator.Service.GetBool("settings.autoSave", true);
        float AutoSaveIntervalMinutes => Mathf.Clamp(GameSettingsServiceLocator.Service.GetFloat("settings.autoSaveInterval", 5f), 1f, 15f);
        bool ShouldSave => NetworkServer.active; // Host 或单人

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (!AutoSaveEnabled || !ShouldSave) return;
            timer += Time.unscaledDeltaTime;
            if (timer >= AutoSaveIntervalMinutes * 60f)
            {
                timer = 0f;
                TriggerAutoSave("定时自动存档");
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && AutoSaveEnabled && ShouldSave) TriggerAutoSave("OnApplicationPause");
        }

        void OnApplicationQuit()
        {
            if (AutoSaveEnabled && ShouldSave) TriggerAutoSave("OnApplicationQuit");
        }

        /// <summary>手动触发自动存档（调试/快捷键/退出保底）。</summary>
        public void TriggerAutoSave(string reason)
        {
            // 正在加载场景/迁移时跳过
            var sceneMgr = FindAnyObjectByType<CqwNetworkSceneManager>();
            if (sceneMgr != null && sceneMgr.IsSwitching)
            {
                Debug.Log("[AutoSave] 正在切换场景，跳过本次自动存档。");
                return;
            }

            var service = SaveSlotServiceLocator.Service;
            if (service == null) return;

            // 写入最近使用槽位
            var lastUsed = service.GetLastUsedSlot();
            int slot = lastUsed != null ? lastUsed.SlotIndex : 0;

            var saveName = $"AutoSave_{System.DateTime.Now:HHmmss}";
            var ok = service.WriteSlot(slot, saveName);

            var uiMgr = ToastManager.Instance;
            if (uiMgr != null)
                uiMgr.Show(ok ? ToastLevel.Info : ToastLevel.Warning,
                    ok ? "ui.toast.autosaveSuccess" : "ui.toast.autosaveFailed");

            Debug.Log($"[AutoSave] {reason} → 槽 {slot}，结果：{(ok ? "成功" : "失败")}。");
        }
    }
}
