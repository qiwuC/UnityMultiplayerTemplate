using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>
    /// 浜や簰鎻愮ず锛?D 涓栫晫绌洪棿妯″紡锛夛細World Space Canvas + TMP锛屽缁堥潰鍚戠浉鏈恒€?    /// 闃舵涔濇帴鍏ユ娴嬮€昏緫鍚庤皟鐢?Show/Hide/SetProgress銆?    /// </summary>
    [AddComponentMenu("Cqw/UI/InteractionPromptWorld")]
    public class InteractionPromptWorld : MonoBehaviour
    {
        TMP_Text promptText;
        Canvas worldCanvas;
        Transform camTransform;

        void Awake()
        {
            Build();
            Hide();
        }

        void Build()
        {
            var canvasGo = new GameObject("WorldCanvas", typeof(Canvas), typeof(CanvasScaler));
            worldCanvas = canvasGo.GetComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = Vector3.zero;

            var textGo = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            var tRt = (RectTransform)textGo.transform;
            tRt.SetParent(canvasGo.transform, false);
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            promptText = textGo.GetComponent<TextMeshProUGUI>();
            promptText.fontSize = 4;
            promptText.color = Color.white;
            promptText.alignment = TMPro.TextAlignmentOptions.Center;
            promptText.raycastTarget = false;
        }

        void LateUpdate()
        {
            // 濮嬬粓闈㈠悜鐩告満
            if (camTransform == null && Camera.main != null) camTransform = Camera.main.transform;
            if (camTransform != null)
                worldCanvas.transform.LookAt(worldCanvas.transform.position + camTransform.forward);
        }

        /// <summary>鏄剧ず浜や簰鎻愮ず銆?/summary>
        public void Show(InteractionPromptData data)
        {
            gameObject.SetActive(true);
            if (promptText != null && data != null)
                promptText.text = UIPanelBase.LocalizedOrTitle(data.Key);
        }

        /// <summary>闅愯棌鎻愮ず銆?/summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>璁剧疆杩涘害锛?-1锛夈€?/summary>
        public void SetProgress(float t)
        {
            // 涓栫晫绌洪棿杩涘害浠ユ枃鏈櫨鍒嗘瘮灞曠ず锛堥樁娈典節鍙浛鎹负鐜舰 Image锛?            if (promptText != null)
            {
                var baseText = promptText.text;
                var idx = baseText.IndexOf(" (");
                if (idx > 0) baseText = baseText.Substring(0, idx);
                promptText.text = $"{baseText} ({Mathf.RoundToInt(Mathf.Clamp01(t) * 100)}%)";
            }
        }

        void SetCamTransform(Transform t) => camTransform = t;
    }
}

