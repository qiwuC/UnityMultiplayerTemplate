using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Save
{
    /// <summary>迁移结果。</summary>
    public enum MigrationResult
    {
        NotNeeded,      // 版本已最新
        Success,        // 迁移成功
        FutureVersion,  // 高版本拒绝
        Failed          // 迁移失败
    }

    /// <summary>存档迁移服务：版本校验、逐级迁移、失败不破坏原文件。</summary>
    public static class SaveMigrationService
    {
        static readonly Dictionary<int, Func<SaveDataRoot, bool>> migrationSteps = new Dictionary<int, Func<SaveDataRoot, bool>>();

        static SaveMigrationService()
        {
            // 预留迁移示例（空操作但结构存在）
            migrationSteps[1] = Migrate_1_To_2;
            migrationSteps[2] = Migrate_2_To_3;
        }

        /// <summary>当前 Schema 版本。</summary>
        public static int CurrentVersion => SaveSchemaVersion.Current;

        /// <summary>文件版本是否可加载（高版本拒绝）。</summary>
        public static bool CanLoad(int fileVersion) => fileVersion <= CurrentVersion;

        /// <summary>执行迁移（内存中逐级调用，成功后由调用方落盘）。</summary>
        public static MigrationResult Migrate(SaveDataRoot root)
        {
            if (root == null) return MigrationResult.Failed;
            if (root.SchemaVersion > CurrentVersion)
            {
                Debug.LogError($"[Migration] 存档版本 {root.SchemaVersion} 高于当前版本 {CurrentVersion}，拒绝加载。");
                return MigrationResult.FutureVersion;
            }

            while (root.SchemaVersion < CurrentVersion)
            {
                int from = root.SchemaVersion;
                if (!migrationSteps.TryGetValue(from, out var step))
                {
                    Debug.LogWarning($"[Migration] 无迁移步骤 {from}→{from + 1}，跳到最新。");
                    root.SchemaVersion = CurrentVersion;
                    continue;
                }

                try
                {
                    if (!step(root))
                    {
                        Debug.LogError($"[Migration] 迁移 {from}→{from + 1} 失败。原数据未损坏。");
                        return MigrationResult.Failed;
                    }
                    root.SchemaVersion = from + 1;
                    Debug.Log($"[Migration] 已迁移 {from}→{from + 1}。");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Migration] 迁移 {from}→{from + 1} 异常：{e.Message}。原数据未损坏。");
                    return MigrationResult.Failed;
                }
            }

            // Provider 级迁移
            foreach (var provider in SaveDataProviderRegistry.GetAll())
            {
                if (root.Providers.TryGetValue(provider.ProviderId, out var json))
                {
                    try
                    {
                        var token = Newtonsoft.Json.Linq.JToken.Parse(json);
                        if (token is Newtonsoft.Json.Linq.JObject obj)
                        {
                            int fileVersion = obj.Value<int?>("version") ?? provider.ProviderVersion;
                            if (fileVersion < provider.ProviderVersion)
                            {
                                if (!provider.Migrate(fileVersion, obj))
                                    Debug.LogWarning($"[Migration] Provider '{provider.ProviderId}' 迁移 {fileVersion}→{provider.ProviderVersion} 失败。");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Migration] Provider '{provider.ProviderId}' 迁移异常：{e.Message}");
                    }
                }
            }

            return root.SchemaVersion == CurrentVersion ? MigrationResult.Success : MigrationResult.Failed;
        }

        // ============ 迁移步骤（占位）============
        static bool Migrate_1_To_2(SaveDataRoot root)
        {
            // 示例：版本 1→2 的数据格式变更（当前为占位空操作）
            Debug.Log("[Migration] Step 1→2（占位空操作）。");
            return true;
        }

        static bool Migrate_2_To_3(SaveDataRoot root)
        {
            Debug.Log("[Migration] Step 2→3（占位空操作）。");
            return true;
        }
    }
}
