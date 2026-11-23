using UnityEngine;
using Mirror;
using TMPro;

namespace Cqw.MultilayerFramework.Interaction.Samples
{
    /// <summary>示例宝箱计数器（T-A03）：SyncVar&lt;int&gt; 服务端权威，HUD TMP 显示。</summary>
    public class SampleChestCounter : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnCountChanged))]
        public int openedCount;

        TMP_Text hudText;

        public static SampleChestCounter Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureHudText();
            UpdateHud(openedCount);
        }

        [Server]
        public void ServerAddOpened()
        {
            openedCount++;
            UpdateHud(openedCount);
        }

        void OnCountChanged(int oldV, int newV) => UpdateHud(newV);

        void EnsureHudText()
        {
            if (hudText != null) return;
            var canvasGo = GameObject.Find("UICanvasRoot");
            if (canvasGo == null) return;
            var textGo = new GameObject("SampleChestCounter", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform.Find("Main") ?? canvasGo.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.02f, 0.9f);
            rt.anchorMax = new Vector2(0.25f, 0.96f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            hudText = textGo.GetComponent<TextMeshProUGUI>();
            hudText.fontSize = 28;
            hudText.color = Color.yellow;
            hudText.text = "";
        }

        void UpdateHud(int count)
        {
            EnsureHudText();
            if (hudText != null) hudText.text = $"宝箱: {count}";
        }
    }
}
