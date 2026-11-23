using UnityEngine;
using Mirror;

namespace Cqw.MultilayerFramework.Interaction.Samples
{
    /// <summary>示例宝箱同步：SyncVar&lt;bool&gt; IsOpened + 缩放动画 + 不可用。</summary>
    public class SampleChestSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnOpenedChanged))]
        public bool isOpened;

        public System.Action<bool> OnOpenedChangedEvent;

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyVisual(isOpened);
        }

        [Server]
        public void ServerOpen()
        {
            if (isOpened) return;
            isOpened = true;
            ApplyVisual(true);
        }

        void OnOpenedChanged(bool oldV, bool newV) => ApplyVisual(newV);

        void ApplyVisual(bool opened)
        {
            if (opened) StartCoroutine(ScaleDown());
            OnOpenedChangedEvent?.Invoke(opened);
        }

        System.Collections.IEnumerator ScaleDown()
        {
            var t = transform;
            Vector3 start = t.localScale;
            Vector3 target = new Vector3(start.x, start.y * 0.3f, start.z);
            float el = 0;
            while (el < 0.3f)
            {
                el += Time.deltaTime;
                t.localScale = Vector3.Lerp(start, target, el / 0.3f);
                yield return null;
            }
            t.localScale = target;
        }
    }
}
