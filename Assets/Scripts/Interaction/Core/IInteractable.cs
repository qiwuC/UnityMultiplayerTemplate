using UnityEngine;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>
    /// 可交互接口（T9-01）：定义范围/提示/行为。
    /// 扩展说明（已报告）：在任务清单接口成员之外新增 RequiresServerAuthority 与
    /// InteractionPosition 两项——前者供 PlayerInteractor 决定 Cmd 路由，后者供注册表范围过滤。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>是否可交互（含冷却/权限等业务判断）。</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>提示文本本地化键（经 LocalizationService 显示）。</summary>
        string PromptKey { get; }

        /// <summary>交互范围（米，以自身为圆心）。</summary>
        float InteractionRange { get; }

        /// <summary>执行交互。</summary>
        void OnInteract(GameObject interactor);

        /// <summary>当前是否可用（拾取后/损坏中等）。</summary>
        bool IsAvailable { get; }

        /// <summary>是否服务端权威（网络对象经 Cmd 路由到服务器执行）。</summary>
        bool RequiresServerAuthority { get; }

        /// <summary>交互判定参考点（默认自身位置）。</summary>
        Vector3 InteractionPosition { get; }
    }
}
