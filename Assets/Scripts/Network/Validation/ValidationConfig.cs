using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>服务端聊天校验（B-02）：长度上限 + 频率限制，由 ChatNetwork 的发送路径调用。</summary>
    [CreateAssetMenu(fileName = "ValidationConfig", menuName = "Cqw/Network/ValidationConfig")]
    public class ValidationConfig : ScriptableObject
    {
        [Header("聊天")]
        public int chatMaxLength = 128;
        public float chatMinInterval = 0.5f;

        [Header("交互")]
        public float interactMinInterval = 0.2f;

        [Header("违规")]
        public int violationThreshold = 10;
        public ViolationPolicy violationPolicy = ViolationPolicy.Ignore;

        [Header("移动速度容差")]
        public float speedTolerance = 1.2f;

        public enum ViolationPolicy { Ignore, Kick }
    }
}
