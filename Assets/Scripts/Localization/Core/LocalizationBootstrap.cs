using UnityEngine;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Localization
{
    /// <summary>
    /// 本地化系统引导器：启动时初始化 LocalizationService（读取持久化语言→系统语言→en）。
    /// 挂在 UIRoot 或独立 DontDestroyOnLoad 对象上。
    /// </summary>
    [AddComponentMenu("Cqw/Localization/LocalizationBootstrap")]
    public class LocalizationBootstrap : MonoBehaviour
    {
        void Awake()
        {
            LocalizationService.Initialize();
            Debug.Log($"[Localization] 初始化完成，当前语言={LocalizationService.CurrentLanguage}。");
        }
    }
}
