using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// Buff 定义资产（ScriptableObject）：静态配置双端一致。
    /// 运行时状态（剩余时间/叠加）由 PlayerStats 的 SyncList<BuffState> 同步。
    /// </summary>
    [CreateAssetMenu(fileName = "BuffData", menuName = "Cqw/Player/BuffData", order = 0)]
    public class BuffData : ScriptableObject
    {
        [Header("基础")]
        [Tooltip("Buff 唯一数字 ID")]
        public int BuffId;
        [Tooltip("显示名（本地化键，如 buff.speed）")]
        public string NameKey = "buff.unnamed";

        [Header("规则")]
        [Tooltip("持续时间（秒）")]
        public float Duration = 10f;
        [Tooltip("最大叠加层数（1=不可叠加，刷新时间）")]
        public int MaxStacks = 1;
    }
}
