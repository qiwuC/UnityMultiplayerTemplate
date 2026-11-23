using UnityEngine;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>可交互数据（ScriptableObject，T9-01）：提示键/范围/冷却/权威模式。</summary>
    [CreateAssetMenu(fileName = "InteractableData", menuName = "Cqw/Interaction/InteractableData")]
    public class InteractableData : ScriptableObject
    {
        [Tooltip("提示文本本地化键")]
        public string promptKey = "interact.default";

        [Tooltip("交互范围（米）")]
        [Min(0.1f)]
        public float range = 2.5f;

        [Tooltip("交互冷却（秒；0=无冷却）")]
        [Min(0f)]
        public float cooldownSeconds = 0f;

        [Tooltip("是否服务端权威（网络对象经 PlayerInteractor 的 Cmd 路由；离线时本地执行）")]
        public bool requireServerAuthority = true;
    }
}
