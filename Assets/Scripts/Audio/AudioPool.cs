using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Audio
{
    /// <summary>音频通道枚举（对应 AudioMixer 组）。</summary>
    public enum AudioChannel { Music = 1, SFX = 2, Voice = 3 }

    /// <summary>播放参数。</summary>
    public struct AudioPlayParams
    {
        public float Volume;
        public float Pitch;
        public bool Loop;
        public int Priority;         // 0=最高 128=最低（Unity AudioSource.priority）
        public float SpatialBlend;   // 0=2D 1=3D
        public AudioChannel Channel;
        public Vector3 Position;     // 3D 时
        public float MinDistance;
        public float MaxDistance;
        public AudioRolloffMode RolloffMode;
        public Transform FollowTarget; // 跟随目标（可选）

        public static AudioPlayParams Default(AudioChannel channel = AudioChannel.SFX) => new AudioPlayParams
        {
            Volume = 1f, Pitch = 1f, Loop = false, Priority = 128,
            SpatialBlend = 0f, Channel = channel, Position = Vector3.zero,
            MinDistance = 1f, MaxDistance = 500f, RolloffMode = AudioRolloffMode.Logarithmic
        };
    }

    /// <summary>播放句柄：控制单个正在播放的音频。</summary>
    public class AudioHandle
    {
        public AudioSource Source { get; internal set; }
        public bool IsValid => Source != null && Source.gameObject != null;
        public bool IsPlaying => IsValid && Source.isPlaying;
        public Transform FollowTarget { get; set; }

        public void Stop() { if (IsValid) { Source.Stop(); Source.gameObject.SetActive(false); } }
        public void SetVolume(float v) { if (IsValid) Source.volume = v; }
        public void SetPosition(Vector3 pos) { if (IsValid) Source.transform.position = pos; }
        public void Pause() { if (IsValid) Source.Pause(); }
        public void UnPause() { if (IsValid) Source.UnPause(); }
    }

    /// <summary>
    /// 音频池：复用 AudioSource，优先级淘汰。
    /// 每池对象绑定一个通道（Music/SFX/Voice），AudioSource 强制指定 mixerGroup（若可用）。
    /// </summary>
    [AddComponentMenu("Cqw/Audio/AudioPool")]
    public class AudioPool : MonoBehaviour
    {
        public static AudioPool Instance { get; private set; }

        [Tooltip("池大小")]
        public int poolSize = 16;

        readonly List<PoolEntry> pool = new List<PoolEntry>();
        Transform poolRoot;

        // 统计
        public int ActiveCount { get; private set; }
        public int PooledCount { get; private set; }
        public int OverflowCount { get; private set; }

        class PoolEntry
        {
            public AudioSource Source;
            public AudioHandle Handle;
            public int Priority;
            public float StartTime;
            public bool Active;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            poolRoot = transform;

            var mixerCtrl = GetComponent<AudioMixerController>();
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"AudioSource_{i}");
                go.transform.SetParent(poolRoot, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                // 绑定 mixer group（SFX 通道作为默认；Music/Voice 由 BgmPlayer/语音系统指定）
                if (mixerCtrl != null)
                {
                    var group = mixerCtrl.GetMixerGroup((int)AudioChannel.SFX);
                    if (group != null) src.outputAudioMixerGroup = group;
                }
                go.SetActive(false);
                pool.Add(new PoolEntry { Source = src, Handle = new AudioHandle { Source = src }, Priority = 128, Active = false });
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>播放音频（从池中取源；池耗尽时按优先级淘汰）。</summary>
        public AudioHandle Play(AudioClip clip, AudioPlayParams p)
        {
            if (clip == null) return null;

            var entry = FindAvailable(p);
            if (entry == null) { OverflowCount++; return null; }

            entry.Active = true;
            entry.Priority = p.Priority;
            entry.StartTime = Time.realtimeSinceStartup;
            entry.Handle.FollowTarget = p.FollowTarget;

            var src = entry.Source;
            src.gameObject.SetActive(true);
            src.clip = clip;
            src.volume = p.Volume;
            src.pitch = p.Pitch;
            src.loop = p.Loop;
            src.priority = p.Priority;
            src.spatialBlend = p.SpatialBlend;
            src.minDistance = p.MinDistance;
            src.maxDistance = p.MaxDistance;
            src.rolloffMode = p.RolloffMode;

            // mixer group 按通道
            var mixerCtrl = GetComponent<AudioMixerController>();
            if (mixerCtrl != null)
            {
                var group = mixerCtrl.GetMixerGroup((int)p.Channel);
                if (group != null) src.outputAudioMixerGroup = group;
            }

            if (p.SpatialBlend > 0.01f)
            {
                src.transform.position = p.Position;
            }

            src.Play();

            // 跟随目标
            if (p.FollowTarget != null)
                StartCoroutine(FollowRoutine(entry.Handle, p.FollowTarget));

            UpdateStats();
            return entry.Handle;
        }

        /// <summary>3D 便捷接口：在指定位置播放。</summary>
        public AudioHandle PlayAt(AudioClip clip, Vector3 position, AudioPlayParams p)
        {
            p.SpatialBlend = 1f;
            p.Position = position;
            return Play(clip, p);
        }

        AudioHandle PlayBgmInternal(AudioClip clip, float volume)
        {
            var p = AudioPlayParams.Default(AudioChannel.Music);
            p.Volume = volume; p.Loop = true;
            return Play(clip, p);
        }

        System.Collections.IEnumerator FollowRoutine(AudioHandle handle, Transform target)
        {
            while (handle != null && handle.IsValid && handle.IsPlaying && handle.FollowTarget != null)
            {
                handle.SetPosition(handle.FollowTarget.position);
                yield return null;
            }
        }

        PoolEntry FindAvailable(AudioPlayParams p)
        {
            PoolEntry best = null;
            foreach (var e in pool)
            {
                if (!e.Active)
                {
                    // 找到空闲源
                    if (best == null || !best.Active) { best = e; break; }
                }
            }

            if (best == null)
            {
                // 池耗尽 → 优先级淘汰：低优先级且最早播放的替换
                PoolEntry lowest = null;
                foreach (var e in pool)
                {
                    if (!e.Active) continue;
                    if (e.Priority > p.Priority || (e.Priority == p.Priority && (lowest == null || e.StartTime < lowest.StartTime)))
                    {
                        if (lowest == null || e.Priority > lowest.Priority || (e.Priority == lowest.Priority && e.StartTime < lowest.StartTime))
                            lowest = e;
                    }
                }
                if (lowest != null)
                {
                    lowest.Handle.Stop();
                    lowest.Active = false;
                    best = lowest;
                }
            }
            return best;
        }

        void Update()
        {
            // 回收已结束的源
            foreach (var e in pool)
            {
                if (e.Active && !e.Source.isPlaying && !e.Source.loop)
                {
                    e.Active = false;
                    e.Source.gameObject.SetActive(false);
                }
            }
            UpdateStats();
        }

        void UpdateStats()
        {
            int active = 0, pooled = 0;
            foreach (var e in pool)
            {
                if (e.Active) active++;
                else pooled++;
            }
            ActiveCount = active;
            PooledCount = pooled;
        }

        public string Describe() => $"active={ActiveCount} pooled={PooledCount} overflow={OverflowCount} size={pool.Count}";
    }
}
