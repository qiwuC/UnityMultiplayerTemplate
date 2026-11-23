using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Cqw.MultilayerFramework.UI;
using Cqw.MultilayerFramework.Audio;
using Cqw.MultilayerFramework.Localization;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>设置键名常量（与阶段四 SettingsPanel / 阶段六 AudioMixerController 键名保持一致）。</summary>
    public static class GameSettingsKeys
    {
        // ---- 视频 ----
        public const string Resolution = "settings.resolution";        // "1920x1080"
        public const string FullscreenMode = "settings.fullscreen";    // int（FullScreenMode）
        public const string VSync = "settings.vsync";
        public const string Quality = "settings.quality";              // int（QualitySettings 索引）
        public const string FrameRateCap = "settings.frameRateCap";    // int，0=不限制

        // ---- 音频（AudioMixerController 已用键）----
        public const string AudioMaster = "audio.master";
        public const string AudioMusic = "audio.music";
        public const string AudioSfx = "audio.sfx";
        public const string AudioVoice = "audio.voice";

        // ---- 操作 ----
        public const string MouseSens = "settings.mouseSens";
        public const string PadSens = "settings.padSens";

        // ---- 游戏 ----
        public const string Language = "game.language";
        public const string ShowNickname = "settings.showNickname";

        // ---- 自动存档 ----
        public const string AutoSaveEnabled = "settings.autoSave";
        public const string AutoSaveInterval = "settings.autoSaveInterval";
    }

    /// <summary>
    /// GameSettings（T8-09）：JSON 持久化 IGameSettingsService 实现，替换阶段四 PlayerPrefs 占位。
    /// 存储：Application.persistentDataPath/settings.json（Newtonsoft.Json）。
    /// 首次运行检测 PlayerPrefs 旧值 → 迁移到 JSON → 删除旧键（保留迁移日志 settings-migration.log）。
    /// 变更标记 dirty，经 Scheduler 500ms 防抖写盘；退出（Application.quitting）强制落盘。
    /// </summary>
    public class JsonGameSettingsService : IGameSettingsService
    {
        const float FlushDelaySeconds = 0.5f;

        /// <summary>迁移候选键 → 类型（b/i/f/s）。</summary>
        static readonly Dictionary<string, char> migratableKeys = new Dictionary<string, char>
        {
            ["settings.vsync"] = 'b',
            ["settings.quality"] = 'i',
            ["settings.master"] = 'f',
            ["settings.music"] = 'f',
            ["settings.sfx"] = 'f',
            ["settings.voice"] = 'f',
            ["settings.mouseSens"] = 'f',
            ["settings.padSens"] = 'f',
            ["settings.showNickname"] = 'b',
            ["settings.autoSave"] = 'b',
            ["settings.autoSaveInterval"] = 'f',
            ["audio.master"] = 'f',
            ["audio.music"] = 'f',
            ["audio.sfx"] = 'f',
            ["audio.voice"] = 'f',
            ["game.language"] = 's',
        };

        readonly string filePath;
        readonly string migrationLogPath;
        JObject root = new JObject();
        bool dirty;
        ScheduleHandle flushHandle;

        public string FilePath => filePath;

        public JsonGameSettingsService(string path = null)
        {
            filePath = path ?? Path.Combine(Application.persistentDataPath, "settings.json");
            migrationLogPath = Path.Combine(Application.persistentDataPath, "settings-migration.log");
            Load();
            MigrateFromPlayerPrefs();
            Application.quitting += FlushNow;
        }

        void Load()
        {
            try
            {
                if (File.Exists(filePath))
                    root = JObject.Parse(File.ReadAllText(filePath)) ?? new JObject();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSettings] 读取失败（使用空配置）：{e.Message}");
                root = new JObject();
            }
        }

        // ============ IGameSettingsService ============

        public int GetInt(string key, int defaultValue)
        {
            var t = root[key];
            if (t == null) return defaultValue;
            try { return t.Value<int>(); } catch { return defaultValue; }
        }

        public void SetInt(string key, int value) { root[key] = value; MarkDirty(); }

        public float GetFloat(string key, float defaultValue)
        {
            var t = root[key];
            if (t == null) return defaultValue;
            try { return t.Value<float>(); } catch { return defaultValue; }
        }

        public void SetFloat(string key, float value) { root[key] = value; MarkDirty(); }

        public bool GetBool(string key, bool defaultValue)
        {
            var t = root[key];
            if (t == null) return defaultValue;
            try { return t.Value<bool>(); } catch { return defaultValue; }
        }

        public void SetBool(string key, bool value) { root[key] = value; MarkDirty(); }

        public string GetString(string key, string defaultValue)
        {
            var t = root[key];
            if (t == null) return defaultValue;
            try { return t.Value<string>(); } catch { return defaultValue; }
        }

        public void SetString(string key, string value) { root[key] = value ?? ""; MarkDirty(); }

        /// <summary>立即落盘。</summary>
        public void Save() => FlushNow();

        /// <summary>恢复默认：清空全部键 → 落盘 → 按默认值 ApplyAll。</summary>
        public void RevertAll()
        {
            root.RemoveAll();
            FlushNow();
            ApplySettings();
            Debug.Log("[GameSettings] RevertAll：已清除全部键并按默认值应用。");
        }

        // ============ 持久化 ============

        void MarkDirty()
        {
            dirty = true;
            if (flushHandle != null) Scheduler.Cancel(flushHandle);
            flushHandle = Scheduler.Delay(FlushDelaySeconds, FlushNow);
        }

        /// <summary>立即写盘（防抖到期 / 退出 / Save 调用）。</summary>
        public void FlushNow()
        {
            if (flushHandle != null) Scheduler.Cancel(flushHandle);
            flushHandle = null;
            if (!dirty) return;
            dirty = false;
            try
            {
                File.WriteAllText(filePath, root.ToString(Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSettings] 写盘失败：{e.Message}");
            }
        }

        // ============ PlayerPrefs 迁移 ============

        void MigrateFromPlayerPrefs()
        {
            if (File.Exists(filePath)) return; // 已有 JSON：非首次运行（或已迁移过）

            var migrated = new List<string>();
            foreach (var kv in migratableKeys)
            {
                var key = kv.Key;
                if (!PlayerPrefs.HasKey(key)) continue;
                switch (kv.Value)
                {
                    case 'b': root[key] = PlayerPrefs.GetInt(key) == 1; break;
                    case 'i': root[key] = PlayerPrefs.GetInt(key); break;
                    case 'f': root[key] = PlayerPrefs.GetFloat(key); break;
                    case 's': root[key] = PlayerPrefs.GetString(key); break;
                }
                PlayerPrefs.DeleteKey(key);
                migrated.Add(key);
            }

            if (migrated.Count == 0) return;
            PlayerPrefs.Save();
            dirty = true;
            FlushNow();

            var detail = string.Join(", ", migrated);
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 迁移 {migrated.Count} 项 PlayerPrefs 值 → {filePath}\n{detail}\n";
            try { File.AppendAllText(migrationLogPath, logLine); } catch { /* 尽力而为 */ }
            Debug.Log($"[GameSettings] 迁移完成：{migrated.Count} 项 PlayerPrefs 值 → settings.json（{detail}）");
            GameLogger.LogInfo($"[GameSettings] 迁移完成：{migrated.Count} 项（明细见 settings-migration.log）");
        }

        // ============ 应用设置 ============

        /// <summary>将全部设置应用到各子系统（视频/音频/语言）。</summary>
        public void ApplySettings() => ApplyInternal();

        /// <summary>静态入口：对当前活动服务执行 ApplyAll（活动服务非 JSON 后端时跳过）。</summary>
        public static void ApplyAll(JsonGameSettingsService service = null)
        {
            var svc = service ?? GameSettingsServiceLocator.Service as JsonGameSettingsService;
            if (svc == null)
            {
                Debug.LogWarning("[GameSettings] ApplyAll：活动服务非 JSON 后端，跳过。");
                return;
            }
            svc.ApplyInternal();
        }

        void ApplyInternal()
        {
            // ---- 视频 ----
            try
            {
                QualitySettings.vSyncCount = GetBool(GameSettingsKeys.VSync, true) ? 1 : 0;
                int cap = GetInt(GameSettingsKeys.FrameRateCap, 0);
                Application.targetFrameRate = cap > 0 ? cap : -1;

                int quality = GetInt(GameSettingsKeys.Quality, QualitySettings.GetQualityLevel());
                QualitySettings.SetQualityLevel(Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1), true);

                var mode = (FullScreenMode)GetInt(GameSettingsKeys.FullscreenMode, (int)FullScreenMode.FullScreenWindow);
                var res = GetString(GameSettingsKeys.Resolution, "");
                if (!string.IsNullOrEmpty(res))
                {
                    var parts = res.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w > 0 && h > 0)
                        Screen.SetResolution(w, h, mode);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettings] 视频设置应用失败：{e.Message}");
            }

            // ---- 音频（实例存在则应用；否则其自身 Awake 会从服务读取）----
            if (AudioMixerController.Instance != null)
                AudioMixerController.Instance.LoadAndApply();

            // ---- 语言（仅在有持久化值且与当前不同时切换；LocalizationBootstrap 之后再次应用无副作用）----
            var lang = GetString(GameSettingsKeys.Language, "");
            if (!string.IsNullOrEmpty(lang) && lang != LocalizationService.CurrentLanguage)
                LocalizationService.SetLanguage(lang, persist: false);

            GameLogger.LogInfo("[GameSettings] ApplyAll 完成。");
        }
    }
}
