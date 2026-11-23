using System.IO;
using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>
    /// Game logger config (T8-08, ScriptableObject, optional).
    /// Defaults when no asset assigned: Info / write to file / 7-day retention / timestamp on / thread ID off / intercept Unity logs on.
    /// </summary>
    [CreateAssetMenu(fileName = "GameLoggerConfig", menuName = "Cqw/Systems/GameLoggerConfig")]
    public class GameLoggerConfig : ScriptableObject
    {
        [Tooltip("Minimum output level")]
        public GameLogLevel minLevel = GameLogLevel.Info;

        [Tooltip("Write to file (persistentDataPath/Logs/game_yyyyMMdd.log)")]
        public bool writeFile = true;

        [Tooltip("Log retention in days (cleans expired files on startup)")]
        public int retentionDays = 7;

        [Tooltip("Include timestamp in output")]
        public bool timestamp = true;

        [Tooltip("Include thread ID in output")]
        public bool threadId = false;

        [Tooltip("Intercept Unity Debug logs and persist to file")]
        public bool captureUnityLogs = true;
    }
}
