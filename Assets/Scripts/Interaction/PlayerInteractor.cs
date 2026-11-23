using System;
using Mirror;
using UnityEngine;
using Object = UnityEngine.Object;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Player;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>
    /// 玩家交互器（T9-02/T9-03）：定时从 InteractableRegistry 查询范围内最近可用目标，
    /// 目标变化时驱动 InteractionPromptScreen / InteractionPromptWorld 显示与隐藏；
    /// 交互输入（Interact 边沿）→ 服务端权威目标经 Cmd 路由，否则本地直调。
    /// 仅本地控制玩家（isOwned）启用；不阻塞移动（纯读输入）。
    /// </summary>
    [AddComponentMenu("Cqw/Interaction/PlayerInteractor")]
    public class PlayerInteractor : NetworkBehaviour
    {
        [Header("检测")]
        [Tooltip("扫描间隔（秒）")]
        public float scanInterval = 0.1f;
        [Tooltip("扫描半径上限（米；实际还需满足目标自身 InteractionRange）")]
        public float scanRadius = 6f;

        /// <summary>当前目标（null=无）。</summary>
        public IInteractable CurrentTarget { get; private set; }

        /// <summary>目标变化事件（null 表示离开）。</summary>
        public event Action<IInteractable> OnTargetChanged;

        IPlayerInputSource inputSource;
        InteractionPromptScreen screenPrompt;
        InteractionPromptWorld worldPrompt;
        IInputDeviceProvider deviceProvider;
        bool prevInteractHeld;
        float nextScanTime;

        public override void OnStartClient()
        {
            base.OnStartClient();
            // 仅本地控制对象（isOwned：含本地多人额外玩家）启用
            enabled = isOwned;
            if (isOwned) ResolveInputSource();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            ResolveInputSource();
        }

        void ResolveInputSource()
        {
            if (inputSource == null)
                inputSource = GetComponent<IPlayerInputSource>();
        }

        void Update()
        {
            if (!isOwned) return;
            if (inputSource == null) ResolveInputSource();

            // 定时扫描（避免每帧全表过滤）
            if (Time.unscaledTime >= nextScanTime)
            {
                nextScanTime = Time.unscaledTime + scanInterval;
                ScanForTarget();
            }

            // 交互输入（按住 → 本帧按下边沿）
            bool held = inputSource != null && inputSource.Interact;
            if (held && !prevInteractHeld) TryInteract();
            prevInteractHeld = held;
        }

        void ScanForTarget()
        {
            IInteractable best = null;
            float bestDist = float.MaxValue;

            foreach (var it in InteractableRegistry.QueryRange(transform.position, scanRadius))
            {
                if ((it as Object) == null) continue;
                float d = Vector3.Distance(transform.position, it.InteractionPosition);
                if (d > it.InteractionRange) continue;
                if (!it.IsAvailable || !it.CanInteract(gameObject)) continue;
                if (d < bestDist) { bestDist = d; best = it; }
            }

            bool changed = !SameTarget(best, CurrentTarget);
            if (!changed && best != null && !best.IsAvailable)
            {
                // 当前目标失效（拾取等）→ 清空
                best = null;
                changed = true;
            }

            if (!changed) return;
            CurrentTarget = best;
            OnTargetChanged?.Invoke(best);
            UpdatePrompts();
        }

        static bool SameTarget(IInteractable a, IInteractable b)
        {
            var ao = a as Object;
            var bo = b as Object;
            if (ao == null && bo == null) return true;   // 都为空/销毁
            if (ao == null || bo == null) return false;
            return ao == bo;
        }

        /// <summary>执行交互（本地边沿触发入口）。</summary>
        public void TryInteract()
        {
            var target = CurrentTarget;
            if ((target as Object) == null) return;

            // 服务端权威 + 本端非服务器 + 目标已联网 → Cmd 路由
            if (target.RequiresServerAuthority && !isServer && netIdentity != null)
            {
                var targetId = (target as Component).GetComponent<NetworkIdentity>();
                if (targetId != null && targetId.netId != 0)
                {
                    CmdInteract(targetId.netId);
                    return;
                }
            }

            // 已是服务器 / 非权威目标 / 未联网 → 本地直调
            target.OnInteract(gameObject);
        }

        [Command]
        void CmdInteract(uint targetNetId)
        {
            if (!NetworkServer.spawned.TryGetValue(targetNetId, out var identity) || identity == null) return;
            var interactable = identity.GetComponent<IInteractable>();
            if (interactable == null) return;
            if (!interactable.IsAvailable || !interactable.CanInteract(gameObject)) return;
            interactable.OnInteract(gameObject);
        }

        // ============ 提示（T9-03）============

        /// <summary>外部设置进度（0-1，长按交互业务用）。</summary>
        public void SetProgress(float t)
        {
            screenPrompt?.SetProgress(t);
            worldPrompt?.SetProgress(t);
        }

        void UpdatePrompts()
        {
            bool show = (CurrentTarget as Object) != null;
            ResolvePrompts();

            if (screenPrompt != null)
            {
                screenPrompt.SetDeviceProvider(ResolveDeviceProvider());
                if (show)
                    screenPrompt.Show(new InteractionPromptData { Key = CurrentTarget.PromptKey, IconSprite = "", ShowProgress = false });
                else
                    screenPrompt.Hide();
            }

            if (worldPrompt != null)
            {
                if (show)
                {
                    var targetTransform = (CurrentTarget as Component).transform;
                    worldPrompt.transform.SetParent(targetTransform, false);
                    worldPrompt.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                    worldPrompt.Show(new InteractionPromptData { Key = CurrentTarget.PromptKey, IconSprite = "", ShowProgress = false });
                }
                else
                {
                    worldPrompt.Hide();
                }
            }
        }

        void ResolvePrompts()
        {
            // 缓存被销毁（切场景）时重新查找；Include 兼容初始隐藏的提示组件
            if (screenPrompt == null)
                screenPrompt = Object.FindAnyObjectByType<InteractionPromptScreen>(FindObjectsInactive.Include);
            if (worldPrompt == null)
            {
                worldPrompt = Object.FindAnyObjectByType<InteractionPromptWorld>(FindObjectsInactive.Include);
                if (worldPrompt == null)
                    worldPrompt = new GameObject("InteractionPromptWorld", typeof(InteractionPromptWorld))
                        .GetComponent<InteractionPromptWorld>();
            }
        }

        IInputDeviceProvider ResolveDeviceProvider()
        {
            if (deviceProvider != null) return deviceProvider;
            // 阶段十 LocalInputRelay 实现 IInputDeviceProvider 时自动接管
            var provider = GetComponent<IInputDeviceProvider>();
            if (provider != null)
            {
                deviceProvider = provider;
                return deviceProvider;
            }
            deviceProvider = new DefaultInputDeviceProvider(GetComponent<DefaultPlayerInputSource>());
            return deviceProvider;
        }
    }
}
