using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Network;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>
    /// 队伍管理：分配（自动平衡/手动）、查询、事件。
    /// 队伍定义来自 TeamConfig（静态资产双端一致）；玩家队伍经 PlayerIdentity.TeamId（SyncVar）同步，
    /// 因此本类为普通单例（无需 spawn），与 PlayerRegistry 协作查询，不重复存储玩家对象。
    /// </summary>
    [AddComponentMenu("Cqw/Player/TeamManager")]
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }

        [Tooltip("队伍配置（留空则从默认路径加载/内置默认）")]
        public TeamConfig Config;

        /// <summary>队伍分配事件（玩家, 队伍 ID）。</summary>
        public event Action<PlayerIdentity, int> OnTeamAssigned;
        /// <summary>队伍变更（玩家, 旧 ID, 新 ID）。</summary>
        public event Action<PlayerIdentity, int, int> OnTeamChanged;
        /// <summary>队伍列表变化（分配后触发，UI 刷新）。</summary>
        public event Action OnTeamsUpdated;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (Config == null) Config = FindConfig();
            if (Config == null)
            {
                // 兜底：内置默认两队
                Config = ScriptableObject.CreateInstance<TeamConfig>();
                Debug.LogWarning("[Team] 未找到 TeamConfig 资产，使用内置默认两队（红/蓝）。");
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        TeamConfig FindConfig()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<TeamConfig>(TeamConfig.DefaultAssetPath);
#else
            return null;
#endif
        }

        /// <summary>队伍数量。</summary>
        public int TeamCount => Config != null ? Config.Teams.Count : 0;

        /// <summary>取队伍定义（无则 null）。</summary>
        public TeamConfig.TeamDefinition GetTeamDefinition(int teamId) => Config != null ? Config.Get(teamId) : null;

        /// <summary>取队伍名称本地化键。</summary>
        public string GetTeamNameKey(int teamId) => GetTeamDefinition(teamId)?.NameKey ?? "team.none";

        /// <summary>取队伍颜色。</summary>
        public Color GetTeamColor(int teamId) => GetTeamDefinition(teamId)?.Color ?? Color.gray;

        /// <summary>自动分配（服务端）：按人数最少的队伍平衡；无队伍配置时返回 false。</summary>
        [Server]
        public bool AutoAssign(PlayerIdentity player)
        {
            if (player == null || TeamCount == 0) return false;

            // 统计各队人数（经 Registry 查询，不重复存储）
            int bestTeam = Config.Teams[0].TeamId;
            int bestCount = int.MaxValue;
            foreach (var t in Config.Teams)
            {
                int c = PlayerRegistry.GetByTeam(t.TeamId).Count;
                if (c < bestCount) { bestCount = c; bestTeam = t.TeamId; }
            }
            return AssignTeam(player, bestTeam);
        }

        /// <summary>手动指定队伍（服务端）。</summary>
        [Server]
        public bool AssignTeam(PlayerIdentity player, int teamId)
        {
            if (player == null) return false;
            if (GetTeamDefinition(teamId) == null && teamId != -1)
            {
                Debug.LogWarning($"[Team] 队伍 {teamId} 不存在于 TeamConfig。");
                return false;
            }

            int oldTeam = player.TeamId;
            if (oldTeam == teamId) return false;

            player.SetTeamId(teamId); // SyncVar 同步客户端
            OnTeamAssigned?.Invoke(player, teamId);
            OnTeamChanged?.Invoke(player, oldTeam, teamId);
            OnTeamsUpdated?.Invoke();
            Debug.Log($"[Team] {player.DisplayName} 队伍 {oldTeam} → {teamId}（{GetTeamNameKey(teamId)}）");
            return true;
        }

        /// <summary>查询某队全部玩家（经 Registry）。</summary>
        public List<PlayerIdentity> GetTeamMembers(int teamId) => PlayerRegistry.GetByTeam(teamId);

        /// <summary>查询玩家所属队伍 ID（-1=未分配）。</summary>
        public int GetTeamOf(PlayerIdentity player) => player != null ? player.TeamId : -1;

        /// <summary>队伍人数统计（teamId → 人数）。</summary>
        public Dictionary<int, int> GetTeamCounts()
        {
            var result = new Dictionary<int, int>();
            if (Config == null) return result;
            foreach (var t in Config.Teams)
                result[t.TeamId] = PlayerRegistry.GetByTeam(t.TeamId).Count;
            return result;
        }
    }
}
