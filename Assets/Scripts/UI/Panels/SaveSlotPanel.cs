using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>存档槽面板：3 槽，读/写/删除（二次确认），ISaveSlotService 接口化。</summary>
    public class SaveSlotPanel : UIPanelBase
    {
        public override string PanelId => "ui.saveSlots";
        public override bool IsModal => true;

        ISaveSlotService SaveService => SaveSlotServiceLocator.Service;
        RectTransform[] slotRows;

        protected override void OnOpen()
        {
            if (slotRows == null) Build();
            RefreshSlots();
        }

        protected override void OnClose() { }

        void Build()
        {
            var bg = UIBuilder.CreateImage(Root, "BG", new Color(0.08f, 0.09f, 0.13f, 0.97f));
            bg.raycastTarget = true;

            var title = UIBuilder.CreateText(Root, "Title", "", 36, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            title.rectTransform.anchorMin = new Vector2(0.1f, 0.85f); title.rectTransform.anchorMax = new Vector2(0.9f, 0.94f);
            SetLocalizedText(title, "ui.save.title");

            slotRows = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                int slotIndex = i; // closure
                var row = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
                var rRt = (RectTransform)row.transform;
                rRt.SetParent(Root, false);
                rRt.anchorMin = new Vector2(0.06f, 0.6f - i * 0.18f);
                rRt.anchorMax = new Vector2(0.94f, 0.74f - i * 0.18f);
                rRt.offsetMin = Vector2.zero; rRt.offsetMax = Vector2.zero;
                row.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.19f, 1f);
                slotRows[i] = rRt;

                var info = UIBuilder.CreateText(rRt, "Info", "", 22);
                info.rectTransform.anchorMin = new Vector2(0.02f, 0.15f);
                info.rectTransform.anchorMax = new Vector2(0.6f, 0.85f);
                SetLocalizedText(info, $"ui.save.slot{i}");
                info.text += $"\n(Empty)";

                var readBtn = UIBuilder.CreateButton(rRt, "Btn_Read", "ui.save.read", out _, new Vector2(140, 36));
                readBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.62f, 0.15f);
                readBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.75f, 0.85f);
                readBtn.GetComponent<RectTransform>().offsetMin = Vector2.zero; readBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                readBtn.onClick.AddListener(() => ReadSlot(slotIndex));

                var writeBtn = UIBuilder.CreateButton(rRt, "Btn_Write", "ui.save.write", out _, new Vector2(140, 36));
                writeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.77f, 0.15f);
                writeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.87f, 0.85f);
                writeBtn.GetComponent<RectTransform>().offsetMin = Vector2.zero; writeBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                writeBtn.onClick.AddListener(() => WriteSlotWithConfirm(slotIndex));

                var deleteBtn = UIBuilder.CreateButton(rRt, "Btn_Delete", "ui.save.delete", out _, new Vector2(140, 36),
                    new Color(0.5f, 0.22f, 0.22f));
                deleteBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.89f, 0.15f);
                deleteBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.85f);
                deleteBtn.GetComponent<RectTransform>().offsetMin = Vector2.zero; deleteBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                deleteBtn.onClick.AddListener(() => DeleteSlotWithConfirm(slotIndex));
            }
        }

        void RefreshSlots()
        {
            var slots = SaveService?.GetSlots();
            if (slots == null) return;
            for (int i = 0; i < slotRows.Length && i < slots.Length; i++)
            {
                var info = slotRows[i].GetComponentInChildren<TMP_Text>();
                if (info != null)
                    info.text = $"Slot {slots[i].SlotIndex}: {slots[i].SaveName}\n{slots[i].ThumbnailPlaceholder}";
            }
        }

        void ReadSlot(int slotIndex)
        {
            var meta = SaveService?.ReadSlot(slotIndex);
            if (meta == null)
                ToastManager.Instance?.Show(ToastLevel.Warning, "ui.toast.saveReadFailed");
            else
                ToastManager.Instance?.Show(ToastLevel.Success, "ui.toast.saveReadSuccess");
        }

        void WriteSlotWithConfirm(int slotIndex)
        {
            ConfirmDialog.Instance?.Show("ui.confirm.saveWriteTitle", "ui.confirm.saveWriteMessage", () =>
            {
                var ok = SaveService?.WriteSlot(slotIndex, $"Save_{slotIndex}");
                ToastManager.Instance?.Show(ok == true ? ToastLevel.Success : ToastLevel.Error,
                    ok == true ? "ui.toast.saveWriteSuccess" : "ui.toast.saveWriteFailed");
            });
        }

        void DeleteSlotWithConfirm(int slotIndex)
        {
            ConfirmDialog.Instance?.Show("ui.confirm.saveDeleteTitle", "ui.confirm.saveDeleteMessage", () =>
            {
                var ok = SaveService?.DeleteSlot(slotIndex);
                ToastManager.Instance?.Show(ok == true ? ToastLevel.Success : ToastLevel.Error,
                    ok == true ? "ui.toast.saveDeleteSuccess" : "ui.toast.saveDeleteFailed");
                RefreshSlots();
            });
        }
    }
}
