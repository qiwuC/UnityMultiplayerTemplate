using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 玩家生成点标记（显式 RoundRobin 轮询源）。
    /// 可与 Mirror 内置 NetworkStartPosition 并存；本组件由 RoundRobinSpawner 统一管理。
    /// </summary>
    [AddComponentMenu("Cqw/Network/SpawnPoint")]
    public class SpawnPoint : MonoBehaviour
    {
        /// <summary>轮询顺序（小者先）。</summary>
        [Tooltip("轮询顺序，小者先；同序按层级顺序")]
        public int order;

        /// <summary>生成时附加随机偏移半径（避免完全重叠）。</summary>
        [Tooltip("生成位置随机偏移半径（0=精确落点）")]
        public float jitterRadius = 0.5f;
    }
}
