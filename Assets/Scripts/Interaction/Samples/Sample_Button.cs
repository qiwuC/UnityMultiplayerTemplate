using UnityEngine;
using Mirror;
using Cqw.MultilayerFramework.Systems;

namespace Cqw.MultilayerFramework.Interaction.Samples
{
    /// <summary>示例按钮（T-A02）：交互切换材质颜色（服务端权威 SyncVar&lt;Color&gt;）。</summary>
    public class Sample_Button : InteractableBehaviour
    {
        SampleButtonSync sync;

        void Awake() { sync = GetComponent<SampleButtonSync>(); }

        public override void OnInteract(GameObject interactor)
        {
            if (sync == null) sync = GetComponent<SampleButtonSync>();
            if (sync == null) return;
            bool offline = !NetworkServer.active && !NetworkClient.active;
            if (offline || sync.isServer) sync.ServerToggle();
        }
    }
}
