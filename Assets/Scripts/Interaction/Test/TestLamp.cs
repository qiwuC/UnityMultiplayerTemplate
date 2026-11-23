using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Systems;

namespace Cqw.MultilayerFramework.Interaction
{
    /// <summary>
    /// 测试台灯（T9-04）：交互切换开/关。服务端权威（TestLampSync 的 SyncVar 同步）；
    /// 离线（无服务器/客户端）时本地回退直接切换视觉。视觉代码构建，回归测试保留资产。
    /// 注意：本类是普通 MonoBehaviour（InteractableBehaviour 基类），服务器判定经
    /// TestLampSync（NetworkBehaviour）的 isServer 属性。
    /// </summary>
    [AddComponentMenu("Cqw/Interaction/TestLamp")]
    public class TestLamp : InteractableBehaviour
    {
        public const string CooldownKeyPrefix = "interact.lamp:";

        TestLampSync sync;
        bool localOn;

        void Awake()
        {
            sync = GetComponent<TestLampSync>();
            EnsureVisual();
        }

        void EnsureVisual()
        {
            if (GetComponentInChildren<Renderer>() != null) return;

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bulb.name = "Bulb";
            bulb.transform.SetParent(transform, false);
            bulb.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            bulb.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(transform, false);
            pole.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            pole.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);

            var lightGo = new GameObject("LampLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 1.5f;
            light.enabled = false;
        }

        public override bool CanInteract(GameObject interactor)
        {
            if (!base.CanInteract(interactor)) return false;
            var key = CooldownKey();
            return string.IsNullOrEmpty(key) || CooldownManager.IsReady(key);
        }

        public override void OnInteract(GameObject interactor)
        {
            bool offline = !NetworkServer.active && !NetworkClient.active;

            if (sync == null || offline)
            {
                // 离线本地回退（无服务器时的直调验证）
                localOn = !localOn;
                if (sync != null) sync.ApplyVisual(localOn);
                else ApplyLocalVisual(localOn);
                return;
            }

            if (!sync.isServer) return; // 服务端权威：客户端交互经 PlayerInteractor 的 Cmd 到达

            if (data != null && data.cooldownSeconds > 0f)
            {
                var key = CooldownKey();
                if (!string.IsNullOrEmpty(key))
                {
                    if (!CooldownManager.IsReady(key)) return;
                    CooldownManager.Start(key, data.cooldownSeconds);
                }
            }

            sync.ServerToggle();
        }

        string CooldownKey() =>
            data != null && data.cooldownSeconds > 0f
                ? CooldownKeyPrefix + transform.position.ToString("F1")
                : null;

        void ApplyLocalVisual(bool on)
        {
            var bulb = GetComponentInChildren<Renderer>();
            if (bulb != null)
            {
                var mat = bulb.material;
                mat.SetColor("_BaseColor", on ? new Color(1f, 0.85f, 0.3f) : new Color(0.25f, 0.25f, 0.28f));
            }
            var light = GetComponentInChildren<Light>();
            if (light != null) light.enabled = on;
        }
    }
}
