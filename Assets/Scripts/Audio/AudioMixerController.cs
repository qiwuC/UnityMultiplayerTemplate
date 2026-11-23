using System;
using UnityEngine;
using UnityEngine.Audio;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Audio
{
    /// <summary>
    /// 音频混音器控制器：四通道音量管理（Master/Music/SFX/Voice）。
    /// 使用 AudioMixer.SetFloat/GetFloat 控制音量（参数需在 mixer 中暴露）；
    /// 无 AudioMixer 时回退为直接控制 AudioSource.volume（通过 VolumeChanged 事件）。
    /// 音量持久化经 IGameSettingsService。
    /// </summary>
    public class AudioMixerController : MonoBehaviour
    {
        public static AudioMixerController Instance { get; private set; }

        [Tooltip("AudioMixer 资产（可在编辑器中手动配置组与暴露参数；为空时走 AudioSource.volume 回退）")]
        public AudioMixer mixer;

        /// <summary>音量变更通知（AudioPool 等订阅以实时调整音量）。</summary>
        public event Action<string, float> OnVolumeChanged;

        // 通道名 → 暴露参数名
        static readonly string[] channelParams = { "MasterVolume", "MusicVolume", "SFXVolume", "VoiceVolume" };
        static readonly string[] settingKeys = { "audio.master", "audio.music", "audio.sfx", "audio.voice" };
        static readonly float[] defaultVolumes = { 1.0f, 0.8f, 1.0f, 1.0f };

        // 当前音量缓存（0-1）
        readonly float[] volumes = { 1f, 0.8f, 1f, 1f };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAndApply();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>启动时从 IGameSettingsService 读取音量并应用。</summary>
        public void LoadAndApply()
        {
            var s = GameSettingsServiceLocator.Service;
            for (int i = 0; i < 4; i++)
            {
                float v = s.GetFloat(settingKeys[i], defaultVolumes[i]);
                SetVolume(i, v, persist: false);
            }
        }

        // ============ 公开 API（按通道索引 0=Master 1=Music 2=SFX 3=Voice）============

        public float GetMasterVolume() => GetVolume(0);
        public float GetMusicVolume() => GetVolume(1);
        public float GetSFXVolume() => GetVolume(2);
        public float GetVoiceVolume() => GetVolume(3);

        public void SetMasterVolume(float v) => SetVolume(0, v);
        public void SetMusicVolume(float v) => SetVolume(1, v);
        public void SetSFXVolume(float v) => SetVolume(2, v);
        public void SetVoiceVolume(float v) => SetVolume(3, v);

        void SetVolume(int channel, float v, bool persist = true)
        {
            v = Mathf.Clamp01(v);
            volumes[channel] = v;

            // AudioMixer 路径（0-1 → dB）
            if (mixer != null)
            {
                float db = v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
                mixer.SetFloat(channelParams[channel], db);
            }

            // 持久化
            if (persist)
                GameSettingsServiceLocator.Service.SetFloat(settingKeys[channel], v);

            OnVolumeChanged?.Invoke(channelParams[channel], v);
        }

        float GetVolume(int channel) => volumes[channel];

        /// <summary>获取指定通道的 AudioMixerGroup（供 AudioPool 使用；需编辑器配置）。</summary>
        public AudioMixerGroup GetMixerGroup(int channel)
        {
            if (mixer == null) return null;
            var groups = mixer.FindMatchingGroups(channelParams[channel].Replace("Volume", ""));
            return groups.Length > 0 ? groups[0] : null;
        }

        /// <summary>获取当前音量（按通道名）。</summary>
        public float GetVolumeByName(string paramName)
        {
            for (int i = 0; i < channelParams.Length; i++)
                if (channelParams[i] == paramName) return volumes[i];
            return 1f;
        }
    }
}
