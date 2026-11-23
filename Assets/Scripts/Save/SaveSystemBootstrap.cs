using UnityEngine;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>存档系统引导器：注册 FileSaveSlotService、SaveMetaProvider、AutoSaveScheduler。</summary>
    [AddComponentMenu("Cqw/Save/SaveSystemBootstrap")]
    public class SaveSystemBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // 注册 provider（阶段三/后续系统在此追加）
            SaveDataProviderRegistry.Register(new SaveMetaProvider());

            // 注册真实存档服务（替换 NullSaveSlotService）
            SaveSlotServiceLocator.Service = new FileSaveSlotService();

            // 确保自动存档调度器存在
            if (AutoSaveScheduler.Instance == null)
            {
                var go = new GameObject("AutoSaveScheduler");
                go.AddComponent<AutoSaveScheduler>();
            }

            // 确保异步队列存在
            if (SaveIOTaskQueue.Instance == null)
            {
                var go = new GameObject("SaveIOTaskQueue");
                go.AddComponent<SaveIOTaskQueue>();
            }

            Debug.Log("[SaveSystem] 存档系统初始化完成（FileSaveSlotService + MetaProvider + AutoSave + TaskQueue）。");
        }
    }
}
