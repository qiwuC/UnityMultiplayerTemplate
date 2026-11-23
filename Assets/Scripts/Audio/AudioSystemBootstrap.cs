using UnityEngine;
using Cqw.MultilayerFramework.UI;

namespace Cqw.MultilayerFramework.Audio
{
    /// <summary>
    /// 音频系统引导器：初始化 AudioMixerController、AudioPool、BgmPlayer、AutoSaveScheduler。
    /// 启动时从 IGameSettingsService 读取音量并应用；SettingsPanel 音量变更经 AudioMixerController。
    /// </summary>
    [AddComponentMenu("Cqw/Audio/AudioSystemBootstrap")]
    public class AudioSystemBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // 确保组件存在
            var mixerCtrl = GetComponent<AudioMixerController>();
            if (mixerCtrl == null) mixerCtrl = gameObject.AddComponent<AudioMixerController>();

            var pool = GetComponent<AudioPool>();
            if (pool == null) pool = gameObject.AddComponent<AudioPool>();

            var bgm = GetComponent<BgmPlayer>();
            if (bgm == null) bgm = gameObject.AddComponent<BgmPlayer>();

            // 订阅音量变更（实时应用到 AudioPool 中的源）
            mixerCtrl.OnVolumeChanged += (param, vol) =>
            {
                // AudioPool 中的源音量已由 mixer 或 source.volume 控制
                // BGM 音量跟随 Music 通道
                if (param == "MusicVolume" && BgmPlayer.Instance != null)
                    BgmPlayer.Instance.SetVolume(vol);
            };

            Debug.Log("[AudioSystem] 音频系统初始化完成（MixerController + Pool + BgmPlayer）。");
        }
    }
}
