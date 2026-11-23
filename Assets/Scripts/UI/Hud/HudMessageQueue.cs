using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>HUD 消息队列：依次展示，避免重叠。</summary>
    public class HudMessageQueue : MonoBehaviour
    {
        readonly Queue<string> messages = new Queue<string>();
        TMP_Text displayText;
        float displayTimer;
        public float displayDuration = 3f;
        public TMP_Text DisplayText { set => displayText = value; }

        public void Enqueue(string message) => messages.Enqueue(message);

        void Update()
        {
            if (displayText == null) return;
            if (messages.Count > 0 && displayTimer <= 0)
            {
                displayText.text = messages.Dequeue();
                displayTimer = displayDuration;
            }
            else if (displayTimer > 0)
            {
                displayTimer -= Time.deltaTime;
                if (displayTimer <= 0 && messages.Count > 0)
                {
                    displayText.text = messages.Dequeue();
                    displayTimer = displayDuration;
                }
                else if (displayTimer <= 0)
                    displayText.text = "";
            }
        }
    }
}
