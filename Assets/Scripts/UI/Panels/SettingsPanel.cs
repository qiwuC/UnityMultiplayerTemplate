using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cqw.MultilayerFramework.Network;
using Cqw.MultilayerFramework.Localization;

namespace Cqw.MultilayerFramework.UI
{
    public class SettingsPanel : UIPanelBase
    {
        public override string PanelId => "ui.settings";
        public override bool IsModal => true;

        IGameSettingsService Settings => GameSettingsServiceLocator.Service;

        TMP_Dropdown ddResolution, ddFullscreen, ddQuality, ddLanguage;
        Toggle tgVSync, tgAutoSave, tgShowNickname;
        Slider slMaster, slMusic, slSfx, slVoice, slMouseSens, slPadSens, slAutoSaveInterval;

        protected override void OnOpen() { if (ddResolution == null) Build(); LoadFromService(); }
        protected override void OnClose() { }

        void Build()
        {
            UIBuilder.CreateImage(Root, "BG", new Color(0.08f, 0.09f, 0.13f, 0.97f));
            var title = UIBuilder.CreateText(Root, "Title", "", 36, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            title.rectTransform.anchorMin = new Vector2(0.1f, 0.88f); title.rectTransform.anchorMax = new Vector2(0.9f, 0.96f);
            SetLocalizedText(title, "ui.settings.title");

            var scroll = UIBuilder.CreateScrollView(Root, "Scroll", out var content);
            var srt = scroll.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.08f, 0.14f); srt.anchorMax = new Vector2(0.92f, 0.86f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(20, 20, 10, 10);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            Section(content, "ui.settings.video");
            ddResolution = AddDropdown(content, "ui.settings.resolution", new[] { "1920x1080", "2560x1440", "3840x2160" });
            ddFullscreen = AddDropdown(content, "ui.settings.fullscreen", new[] { "Fullscreen", "Windowed", "Borderless" });
            tgVSync = AddToggle(content, "ui.settings.vsync");
            ddQuality = AddDropdown(content, "ui.settings.quality", new[] { "Low", "Medium", "High", "Ultra" });

            Section(content, "ui.settings.audio");
            slMaster = AddSlider(content, "ui.settings.master");
            slMusic = AddSlider(content, "ui.settings.music");
            slSfx = AddSlider(content, "ui.settings.sfx");
            slVoice = AddSlider(content, "ui.settings.voice");

            Section(content, "ui.settings.controls");
            var rebind = UIBuilder.CreateButton(content, "Btn_Rebind", "ui.settings.keybinds", out _, new Vector2(0, 40));
            rebind.onClick.AddListener(() => UIManager.Instance?.OpenPanel("ui.keybinds"));
            slMouseSens = AddSlider(content, "ui.settings.mouseSensitivity");
            slPadSens = AddSlider(content, "ui.settings.gamepadSensitivity");

            Section(content, "ui.settings.game");
            ddLanguage = AddDropdown(content, "ui.settings.language",
                LanguageRegistry.GetAll().Select(l => l.LocalName).ToArray());
            ddLanguage.onValueChanged.AddListener(idx =>
            {
                var code = LanguageRegistry.GetAll()[idx].Code;
                LocalizationService.SetLanguage(code);
            });
            tgShowNickname = AddToggle(content, "ui.settings.showNickname");

            Section(content, "ui.settings.autosave");
            tgAutoSave = AddToggle(content, "ui.settings.autosaveEnabled");
            slAutoSaveInterval = AddSlider(content, "ui.settings.autosaveInterval");
            slAutoSaveInterval.minValue = 1; slAutoSaveInterval.maxValue = 15;

            var btnArea = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            btnArea.SetParent(Root, false);
            btnArea.anchorMin = new Vector2(0.1f, 0.03f); btnArea.anchorMax = new Vector2(0.9f, 0.12f);
            var hlg = btnArea.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            var applyBtn = UIBuilder.CreateButton(btnArea, "Btn_Apply", "ui.settings.apply", out _, new Vector2(0, 50));
            var cancelBtn = UIBuilder.CreateButton(btnArea, "Btn_Cancel", "ui.settings.cancel", out _, new Vector2(0, 50));
            var revertBtn = UIBuilder.CreateButton(btnArea, "Btn_Revert", "ui.settings.revert", out _, new Vector2(0, 50));
            applyBtn.onClick.AddListener(Apply);
            cancelBtn.onClick.AddListener(Cancel);
            revertBtn.onClick.AddListener(Revert);
        }

        void Section(RectTransform content, string key)
        {
            var txt = UIBuilder.CreateText(content, "Sec_" + key, "", 28, TMPro.TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.3f));
            SetLocalizedText(txt, key);
            txt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
        }

