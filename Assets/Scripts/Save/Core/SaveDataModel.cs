using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>当前存档 Schema 版本。每次破坏性格式变更递增。</summary>
    public static class SaveSchemaVersion
    {
        public const int Current = 1;
    }

    /// <summary>存档数据根对象：所有 provider 的状态汇总。</summary>
    [Serializable]
    public class SaveDataRoot
    {
        public int SchemaVersion = SaveSchemaVersion.Current;
        public string GameVersion = Application.version;
        public string SavedAtUtc = DateTime.UtcNow.ToString("o");
        /// <summary>providerId → 该 provider 序列化后的 JSON 状态。</summary>
        public Dictionary<string, string> Providers = new Dictionary<string, string>();
    }

    /// <summary>存档数据提供者接口：各系统注册要保存的数据。</summary>
    public interface ISaveDataProvider
    {
        string ProviderId { get; }
        int ProviderVersion { get; }
        object CaptureState();
        void RestoreState(Newtonsoft.Json.Linq.JToken data);
        /// <summary>provider 自身迁移钩子（可选；返回 true=迁移成功，false=失败）。</summary>
        bool Migrate(int fromVersion, Newtonsoft.Json.Linq.JObject data) => true;
    }

    /// <summary>Provider 注册表：注册/注销/枚举。</summary>
    public static class SaveDataProviderRegistry
    {
        static readonly List<ISaveDataProvider> providers = new List<ISaveDataProvider>();
        static readonly object lockObj = new object();

        public static void Register(ISaveDataProvider provider)
        {
            if (provider == null) return;
            lock (lockObj) { if (!providers.Contains(provider)) providers.Add(provider); }
        }

        public static void Unregister(ISaveDataProvider provider)
        {
            if (provider == null) return;
            lock (lockObj) providers.Remove(provider);
        }

        public static List<ISaveDataProvider> GetAll()
        {
            lock (lockObj) return new List<ISaveDataProvider>(providers);
        }

        public static void Clear() { lock (lockObj) providers.Clear(); }
    }
}
