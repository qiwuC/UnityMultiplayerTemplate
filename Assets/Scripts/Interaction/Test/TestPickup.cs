using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Systems;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>拾取事件（EventBus 广播；PlayerIdentity 记录采用占位实现，已报告）。</summary>
    public struct PickupEvent
    {
        public string PickerName;
        public string ItemName;
        public uint PickerNetId;
    }

    /// <summary>
    /// 测试拾取物（T9-04）：交互后"拾取"（服务端权威：TestPickupSync 的 SyncVar 同步隐藏全部端视觉），
    /// 记录采用占位（EventBus PickupEvent + pickedByNetId）。回归测试保留资产。
    /// 注意：本类是普通 MonoBehaviour（InteractableBehaviour 基类），服务器判定经
    /// TestPickupSync（NetworkBehaviour）的 isServer 属性。
    /// </summary>
    [AddComponentMenu("Cqw/Interaction/TestPickup")]
    public class TestPickup : InteractableBehaviour
    {
        TestPickupSync sync;

        void Awake()
        {
            sync = GetComponent<TestPickupSync>();
            EnsureVisual();
        }

        void EnsureVisual()
        {
            if (GetComponentInChildren<Renderer>() != null) return;
            var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            item.name = "PickupItem";
            item.transform.SetParent(transform, false);
            item.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            item.transform.localScale = Vector3.one * 0.4f;
        }

        public override bool IsAvailable => sync == null || !sync.Picked;

        public override void OnInteract(GameObject interactor)
        {
            bool offline = !NetworkServer.active && !NetworkClient.active;
            string pickerName = interactor != null ? interactor.name : "?";
            uint pickerNetId = 0;

            if (offline)
            {
                if (sync != null) sync.ServerPickup(0);
                else SetLocalHidden(true);
            }
            else
            {
                if (sync == null || !sync.isServer) return; // 服务端权威
                if (sync.Picked) return;
                var ident = interactor != null ? interactor.GetComponent<NetworkIdentity>() : null;
                if (ident != null) pickerNetId = ident.netId;
                sync.ServerPickup(pickerNetId);
            }

            // 占位记录：EventBus 广播（业务可在订阅方写入背包/PlayerIdentity 扩展）
            var identity = interactor != null ? interactor.GetComponent<PlayerIdentity>() : null;
            if (identity != null) pickerName = identity.DisplayName;
            EventBus.Publish(new PickupEvent { PickerName = pickerName, ItemName = name, PickerNetId = pickerNetId });
        }

        void SetLocalHidden(bool hidden)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !hidden;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = !hidden;
        }
    }
}
