using System;
using System.Collections;
using UnityEngine;

namespace Cqw.MultilayerFramework.Audio
{
    /// <summary>
    /// BGM 播放器：淡入淡出平滑过渡，CrossFade，Stop(fadeOut)。
    /// 使用独立 AudioSource（Music 通道），淡入淡出基于 unscaledDeltaTime（不受 timeScale 影响）。
    /// </summary>
    [AddComponentMenu("Cqw/Audio/BgmPlayer")]
    public class BgmPlayer : MonoBehaviour
    {
        public static BgmPlayer Instance { get; private set; }

        AudioSource source;
        Coroutine fadeRoutine;

        /// <summary>当前播放的 AudioClip（无则 null）。</summary>
        public AudioClip CurrentClip { get; private set; }
        public bool IsPlaying { get; private set; }
        /// <summary>当前 BGM 音量（0-1，不含 mixer 缩放）。</summary>
        public float CurrentVolume { get; private set; } = 1f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var go = new GameObject("BgmSource");
            go.transform.SetParent(transform, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;

            var mixerCtrl = GetComponent<AudioMixerController>();
            if (mixerCtrl != null)
            {
                var group = mixerCtrl.GetMixerGroup(1); // Music 通道
                if (group != null) source.outputAudioMixerGroup = group;
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>播放 BGM（淡入）。</summary>
        public void Play(AudioClip clip, float fadeInSec = 1f, bool loop = true)
        {
            if (clip == null) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            source.clip = clip;
            source.loop = loop;
            source.volume = 0f;
            source.Play();
            CurrentClip = clip;
            IsPlaying = true;
            fadeRoutine = StartCoroutine(FadeRoutine(0f, CurrentVolume, fadeInSec));
        }

        /// <summary>交叉淡入淡出：当前 BGM 淡出，新 BGM 淡入。</summary>
        public void CrossFade(AudioClip nextClip, float fadeSec = 1f)
        {
            if (nextClip == null) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);

            var targetVolume = CurrentVolume;
            if (CurrentClip == nextClip && IsPlaying) return; // 同一曲不重复

            if (IsPlaying && source.clip != null)
            {
                // 双 AudioSource 交叉过渡
                StartCoroutine(CrossFadeRoutine(nextClip, targetVolume, fadeSec));
            }
            else
            {
                Play(nextClip, fadeSec);
            }
        }

        IEnumerator CrossFadeRoutine(AudioClip nextClip, float targetVol, float fadeSec)
        {
            // 新源（淡入）
            var newGo = new GameObject("BgmSource_Next");
            newGo.transform.SetParent(transform, false);
            var newSrc = newGo.AddComponent<AudioSource>();
            newSrc.clip = nextClip;
            newSrc.loop = true;
            newSrc.volume = 0f;
            var mixerCtrl = GetComponent<AudioMixerController>();
            if (mixerCtrl != null)
            {
                var group = mixerCtrl.GetMixerGroup(1);
                if (group != null) newSrc.outputAudioMixerGroup = group;
            }
            newSrc.Play();

            // 旧源淡出 + 新源淡入
            float t = 0;
            while (t < fadeSec)
            {
                t += Time.unscaledDeltaTime;
                float alpha = t / fadeSec;
                source.volume = Mathf.Lerp(targetVol, 0f, alpha);
                newSrc.volume = Mathf.Lerp(0f, targetVol, alpha);
                yield return null;
            }

            // 清理旧源
            source.Stop();
            source.clip = nextClip;
            var tmp = source;
            source = newSrc;
            Destroy(newGo);
            CurrentClip = nextClip;
        }

        /// <summary>停止 BGM（淡出）。</summary>
        public void Stop(float fadeOutSec = 1f)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(CurrentVolume, 0f, fadeOutSec, stopOnComplete: true));
        }

        IEnumerator FadeRoutine(float from, float to, float duration, bool stopOnComplete = false)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // 不受 timeScale 影响
                source.volume = Mathf.Lerp(from, to, t / Mathf.Max(duration, 0.01f));
                yield return null;
            }
            source.volume = to;
            if (stopOnComplete)
            {
                source.Stop();
                IsPlaying = false;
                CurrentClip = null;
            }
        }

        /// <summary>设置 BGM 基础音量（0-1）。</summary>
        public void SetVolume(float v)
        {
            CurrentVolume = Mathf.Clamp01(v);
            if (IsPlaying && source != null) source.volume = CurrentVolume;
        }
    }
}
