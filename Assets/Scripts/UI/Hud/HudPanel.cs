using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>HUD锛氱敓鍛?浣撳姏/闃熶紞/鑱婂ぉ鍏ュ彛绛夋彃妲斤紙鏁版嵁鏉ヨ嚜 PlayerStats/PlayerIdentity/TeamManager锛夈€?/summary>
    public class HudPanel : UIPanelBase
    {
        public override string PanelId => "ui.hud";

        TMP_Text healthText, staminaText, teamText;
        PlayerStats trackedStats;
        PlayerIdentity trackedIdentity;

        public HudMessageQueue MessageQueue { get; private set; }

        protected override void OnOpen()
        {
            if (healthText != null) return;
            Build();
            BindLocalPlayer();
        }

        protected override void OnClose() { }

        void BindLocalPlayer()
        {
            var lp = PlayerRegistry.GetLocalPlayer();
            if (lp == null) return;
            trackedIdentity = lp;
            trackedStats = lp.GetComponent<PlayerStats>();
            if (trackedStats != null)
            {
                trackedStats.OnHealthChanged += OnHealthChanged;
                trackedStats.OnStaminaChanged += OnStaminaChanged;
                UpdateHealthDisplay();
                UpdateStaminaDisplay();
            }
        }

        void OnHealthChanged(int newV, int oldV) => UpdateHealthDisplay();
        void OnStaminaChanged(float newV, float oldV) => UpdateStaminaDisplay();

        void UpdateHealthDisplay()
        {
            if (healthText != null && trackedStats != null)
                healthText.text = $"HP {trackedStats.Health}/{trackedStats.MaxHealth}";
        }

        void UpdateStaminaDisplay()
        {
            if (staminaText != null && trackedStats != null)
                staminaText.text = $"SP {trackedStats.Stamina:F0}/{trackedStats.MaxStamina:F0}";
        }

        public override void OnRefresh()
        {
            BindLocalPlayer();
            if (teamText != null && trackedIdentity != null)
                teamText.text = $"Team: {trackedIdentity.TeamId}";
        }

        void Build()
        {
            var healthLabel = UIBuilder.CreateText(Root, "HealthLabel", "", 26, TMPro.TextAlignmentOptions.Left,
                new Color(0.9f, 0.3f, 0.3f));
            healthLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.88f);
            healthLabel.rectTransform.anchorMax = new Vector2(0.25f, 0.95f);
            SetLocalizedText(healthLabel, "ui.hud.health");
            healthText = healthLabel;

            var staminaLabel = UIBuilder.CreateText(Root, "StaminaLabel", "", 26, TMPro.TextAlignmentOptions.Left,
                new Color(0.3f, 0.7f, 0.9f));
            staminaLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.8f);
            staminaLabel.rectTransform.anchorMax = new Vector2(0.25f, 0.87f);
            SetLocalizedText(staminaLabel, "ui.hud.stamina");
            staminaText = staminaLabel;

            var teamLabel = UIBuilder.CreateText(Root, "TeamLabel", "", 22, TMPro.TextAlignmentOptions.Left,
                new Color(0.8f, 0.8f, 0.5f));
            teamLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.73f);
            teamLabel.rectTransform.anchorMax = new Vector2(0.25f, 0.79f);
            SetLocalizedText(teamLabel, "ui.hud.team");
            teamText = teamLabel;

            var mqGo = new GameObject("MessageQueue", typeof(RectTransform));
            mqGo.transform.SetParent(Root, false);
            var mqText = UIBuilder.CreateText(mqGo.transform, "MQText", "", 22, TMPro.TextAlignmentOptions.TopLeft);
            mqText.rectTransform.anchorMin = new Vector2(0.3f, 0.35f);
            mqText.rectTransform.anchorMax = new Vector2(0.7f, 0.45f);
            var mq = mqGo.AddComponent<HudMessageQueue>();
            mq.DisplayText = mqText;
            MessageQueue = mq;
        }
    }
}

