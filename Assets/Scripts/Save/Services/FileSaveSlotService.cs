using System;
using System.IO;
using System.Linq;
using Mirror;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>
    /// ISaveSlotService 的真实后端：3 槽位，文件持久化，
    /// 集成 SaveFileIO（原子写入/校验/备份）、SaveMigrationService（迁移）、SaveIOTaskQueue（异步）。
    /// </summary>
    public class FileSaveSlotService : ISaveSlotService
    {
        const int SlotCount = 3;
        const string Tag = "[FileSave]";

        /// <summary>存档根目录。</summary>
        public string SaveDirectory { get; }

        public FileSaveSlotService()
        {
            SaveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
            Debug.Log($"[FileSave] 存档目录：{SaveDirectory}");
        }

        string MainPath(int slot) => Path.Combine(SaveDirectory, $"slot_{slot}.json");
        string MetaPath(int slot) => Path.Combine(SaveDirectory, $"slot_{slot}.meta.json");
        string LastUsedPath => Path.Combine(SaveDirectory, "last_used.txt");

        // ============ ISaveSlotService ============

        public SaveSlotMeta[] GetSlots()
        {
            var result = new SaveSlotMeta[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                var (metaJson, result_) = SaveFileIO.ReadMeta(MetaPath(i));
                if (metaJson != null && result_ == SaveIOResult.Success)
                {
                    try
                    {
                        var j = JObject.Parse(metaJson);
                        result[i] = new SaveSlotMeta
                        {
                            SlotIndex = i,
                            HasData = true,
                            SaveName = j.Value<string>("saveName") ?? $"Slot {i}",
                            LastSaved = DateTime.TryParse(j.Value<string>("savedAtUtc"), out var dt) ? dt : DateTime.MinValue,
                            GameVersion = j.Value<string>("gameVersion") ?? "?"
                        };
                    }
                    catch { /* 解析失败视为空槽 */ }
                }
                if (result[i] == null)
                    result[i] = new SaveSlotMeta { SlotIndex = i, HasData = false, SaveName = $"Slot {i} (Empty)" };
            }
            return result;
        }

        public SaveSlotMeta ReadSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) { Debug.LogWarning($"{Tag} ReadSlot: 无效槽位 {slotIndex}。"); return null; }

            var (data, result) = SaveFileIO.ReadWithChecksum(MainPath(slotIndex));
            if (result != SaveIOResult.Success || data == null)
            {
                Debug.LogWarning($"{Tag} ReadSlot({slotIndex}): {result}（空槽或损坏）。");
                return null;
            }

            var root = SaveSerializer.Deserialize(data);
            if (root == null) { Debug.LogError($"{Tag} 反序列化失败。"); return null; }

            // 迁移
            var migration = SaveMigrationService.Migrate(root);
            if (migration == MigrationResult.FutureVersion || migration == MigrationResult.Failed)
            {
                Debug.LogError($"{Tag} 迁移失败/拒绝：{migration}。");
                return null;
            }

            // 通知所有 provider 恢复状态
            foreach (var provider in SaveDataProviderRegistry.GetAll())
            {
                if (root.Providers.TryGetValue(provider.ProviderId, out var json))
                {
                    try
                    {
                        var token = SaveSerializer.DeserializeProvider(json);
                        provider.RestoreState(token);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"{Tag} Provider '{provider.ProviderId}' RestoreState 异常：{e.Message}");
                    }
                }
            }

            // 返回元数据
            var (metaJson, _) = SaveFileIO.ReadMeta(MetaPath(slotIndex));
            var meta = ParseMeta(metaJson) ?? new SaveSlotMeta();
            meta.SlotIndex = slotIndex;
            meta.HasData = true;
            return meta;
        }

        public bool WriteSlot(int slotIndex, string saveName)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) { Debug.LogWarning($"{Tag} WriteSlot: 无效槽位 {slotIndex}。"); return false; }

            // 组装 SaveDataRoot
            var root = new SaveDataRoot();
            foreach (var provider in SaveDataProviderRegistry.GetAll())
            {
                try
                {
                    var state = provider.CaptureState();
                    root.Providers[provider.ProviderId] = SaveSerializer.SerializeProvider(state);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{Tag} Provider '{provider.ProviderId}' CaptureState 异常：{e.Message}");
                }
            }

            // 序列化
            var bytes = System.Text.Encoding.UTF8.GetBytes(SaveSerializer.Serialize(root));

            // 原子写入
            var ioResult = SaveFileIO.WriteAtomic(MainPath(slotIndex), bytes);
            if (ioResult != SaveIOResult.Success) { Debug.LogError($"{Tag} WriteAtomic 失败。"); return false; }

            // 写元数据
            var meta = new JObject
            {
                ["saveName"] = saveName ?? $"Slot {slotIndex}",
                ["savedAtUtc"] = SaveSerializer.UtcTimestamp(),
                ["gameVersion"] = Application.version,
                ["schemaVersion"] = SaveSchemaVersion.Current,
                ["hasData"] = true
            };
            SaveFileIO.WriteMeta(MetaPath(slotIndex), meta.ToString());

            // 记录最近使用槽位
            WriteLastUsedSlot(slotIndex);

            Debug.Log($"{Tag} 已写入槽 {slotIndex}（{bytes.Length} bytes）。");
            return true;
        }

        public bool DeleteSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            SaveFileIO.DeleteAll(MainPath(slotIndex));
            if (File.Exists(MetaPath(slotIndex))) File.Delete(MetaPath(slotIndex));
            Debug.Log($"{Tag} 已删除槽 {slotIndex}。");
            return true;
        }

        public SaveSlotMeta GetLastUsedSlot()
        {
            if (!File.Exists(LastUsedPath)) return null;
            if (!int.TryParse(File.ReadAllText(LastUsedPath).Trim(), out var slot)) return null;
            if (slot < 0 || slot >= SlotCount) return null;

            // 确认该槽位有数据
            if (!File.Exists(MainPath(slot))) return null;

            var (metaJson, _) = SaveFileIO.ReadMeta(MetaPath(slot));
            var meta = ParseMeta(metaJson) ?? new SaveSlotMeta();
            meta.SlotIndex = slot;
            meta.HasData = true;
            return meta;
        }

        // ============ 内部 ============

        SaveSlotMeta ParseMeta(string metaJson)
        {
            if (metaJson == null) return null;
            try
            {
                var j = JObject.Parse(metaJson);
                return new SaveSlotMeta
                {
                    SaveName = j.Value<string>("saveName") ?? $"Slot",
                    LastSaved = DateTime.TryParse(j.Value<string>("savedAtUtc"), out var dt) ? dt : DateTime.MinValue,
                    GameVersion = j.Value<string>("gameVersion") ?? "?"
                };
            }
            catch { return null; }
        }

        void WriteLastUsedSlot(int slot)
        {
            File.WriteAllText(LastUsedPath, slot.ToString());
        }
    }
}
