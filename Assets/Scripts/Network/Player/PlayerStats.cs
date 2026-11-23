using System;
using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>Buff 运行时状态（网络同步：id + 剩余时间 + 层数）。</summary>
    [Serializable]
    public struct BuffState
    {
        public int buffId;
        public float remainingTime;
        public int stacks;
    }

    /// <summary>
    /// 玩家数值（网络同步）：生命/体力 SyncVar + Buff 框架（SyncList）。
    /// 服务端权威：ApplyDamage/Heal/ConsumeStamina/RestoreStamina/AddBuff/RemoveBuff 仅服务端调用。
    /// 体力回复：服务端 tick（延迟+速率可配）。
    /// 事件双端触发（服务端手动 + 客户端 hook/SyncList Callback）。
    /// </summary>
    [AddComponentMenu("Cqw/Network/PlayerStats")]
    public class PlayerStats : NetworkBehaviour
    {
        [Header("基础数值")]
        [SyncVar(hook = nameof(OnHealthChangedHook))]
        int health = 100;
        [SyncVar] int maxHealth = 100;
        [SyncVar(hook = nameof(OnStaminaChangedHook))]
        float stamina = 100f;
        [SyncVar] float maxStamina = 100f;
        [SyncVar] bool isDead;

        [Header("体力回复（服务端）")]
        [Tooltip("停止消耗后多少秒开始回复")]
        public float staminaRegenDelay = 2f;
        [Tooltip("每秒回复量")]
        public float staminaRegenRate = 15f;

        public int Health => health;
        public int MaxHealth => maxHealth;
        public float Stamina => stamina;
        public float MaxStamina => maxStamina;
        public bool IsDead => isDead;

        // ---- Buff（SyncList：id + 剩余时间 + 层数）----
        public readonly SyncList<BuffState> buffs = new SyncList<BuffState>();

        // ---- 事件（双端）----
        public event Action<int, int> OnHealthChanged;     // (new, old)
        public event Action<float, float> OnStaminaChanged;
        public event Action<BuffState> OnBuffAdded;
        public event Action<BuffState> OnBuffRemoved;

        /// <summary>Buff 定义解析（buffId → BuffData 资产；由配置层注册）。</summary>
        static readonly System.Collections.Generic.Dictionary<int, BuffData> buffTable =
            new System.Collections.Generic.Dictionary<int, BuffData>();

        public static void RegisterBuffDefinition(BuffData data)
        {
            if (data != null) buffTable[data.BuffId] = data;
        }

        public static BuffData GetBuffDefinition(int buffId) =>
            buffTable.TryGetValue(buffId, out var d) ? d : null;

        float lastStaminaConsumeTime;

        void Awake()
        {
            // Mirror 96 SyncList Callback：op, index, oldItem, newItem
            buffs.Callback += OnBuffsChanged;
        }

        void Update()
        {
            if (!isServer) return;
            TickStaminaRegen();
            TickBuffs();
        }

        // ==================== 生命 ====================

        [Server]
        public void ApplyDamage(int amount)
        {
            if (isDead || amount <= 0) return;
            health = Mathf.Clamp(health - amount, 0, maxHealth);
            OnHealthChanged?.Invoke(health, health); // 服务端手动事件
            if (health == 0) isDead = true;          // 死亡占位
        }

        [Server]
        public void Heal(int amount)
        {
            if (isDead || amount <= 0) return;
            health = Mathf.Clamp(health + amount, 0, maxHealth);
            OnHealthChanged?.Invoke(health, health);
        }

        [Server]
        public void SetMaxHealth(int value) => maxHealth = Mathf.Max(1, value);

        void OnHealthChangedHook(int oldV, int newV) => OnHealthChanged?.Invoke(newV, oldV);

        // ==================== 体力 ====================

        [Server]
        public bool ConsumeStamina(float amount)
        {
            if (isDead) return false;
            if (stamina < amount) return false;
            stamina = Mathf.Clamp(stamina - amount, 0, maxStamina);
            lastStaminaConsumeTime = Time.unscaledTime;
            OnStaminaChanged?.Invoke(stamina, stamina);
            return true;
        }

        [Server]
        public void RestoreStamina(float amount)
        {
            if (isDead) return;
            stamina = Mathf.Clamp(stamina + amount, 0, maxStamina);
            OnStaminaChanged?.Invoke(stamina, stamina);
        }

        [Server]
        public void SetMaxStamina(float value) => maxStamina = Mathf.Max(1f, value);

        void OnStaminaChangedHook(float oldV, float newV) => OnStaminaChanged?.Invoke(newV, oldV);

        void TickStaminaRegen()
        {
            if (isDead) return;
            if (Time.unscaledTime - lastStaminaConsumeTime < staminaRegenDelay) return;
            if (stamina >= maxStamina) return;
            stamina = Mathf.Min(maxStamina, stamina + staminaRegenRate * Time.deltaTime);
        }

        // ==================== Buff 框架 ====================

        /// <summary>添加 Buff（服务端）：叠加刷新时间；SyncList 自动同步客户端。</summary>
        [Server]
        public void AddBuff(BuffData data)
        {
            if (data == null) return;
            for (int i = 0; i < buffs.Count; i++)
            {
                var b = buffs[i];
                if (b.buffId != data.BuffId) continue;
                b.stacks = Mathf.Min(b.stacks + 1, data.MaxStacks);
                b.remainingTime = data.Duration;
                buffs[i] = b;
                OnBuffAdded?.Invoke(b);
                return;
            }
            var state = new BuffState
            {
                buffId = data.BuffId,
                remainingTime = data.Duration,
                stacks = 1
            };
            buffs.Add(state);
            OnBuffAdded?.Invoke(state);
        }

        [Server]
        public bool RemoveBuff(int buffId)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].buffId != buffId) continue;
                var removed = buffs[i];
                buffs.RemoveAt(i);
                OnBuffRemoved?.Invoke(removed);
                return true;
            }
            return false;
        }

        [Server]
        public bool HasBuff(int buffId)
        {
            foreach (var b in buffs) if (b.buffId == buffId) return true;
            return false;
        }

        [Server]
        public int GetBuffStacks(int buffId)
        {
            foreach (var b in buffs) if (b.buffId == buffId) return b.stacks;
            return 0;
        }

        void TickBuffs()
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                var b = buffs[i];
                b.remainingTime -= Time.deltaTime;
                if (b.remainingTime <= 0f)
                {
                    buffs.RemoveAt(i);
                    OnBuffRemoved?.Invoke(b);
                }
                else buffs[i] = b;
            }
        }

        // SyncList Callback（4 参）：客户端经此触发事件（服务端已在 API 内手动触发）
        void OnBuffsChanged(SyncList<BuffState>.Operation op, int index, BuffState oldItem, BuffState newItem)
        {
            if (isServer) return; // 服务端事件已在 API 手动触发
            switch (op)
            {
                case SyncList<BuffState>.Operation.OP_ADD:
                case SyncList<BuffState>.Operation.OP_SET:
                    OnBuffAdded?.Invoke(newItem);
                    break;
                case SyncList<BuffState>.Operation.OP_REMOVEAT:
                case SyncList<BuffState>.Operation.OP_CLEAR:
                    OnBuffRemoved?.Invoke(oldItem);
                    break;
            }
        }

        new void OnValidate()
        {
            if (health > maxHealth) health = maxHealth;
            if (stamina > maxStamina) stamina = maxStamina;
        }
    }
}
