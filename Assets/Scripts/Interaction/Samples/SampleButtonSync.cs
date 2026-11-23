using UnityEngine;
using Mirror;

namespace Cqw.MultilayerFramework.Interaction.Samples
{
    /// <summary>示例按钮同步：SyncVar&lt;Color&gt; 驱动材质颜色（服务端权威）。</summary>
    public class SampleButtonSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnColorChanged))]
        public Color matColor = Color.red;

        Renderer rend;

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyVisual(matColor);
        }

        [Server]
        public void ServerToggle()
        {
            matColor = matColor == Color.red ? Color.green : Color.red;
            ApplyVisual(matColor);
        }

        void OnColorChanged(Color oldV, Color newV) => ApplyVisual(newV);

        void ApplyVisual(Color c)
        {
            if (rend == null) rend = GetComponentInChildren<Renderer>();
            if (rend != null) rend.material.color = c;
        }
    }
}
