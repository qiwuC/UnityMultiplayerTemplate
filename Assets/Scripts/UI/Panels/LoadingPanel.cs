using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.UI
{
    public class LoadingPanel : UIPanelBase
    {
        public override string PanelId => "ui.loading";
        public override bool IsModal => true;
        public override bool CloseOnBack => false;
        public float minDisplayDuration = 1f;

        Slider progressBar; TMP_Text progressText;

        protected override void OnOpen()
        {
            if (progressBar == null) Build();
            SetInput(false); RefreshProgress(0f);
        }
        protected override void OnClose() { SetInput(true); }

        void SetInput(bool on)
        { var lp = PlayerRegistry.GetLocalPlayer(); if (lp != null) { var c = lp.GetComponent<PlayerController>(); if (c != null) c.InputEnabled = on; } }

        void Build()
        {
            UIBuilder.CreateImage(Root, "BG", new Color(0.05f, 0.06f, 0.1f, 1f)).raycastTarget = true;
            var t = UIBuilder.CreateText(Root, "T", "", 36, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            t.rectTransform.anchorMin = new Vector2(0.15f, 0.6f); t.rectTransform.anchorMax = new Vector2(0.85f, 0.7f);
            SetLocalizedText(t, "ui.loading.title");
            progressBar = UIBuilder.CreateSlider(Root, "P", new Vector2(0, 28));
            progressBar.GetComponent<RectTransform>().anchorMin = new Vector2(0.15f, 0.42f); progressBar.GetComponent<RectTransform>().anchorMax = new Vector2(0.85f, 0.5f);
            progressBar.interactable = false;
            progressText = UIBuilder.CreateText(Root, "PT", "0%", 26, TMPro.TextAlignmentOptions.Center);
            progressText.rectTransform.anchorMin = new Vector2(0.15f, 0.32f); progressText.rectTransform.anchorMax = new Vector2(0.85f, 0.4f);
        }

        public void RefreshProgress(float t)
        { t = Mathf.Clamp01(t); if (progressBar != null) progressBar.value = t; if (progressText != null) progressText.text = (t * 100).ToString("F0") + "%"; }
        public void ShowError(string s) { ToastManager.Instance?.Show(ToastLevel.Error, "ui.toast.loadFailed"); }
    }
}
