using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>Log level enum.</summary>
    public enum GameLogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Off = 4
    }

    /// <summary>
    /// Game logger (T8-08): Leveled output + Unity Console + file persistence (background thread, non-blocking main thread) + retention cleanup.
    /// File: Application.persistentDataPath/Logs/game_yyyyMMdd.log; default retention 7 days.
    /// Console output via Debug.unityLogger (Editor includes stack trace; DevBuild keeps ScriptOnly stack);
    /// File output via Application.logMessageReceived interception (toggleable), or direct enqueue (when interception is off).
    /// </summary>
    public static class GameLogger
    {
        const int FlushLineCount = 32;

        static readonly BlockingCollection<string> queue = new BlockingCollection<string>(new ConcurrentQueue<string>());
        static Thread writerThread;
        static GameLogLevel minLevel = GameLogLevel.Info;
        static bool writeFile = true;
        static bool timestampEnabled = true;
        static bool threadIdEnabled = false;
        static bool interceptUnityLogs = true;
        static bool initialized;
        static string logDir;

        /// <summary>Current minimum level (mutable at runtime).</summary>
        public static GameLogLevel Level
        {
            get => minLevel;
            set
            {
                minLevel = value;
                Debug.Log($"[GameLogger] level -> {value}");
            }
        }

        /// <summary>Log directory (valid after initialization).</summary>
        public static string LogDirectory => logDir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit() => Initialize(null);

        /// <summary>Initialize (idempotent; null config uses defaults).</summary>
        public static void Initialize(GameLoggerConfig config = null)
        {
            if (initialized) return;
            initialized = true;

            if (config != null)
            {
                minLevel = config.minLevel;
                writeFile = config.writeFile;
                timestampEnabled = config.timestamp;
                threadIdEnabled = config.threadId;
                interceptUnityLogs = config.captureUnityLogs;
            }

            logDir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(logDir);
            CleanOldLogs(config != null ? config.retentionDays : 7);

            // DevBuild keeps script-only stack traces (Editor already includes them by default)
            if (Debug.isDebugBuild)
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);

            if (writeFile)
            {
                writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "GameLogger" };
                writerThread.Start();
                if (interceptUnityLogs)
                    Application.logMessageReceived += OnUnityLogReceived;
            }

            Application.quitting += Shutdown;
            Debug.Log($"[GameLogger] initialized level={minLevel} file={writeFile} dir={logDir}");
        }

        /// <summary>Stops the writer thread on quit (Application.quitting).</summary>
        public static void Shutdown()
        {
            if (!initialized) return;
            try
            {
                queue.CompleteAdding();
                writerThread?.Join(2000);
            }
            catch { /* best effort during quit */ }
        }

        // ============ Public API ============

        public static void LogDebug(string message) => Write(GameLogLevel.Debug, message, null);
        public static void LogInfo(string message) => Write(GameLogLevel.Info, message, null);
        public static void LogWarning(string message) => Write(GameLogLevel.Warning, message, null);
        public static void LogError(string message) => Write(GameLogLevel.Error, message, null);

        public static void LogException(Exception exception)
        {
            if (exception == null) return;
            if (interceptUnityLogs && writeFile)
            {
                Debug.LogException(exception); // interception callback includes stack trace
            }
            else
            {
                Debug.LogException(exception);
                Enqueue(Format(GameLogLevel.Error, exception.Message, exception.ToString()));
            }
        }

        // ============ Internal ============

        static void Write(GameLogLevel level, string message, string stackTrace)
        {
            if (level < minLevel || level == GameLogLevel.Off) return;

            switch (level)
            {
                case GameLogLevel.Warning: Debug.unityLogger.Log(LogType.Warning, message); break;
                case GameLogLevel.Error: Debug.unityLogger.Log(LogType.Error, message); break;
                default: Debug.unityLogger.Log(LogType.Log, message); break;
            }

            // Intercept mode: OnUnityLogReceived persists to file (avoids double write); otherwise enqueue directly
            if (!writeFile) return;
            if (!interceptUnityLogs) Enqueue(Format(level, message, stackTrace));
        }

        static void OnUnityLogReceived(string condition, string stackTrace, LogType type)
        {
            // Note: does not call back into Debug API, no recursion risk
            var level = type switch
            {
                LogType.Error or LogType.Assert or LogType.Exception => GameLogLevel.Error,
                LogType.Warning => GameLogLevel.Warning,
                _ => GameLogLevel.Info,
            };
            if (level < minLevel) return;

            // Error/Exception include stack trace; regular logs also include in Editor
            bool withStack = type is LogType.Error or LogType.Assert or LogType.Exception
                             || (Application.isEditor && !string.IsNullOrEmpty(stackTrace));
            Enqueue(Format(level, condition, withStack ? stackTrace : null));
        }

        static string Format(GameLogLevel level, string message, string stack)
        {
            var sb = new StringBuilder(96);
            if (timestampEnabled)
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ');
            else
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd")).Append(' ');
            if (threadIdEnabled)
                sb.Append("[T").Append(Thread.CurrentThread.ManagedThreadId).Append("] ");
            sb.Append(level switch
            {
                GameLogLevel.Debug => "DEBUG",
                GameLogLevel.Info => "INFO",
                GameLogLevel.Warning => "WARN",
                GameLogLevel.Error => "ERROR",
                _ => "LOG",
            }).Append(' ').Append(message);
            if (!string.IsNullOrEmpty(stack))
                sb.Append('\n').Append(stack);
            return sb.ToString();
        }

        static void Enqueue(string line)
        {
            if (queue.IsAddingCompleted) return;
            try { queue.TryAdd(line); } catch { /* ignore after completion */ }
        }

        static void WriterLoop()
        {
            var buffer = new List<string>(FlushLineCount);
            var lastFlush = DateTime.UtcNow;
            while (!queue.IsCompleted)
            {
                if (queue.TryTake(out var line, 250))
                    buffer.Add(line);
                if (buffer.Count >= FlushLineCount || (buffer.Count > 0 && (DateTime.UtcNow - lastFlush).TotalMilliseconds > 500))
                    FlushBuffer(buffer, ref lastFlush);
            }
            while (queue.TryTake(out var line))
                buffer.Add(line);
            FlushBuffer(buffer, ref lastFlush);
        }

        static void FlushBuffer(List<string> buffer, ref DateTime lastFlush)
        {
            if (buffer.Count == 0) { lastFlush = DateTime.UtcNow; return; }
            try
            {
                // Group by day (line starts with date when timestamp enabled; otherwise use current date)
                var byDay = new Dictionary<string, StringBuilder>();
                foreach (var line in buffer)
                {
                    var day = timestampEnabled && line.Length >= 10
                        ? line.Substring(0, 10)
                        : DateTime.Now.ToString("yyyy-MM-dd");
                    if (!byDay.TryGetValue(day, out var sb))
                    {
                        sb = new StringBuilder();
                        byDay[day] = sb;
                    }
                    sb.AppendLine(line);
                }
                foreach (var kv in byDay)
                    File.AppendAllText(Path.Combine(logDir, $"game_{kv.Key.Replace("-", "")}.log"), kv.Value.ToString(), Encoding.UTF8);
                buffer.Clear();
            }
            catch
            {
                // IO exceptions (file locked etc.): discard batch to prevent background thread infinite loop
                buffer.Clear();
            }
            finally
            {
                lastFlush = DateTime.UtcNow;
            }
        }

        static void CleanOldLogs(int retentionDays)
        {
            try
            {
                if (retentionDays <= 0) return;
                var cutoff = DateTime.Now.AddDays(-retentionDays);
                foreach (var file in Directory.GetFiles(logDir, "game_*.log"))
                {
                    var name = Path.GetFileNameWithoutExtension(file); // game_20260927
                    if (name.Length < 12) continue;
                    if (!DateTime.TryParseExact(name.Substring(5, 8), "yyyyMMdd", null,
                            System.Globalization.DateTimeStyles.None, out var day)) continue;
                    if (day < cutoff)
                    {
                        File.Delete(file);
                        Debug.Log($"[GameLogger] Cleaned expired log: {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameLogger] Failed to clean expired logs: {e.Message}");
            }
        }
    }
}
