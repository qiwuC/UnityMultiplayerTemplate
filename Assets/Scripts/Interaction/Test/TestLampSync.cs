using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>
    /// TestLamp 网络同步组件：SyncVar&lt;bool&gt; isOn（服务端权威）+ hook 更新视觉。
    /// 供 TestLamp（IInteractable）驱动；视觉（灯罩 Renderer + 点光源）由 TestLamp 代码构建。
    /// </summary>
    public class TestLampSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnIsOnChanged))]
        bool isOn;

        /// <summary>当前灯状态（客户端经同步获知）。</summary>
        public bool IsOn => isOn;

        /// <summary>状态变化事件（客户端 hook / 服务端直调后触发）。</summary>
        public event System.Action<bool> OnStateChanged;

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyVisual(isOn);
        }

        /// <summary>服务端切换灯状态。</summary>
        [Server]
        public void ServerToggle()
        {
            isOn = !isOn;
            ApplyVisual(isOn); // 服务端不触发 hook → 手动应用
            OnStateChanged?.Invoke(isOn);
        }

        /// <summary>应用视觉（公开：离线本地调试路径也可调用）。</summary>
        public void ApplyVisual(bool on)
        {
            var bulb = GetComponentInChildren<Renderer>();
            if (bulb != null)
            {
                var mat = bulb.material;
                mat.SetColor("_BaseColor", on ? new Color(1f, 0.85f, 0.3f) : new Color(0.25f, 0.25f, 0.28f));
                mat.SetColor("_EmissionColor", on ? new Color(1f, 0.8f, 0.2f) * 1.5f : Color.black);
                mat.EnableKeyword("_EMISSION");
            }

            var light = GetComponentInChildren<Light>();
            if (light != null)
            {
                light.enabled = on;
                light.color = new Color(1f, 0.85f, 0.4f);
            }
        }

        void OnIsOnChanged(bool oldV, bool newV)
        {
            ApplyVisual(newV);
            OnStateChanged?.Invoke(newV);
        }
    }
}
