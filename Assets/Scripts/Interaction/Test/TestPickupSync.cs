using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>TestPickup 网络同步：SyncVar picked（服务端权威）+ 隐藏视觉。</summary>
    public class TestPickupSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnPickedChanged))]
        bool picked;

        [SyncVar]
        uint pickedByNetId;

        /// <summary>是否已被拾取。</summary>
        public bool Picked => picked;

        /// <summary>拾取者 netId（0=离线占位）。</summary>
        public uint PickedByNetId => pickedByNetId;

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyVisual(picked);
        }

        /// <summary>服务端执行拾取。</summary>
        [Server]
        public void ServerPickup(uint interactorNetId)
        {
            if (picked) return;
            picked = true;
            pickedByNetId = interactorNetId;
            ApplyVisual(picked);
            Debug.Log($"[TestPickup] 服务端拾取：{name} by netId={interactorNetId}");
        }

        void OnPickedChanged(bool oldV, bool newV) => ApplyVisual(newV);

        /// <summary>隐藏/显示视觉（Renderer+Collider；保留对象供 SyncVar 同步）。</summary>
        public void ApplyVisual(bool hidden)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !hidden;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = !hidden;
        }
    }
}
