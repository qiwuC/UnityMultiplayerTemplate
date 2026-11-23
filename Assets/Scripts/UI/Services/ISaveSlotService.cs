using System;
using UnityEngine;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>存档槽元数据。</summary>
    public class SaveSlotMeta
    {
        public int SlotIndex;
        public string SaveName;
        public DateTime LastSaved;
        public string GameVersion;
        public string ThumbnailPlaceholder = "□";
        public bool HasData;
    }

    /// <summary>存档槽服务接口（阶段五实现真实后端；本阶段 NullSaveSlotService 占位）。</summary>
    public interface ISaveSlotService
    {
        SaveSlotMeta[] GetSlots();
        SaveSlotMeta ReadSlot(int slotIndex);
        bool WriteSlot(int slotIndex, string saveName);
        bool DeleteSlot(int slotIndex);
        SaveSlotMeta GetLastUsedSlot();
    }

    /// <summary>占位实现：所有方法返回空/失败并打日志。阶段五替换为真实实现。</summary>
    public class NullSaveSlotService : ISaveSlotService
    {
        const string Tag = "[NullSaveSlotService]";

        public SaveSlotMeta[] GetSlots()
        {
            Debug.Log(Tag + " GetSlots() (placeholder phase 5). Returning 3 empty slots.");
            var slots = new SaveSlotMeta[3];
            for (int i = 0; i < 3; i++)
                slots[i] = new SaveSlotMeta { SlotIndex = i, HasData = false, SaveName = "Slot " + i + " (Empty)" };
            return slots;
        }

        public SaveSlotMeta ReadSlot(int slotIndex)
        {
            Debug.Log(Tag + " ReadSlot(" + slotIndex + ") (placeholder phase 5). Returning null.");
            return null;
        }

        public bool WriteSlot(int slotIndex, string saveName)
        {
            Debug.Log(Tag + " WriteSlot(" + slotIndex + ") (placeholder phase 5). Returning false.");
            return false;
        }

        public bool DeleteSlot(int slotIndex)
        {
            Debug.Log(Tag + " DeleteSlot(" + slotIndex + ") (placeholder phase 5). Returning false.");
            return false;
        }

        public SaveSlotMeta GetLastUsedSlot()
        {
            Debug.Log(Tag + " GetLastUsedSlot() (placeholder phase 5). Returning null.");
            return null;
        }
    }

    /// <summary>存档服务定位器（阶段五替换 Service 实例即可，UI 不改）。</summary>
    public static class SaveSlotServiceLocator
    {
        static ISaveSlotService service;
        public static ISaveSlotService Service
        {
            get
            {
                service ??= new NullSaveSlotService();
                return service;
            }
            set => service = value;
        }
    }
}
