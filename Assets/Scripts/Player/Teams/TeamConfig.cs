using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>队伍定义（ScriptableObject：静态配置双端一致）。</summary>
    [CreateAssetMenu(fileName = "TeamConfig", menuName = "Cqw/Player/TeamConfig", order = 1)]
    public class TeamConfig : ScriptableObject
    {
        /// <summary>单个队伍定义。</summary>
        [Serializable]
        public class TeamDefinition
        {
            [Tooltip("队伍 ID（0 起；-1 保留为未分配）")]
            public int TeamId;
            [Tooltip("队伍名称本地化键（如 team.red）")]
            public string NameKey = "team.unnamed";
            [Tooltip("队伍颜色（UI/角色染色）")]
            public Color Color = Color.white;
        }

        [Tooltip("队伍列表（顺序即分配顺序）")]
        public List<TeamDefinition> Teams = new List<TeamDefinition>
        {
            new TeamDefinition { TeamId = 0, NameKey = "team.red", Color = new Color(0.9f, 0.25f, 0.25f) },
            new TeamDefinition { TeamId = 1, NameKey = "team.blue", Color = new Color(0.25f, 0.5f, 0.95f) }
        };

        /// <summary>按 ID 取定义（无则 null）。</summary>
        public TeamDefinition Get(int teamId)
        {
            foreach (var t in Teams) if (t.TeamId == teamId) return t;
            return null;
        }

        public const string DefaultAssetPath = "Assets/ScriptableObjects/TeamConfig.asset";
    }
}
