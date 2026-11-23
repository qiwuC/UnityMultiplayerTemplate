using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>存档 IO 结果。</summary>
    public enum SaveIOResult
    {
        Success, ChecksumMismatch, Corrupted, NotFound, IOError, Busy
    }

    /// <summary>存档文件 IO：原子写入、SHA-256 校验、写入前备份、读取失败恢复、元数据独立存储。</summary>
    public static class SaveFileIO
    {
        /// <summary>备份保留份数。</summary>
        public static int BackupCount = 1;

        // ============ 原子写入 ============

        /// <summary>原子写入：先写临时文件，Flush，再替换目标文件。</summary>
        public static SaveIOResult WriteAtomic(string path, byte[] data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // 写入前备份旧文件
                BackupFile(path);

                var tmpPath = path + ".tmp";
                using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true); // fsync
                }
                // 原子替换
                if (File.Exists(path)) File.Replace(tmpPath, path, null);
                else File.Move(tmpPath, path);

                // 写校验文件
                WriteChecksum(path, ComputeChecksum(data));
                return SaveIOResult.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveFileIO] WriteAtomic 失败：{e.Message}");
                return SaveIOResult.IOError;
            }
        }

        // ============ 读取 + 校验 ============

        /// <summary>读取主文件并校验 SHA-256；失败尝试备份恢复。</summary>
        public static (byte[] data, SaveIOResult result) ReadWithChecksum(string path)
        {
            // 尝试主文件
            var (data, result) = TryReadWithChecksum(path);
            if (result == SaveIOResult.Success) return (data, result);

            // 尝试备份
            var bakPath = path + ".bak";
            if (File.Exists(bakPath))
            {
                Debug.LogWarning($"[SaveFileIO] 主文件损坏/缺失，尝试备份恢复：{bakPath}");
                var (bakData, bakResult) = TryReadWithChecksum(bakPath);
                if (bakResult == SaveIOResult.Success)
                {
                    // 恢复：备份覆盖主文件
                    File.Copy(bakPath, path, true);
                    var bakChecksum = bakPath + ".sha256";
                    if (File.Exists(bakChecksum)) File.Copy(bakChecksum, path + ".sha256", true);
                    Debug.Log($"[SaveFileIO] 已从备份恢复 {path}。");
                    return (bakData, SaveIOResult.Success);
                }
            }
            return (null, result);
        }

        static (byte[] data, SaveIOResult result) TryReadWithChecksum(string path)
        {
            try
            {
                if (!File.Exists(path)) return (null, SaveIOResult.NotFound);

                var data = File.ReadAllBytes(path);
                var checksumPath = path + ".sha256";

                if (File.Exists(checksumPath))
                {
                    var expected = File.ReadAllText(checksumPath).Trim();
                    var actual = ComputeChecksum(data);
                    if (expected != actual)
                    {
                        Debug.LogWarning($"[SaveFileIO] 校验和不匹配：{path}（期望={expected.Substring(0, 16)}… 实际={actual.Substring(0, 16)}…）");
                        return (data, SaveIOResult.ChecksumMismatch);
                    }
                }
                return (data, SaveIOResult.Success);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveFileIO] ReadWithChecksum 异常：{e.Message}");
                return (null, SaveIOResult.IOError);
            }
        }

        // ============ 元数据独立存储 ============

        public static SaveIOResult WriteMeta(string metaPath, string metaJson)
        {
            try
            {
                var dir = Path.GetDirectoryName(metaPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(metaPath, metaJson);
                return SaveIOResult.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveFileIO] WriteMeta 失败：{e.Message}");
                return SaveIOResult.IOError;
            }
        }

        public static (string metaJson, SaveIOResult result) ReadMeta(string metaPath)
        {
            try
            {
                if (!File.Exists(metaPath)) return (null, SaveIOResult.NotFound);
                return (File.ReadAllText(metaPath), SaveIOResult.Success);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveFileIO] ReadMeta 失败：{e.Message}");
                return (null, SaveIOResult.IOError);
            }
        }

        // ============ 删除 ============

        public static void DeleteAll(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".sha256")) File.Delete(path + ".sha256");
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".bak.sha256")) File.Delete(path + ".bak.sha256");
        }

        // ============ 内部 ============

        static void BackupFile(string path)
        {
            if (!File.Exists(path)) return;
            for (int i = BackupCount; i >= 1; i--)
            {
                var dst = i == 1 ? path + ".bak" : $"{path}.bak.{i - 1}";
                var src = i == 1 ? path : $"{path}.bak.{i - 1}";
                if (File.Exists(src))
                {
                    File.Copy(src, dst, true);
                    var srcChecksum = src + ".sha256";
                    if (File.Exists(srcChecksum)) File.Copy(srcChecksum, dst + ".sha256", true);
                }
            }
        }

        static void WriteChecksum(string path, string checksum)
        {
            File.WriteAllText(path + ".sha256", checksum);
        }

        static string ComputeChecksum(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLower();
        }
    }
}
