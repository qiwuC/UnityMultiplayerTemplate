using UnityEngine;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>示例元数据 Provider：保存 SavedAtUtc、SceneName、PlayersCount。</summary>
    public class SaveMetaProvider : ISaveDataProvider
    {
        public string ProviderId => "meta";
        public int ProviderVersion => 1;

        public string SceneName = "Unknown";
        public int PlayersCount;

        public object CaptureState() => new
        {
            sceneName = SceneName,
            playersCount = PlayersCount,
            savedAt = SaveSerializer.UtcTimestamp()
        };

        public void RestoreState(Newtonsoft.Json.Linq.JToken data)
        {
            SceneName = data.Value<string>("sceneName") ?? "Unknown";
            PlayersCount = data.Value<int?>("playersCount") ?? 0;
        }
    }
}
