using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>Newtonsoft Json 序列化器（存档专用配置）。</summary>
    public static class SaveSerializer
    {
        static readonly JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            DateFormatString = "o",
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Error = (sender, args) =>
            {
                Debug.LogError($"[SaveSerializer] {args.ErrorContext.Error.Message}");
                args.ErrorContext.Handled = true;
            }
        };

        public static string Serialize(SaveDataRoot root)
        {
            return JsonConvert.SerializeObject(root, settings);
        }

        public static SaveDataRoot Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<SaveDataRoot>(json, settings);
        }

        public static SaveDataRoot Deserialize(byte[] bytes)
        {
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return Deserialize(json);
        }

        /// <summary>序列化单个 provider 的 state 为 JSON 字符串。</summary>
        public static string SerializeProvider(object state)
        {
            return JsonConvert.SerializeObject(state, settings);
        }

        /// <summary>反序列化 provider 的 JSON 为 JToken（供 RestoreState 使用）。</summary>
        public static JToken DeserializeProvider(string json)
        {
            return JToken.Parse(json);
        }

        /// <summary>生成 UTC ISO 8601 时间戳。</summary>
        public static string UtcTimestamp() => DateTime.UtcNow.ToString("o");

        /// <summary>生成唯一存档 ID。</summary>
        public static string GenerateUniqueId() => Guid.NewGuid().ToString("N");
    }
}
