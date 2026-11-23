using UnityEngine;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>
    /// 可交互行为基类（T9-01）：实现 IInteractable，供业务继承。
    /// 数据驱动（InteractableData）；OnEnable/OnDisable 自动进/出 InteractableRegistry。
    /// </summary>
    public abstract class InteractableBehaviour : MonoBehaviour, IInteractable
    {
        [Header("数据")]
        [Tooltip("交互数据（留空用默认值）")]
        public InteractableData data;

        public virtual string PromptKey => data != null && !string.IsNullOrEmpty(data.promptKey) ? data.promptKey : "interact.default";

        public virtual float InteractionRange => data != null ? data.range : 2.5f;

        public virtual bool RequiresServerAuthority => data == null || data.requireServerAuthority;

        public Vector3 InteractionPosition => transform.position;

        public virtual bool IsAvailable => true;

        public virtual bool CanInteract(GameObject interactor) => IsAvailable;

        public abstract void OnInteract(GameObject interactor);

        protected virtual void OnEnable() => InteractableRegistry.Register(this);

        protected virtual void OnDisable() => InteractableRegistry.Unregister(this);
    }
}
