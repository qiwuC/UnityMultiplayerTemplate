using UnityEngine;
using Mirror;

namespace Cqw.MultilayerFramework.Interaction.Samples
{
    /// <summary>示例宝箱（T-A02）：交互后打开（缩放动画 + 不可用），服务端权威 SyncVar&lt;bool&gt;。</summary>
    public class Sample_Chest : InteractableBehaviour
    {
        SampleChestSync sync;

        void Awake() { sync = GetComponent<SampleChestSync>(); }

        public override bool IsAvailable => sync == null || !sync.isOpened;

        public override void OnInteract(GameObject interactor)
        {
            if (sync == null) sync = GetComponent<SampleChestSync>();
            if (sync == null) return;
            bool offline = !NetworkServer.active && !NetworkClient.active;
            if (offline || sync.isServer) sync.ServerOpen();
            var counter = Object.FindAnyObjectByType<SampleChestCounter>();
            if (counter != null && (offline || sync.isServer)) counter.ServerAddOpened();
        }
    }
}
