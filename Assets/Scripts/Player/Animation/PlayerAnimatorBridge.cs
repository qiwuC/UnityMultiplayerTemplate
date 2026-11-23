using UnityEngine;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>
    /// 玩家动画桥：把移动状态（速度/着地/跳跃）写入 Animator 参数。
    /// 同步复用阶段二 NetworkAnimationSync（不新增同步通道）：
    ///   - 本地玩家（客户端权威）由 PlayerController 驱动 → NetworkAnimationSync 采集并同步到服务器/其他客户端
    ///   - 远程玩家：NetworkAnimationSync 应用同步来的参数 → 本桥不再覆写（避免与同步值冲突）
    /// 无 Animator 时安全降级（不报错）。
    /// </summary>
    [AddComponentMenu("Cqw/Player/PlayerAnimatorBridge")]
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [Header("目标（留空自动查找子物体 Animator）")]
        public Animator animator;

        [Header("参数名（与 AnimatorController 一致）")]
        public string speedParam = "Speed";
        public string moveXParam = "MoveX";
        public string moveYParam = "MoveY";
        public string groundedParam = "IsGrounded";
        public string jumpParam = "Jump";
        public string verticalSpeedParam = "VerticalSpeed";

        [Header("引用（留空自动查找）")]
        public PlayerController controller;
        public MonoBehaviour inputSourceBehaviour;

        IPlayerInputSource inputSource;
        AnimatorControllerParameterType[] paramTypes;
        string[] paramNames;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (controller == null) controller = GetComponent<PlayerController>();
            CacheParameters();
        }

        void CacheParameters()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                paramNames = System.Array.Empty<string>();
                paramTypes = System.Array.Empty<AnimatorControllerParameterType>();
                return;
            }
            var ps = animator.parameters;
            paramNames = new string[ps.Length];
            paramTypes = new AnimatorControllerParameterType[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                paramNames[i] = ps[i].name;
                paramTypes[i] = ps[i].type;
            }
        }

        void ResolveInput()
        {
            if (inputSourceBehaviour is IPlayerInputSource s) { inputSource = s; return; }
            inputSource = GetComponent<DefaultPlayerInputSource>();
        }

        void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return; // 无 Animator 安全降级

            // 远程玩家：参数由 NetworkAnimationSync 应用，本桥不覆写
            var identity = controller != null ? controller.GetComponent<Mirror.NetworkIdentity>() : GetComponent<Mirror.NetworkIdentity>();
            if (identity != null && identity.isClient && !identity.isLocalPlayer) return;

            if (inputSource == null) ResolveInput();

            float speed = controller != null ? controller.CurrentSpeed : 0f;
            bool grounded = controller != null ? controller.IsGrounded : true;
            Vector2 move = inputSource != null ? inputSource.Move : Vector2.zero;
            float vy = controller != null ? controller.Velocity.y : 0f;
            bool jumpPressed = inputSource != null && inputSource.JumpPressed;

            SetFloatSafe(speedParam, speed);
            SetFloatSafe(moveXParam, move.x);
            SetFloatSafe(moveYParam, move.y);
            SetFloatSafe(verticalSpeedParam, vy);
            SetBoolSafe(groundedParam, grounded);
            if (jumpPressed) SetTriggerSafe(jumpParam);
        }

        // ---- 按参数实际类型安全写入（参数缺失时静默跳过）----

        bool HasParam(string name, AnimatorControllerParameterType type)
        {
            for (int i = 0; i < paramNames.Length; i++)
                if (paramNames[i] == name && paramTypes[i] == type) return true;
            return false;
        }

        void SetFloatSafe(string name, float value)
        {
            if (HasParam(name, AnimatorControllerParameterType.Float)) animator.SetFloat(name, value);
        }

        void SetBoolSafe(string name, bool value)
        {
            if (HasParam(name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, value);
        }

        void SetTriggerSafe(string name)
        {
            if (HasParam(name, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(name);
        }
    }
}