        TMP_Dropdown AddDropdown(RectTransform c, string key, string[] opts)
        { AddLabel(c, key); var d = UIBuilder.CreateDropdown(c, "DD_" + key, new Vector2(0, 36), opts); d.gameObject.AddComponent<LayoutElement>().preferredHeight = 36; return d; }
        Toggle AddToggle(RectTransform c, string key)
        { var t = UIBuilder.CreateToggle(c, "TG_" + key, new Vector2(0, 34), out var l); SetLocalizedText(l, key); t.gameObject.AddComponent<LayoutElement>().preferredHeight = 34; return t; }
        Slider AddSlider(RectTransform c, string key)
        { AddLabel(c, key); var s = UIBuilder.CreateSlider(c, "SL_" + key, new Vector2(0, 28)); s.gameObject.AddComponent<LayoutElement>().preferredHeight = 28; return s; }
        void AddLabel(RectTransform c, string key)
        { var t = UIBuilder.CreateText(c, "L_" + key + "_" + c.childCount, "", 22); SetLocalizedText(t, key); t.gameObject.AddComponent<LayoutElement>().preferredHeight = 30; }

        void LoadFromService()
        {
            var s = Settings;
            tgVSync.isOn = s.GetBool("settings.vsync", true);
            ddQuality.value = Mathf.Clamp(s.GetInt("settings.quality", 2), 0, ddQuality.options.Count - 1);
            slMaster.value = s.GetFloat("settings.master", 1f);
            slMusic.value = s.GetFloat("settings.music", 0.8f);
            slSfx.value = s.GetFloat("settings.sfx", 0.8f);
            slVoice.value = s.GetFloat("settings.voice", 1f);
            slMouseSens.value = s.GetFloat("settings.mouseSens", 0.12f);
            slPadSens.value = s.GetFloat("settings.padSens", 120f);
            tgShowNickname.isOn = s.GetBool("settings.showNickname", true);
            tgAutoSave.isOn = s.GetBool("settings.autoSave", true);
            slAutoSaveInterval.value = s.GetFloat("settings.autoSaveInterval", 5f);
            ddLanguage.value = Mathf.Clamp(LanguageRegistry.GetAll().ToList().FindIndex(l => l.Code == LocalizationService.CurrentLanguage), 0, ddLanguage.options.Count - 1);
        }

        void Apply()
        {
            var s = Settings;
            s.SetBool("settings.vsync", tgVSync.isOn);
            s.SetInt("settings.quality", ddQuality.value);
            s.SetFloat("settings.master", slMaster.value);
            s.SetFloat("settings.music", slMusic.value);
            s.SetFloat("settings.sfx", slSfx.value);
            s.SetFloat("settings.voice", slVoice.value);
            s.SetFloat("settings.mouseSens", slMouseSens.value);
            s.SetFloat("settings.padSens", slPadSens.value);
            s.SetBool("settings.showNickname", tgShowNickname.isOn);
            s.SetBool("settings.autoSave", tgAutoSave.isOn);
            s.SetFloat("settings.autoSaveInterval", slAutoSaveInterval.value);
            s.Save();
            ToastManager.Instance?.Show(ToastLevel.Success, "ui.toast.settingsApplied");
        }

        void Cancel() { LoadFromService(); ToastManager.Instance?.Show(ToastLevel.Info, "ui.toast.settingsCancelled"); }
        void Revert()
        {
            tgVSync.isOn = true; ddQuality.value = 2;
            slMaster.value = 1f; slMusic.value = 0.8f; slSfx.value = 0.8f; slVoice.value = 1f;
            slMouseSens.value = 0.12f; slPadSens.value = 120f;
            tgShowNickname.isOn = true; tgAutoSave.isOn = true; slAutoSaveInterval.value = 5f;
            LocalizationService.SetLanguage("en");
            ToastManager.Instance?.Show(ToastLevel.Info, "ui.toast.settingsReverted");
        }
    }
}
