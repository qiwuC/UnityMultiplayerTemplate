using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>状态接口（T8-03）。</summary>
    public interface IState<TContext>
    {
        string Id { get; }
        void OnEnter(TContext context);
        void OnUpdate(TContext context, float deltaTime);
        void OnExit(TContext context);
    }

    /// <summary>状态基类：按需覆写（T8-03）。</summary>
    public abstract class StateBase<TContext> : IState<TContext>
    {
        public abstract string Id { get; }
        public virtual void OnEnter(TContext context) { }
        public virtual void OnUpdate(TContext context, float deltaTime) { }
        public virtual void OnExit(TContext context) { }
    }

    /// <summary>帧驱动接口（StateMachineRunner 消费）。</summary>
    public interface IStateTickable
    {
        void Tick(float deltaTime);
    }

    /// <summary>
    /// 通用状态机（T8-03）：注册/初始/切换 + OnStateChanged 事件。
    /// 拒绝自转换与未注册目标转换（告警并返回 false，不抛异常）。
    /// </summary>
    public class StateMachine<TContext> : IStateTickable
    {
        readonly Dictionary<string, IState<TContext>> states = new Dictionary<string, IState<TContext>>();

        public TContext Context { get; }
        public IState<TContext> Current { get; private set; }
        public int StateCount => states.Count;

        /// <summary>状态切换事件（from 为 null 表示初始进入）。</summary>
        public event Action<IState<TContext>, IState<TContext>> OnStateChanged;

        public StateMachine(TContext context = default) => Context = context;

        public void AddState(IState<TContext> state)
        {
            if (state == null || string.IsNullOrEmpty(state.Id))
                throw new ArgumentNullException(nameof(state), "[StateMachine] 状态或 Id 为空。");
            if (states.TryGetValue(state.Id, out var old) && !ReferenceEquals(old, state))
                Debug.LogWarning($"[StateMachine] 状态 {state.Id} 已被覆盖。");
            states[state.Id] = state;
        }

        /// <summary>设置初始状态（仅在未进入任何状态前有效）。</summary>
        public bool SetInitial(string id)
        {
            if (Current != null)
            {
                Debug.LogWarning($"[StateMachine] SetInitial 仅在未进入状态前有效（当前={Current.Id}）。");
                return false;
            }
            return Transition(id);
        }

        /// <summary>切换状态（自转换/未注册目标被拒绝）。</summary>
        public bool Transition(string id)
        {
            if (Current != null && Current.Id == id)
            {
                Debug.LogWarning($"[StateMachine] 拒绝自转换：{id}");
                return false;
            }
            if (!states.TryGetValue(id, out var next))
            {
                Debug.LogWarning($"[StateMachine] 拒绝未注册转换：{Current?.Id ?? "(null)"} → {id}");
                return false;
            }

            var prev = Current;
            try { prev?.OnExit(Context); }
            finally
            {
                Current = next;
                next.OnEnter(Context);
            }
            OnStateChanged?.Invoke(prev, next);
            return true;
        }

        public void Update(float deltaTime) => Current?.OnUpdate(Context, deltaTime);
        public void Tick(float deltaTime) => Update(deltaTime);
    }

    /// <summary>层级状态机节点（T8-03）：可挂子状态；AddChild(isInitial) 指定初始子状态。</summary>
    public class HierarchicalState<TContext> : StateBase<TContext>
    {
        readonly string id;
        public override string Id => id;

        public HierarchicalState<TContext> Parent { get; private set; }
        public readonly List<HierarchicalState<TContext>> Children = new List<HierarchicalState<TContext>>();
        public HierarchicalState<TContext> InitialChild { get; private set; }

        public HierarchicalState(string id) => this.id = id;

        public HierarchicalState<TContext> AddChild(HierarchicalState<TContext> child, bool isInitial = false)
        {
            child.Parent = this;
            Children.Add(child);
            if (isInitial || InitialChild == null) InitialChild = child;
            return child;
        }
    }

    /// <summary>
    /// 层级状态机（T8-03）：父子状态，父 OnUpdate 先于子；
    /// 进入复合状态自动沿 InitialChild 下钻到叶子；退出按叶→根顺序。
    /// Transition 到任意已注册状态（祖先链自动补齐）。
    /// </summary>
    public class HierarchicalStateMachine<TContext> : IStateTickable
    {
        readonly Dictionary<string, HierarchicalState<TContext>> states = new Dictionary<string, HierarchicalState<TContext>>();
        readonly List<HierarchicalState<TContext>> currentChain = new List<HierarchicalState<TContext>>(); // root→leaf

        public TContext Context { get; }
        /// <summary>当前叶子状态。</summary>
        public HierarchicalState<TContext> CurrentLeaf => currentChain.Count > 0 ? currentChain[currentChain.Count - 1] : null;
        public int StateCount => states.Count;

        /// <summary>叶子状态切换事件。</summary>
        public event Action<HierarchicalState<TContext>, HierarchicalState<TContext>> OnStateChanged;

        public HierarchicalStateMachine(TContext context = default) => Context = context;

        /// <summary>注册状态；parentId 非空时挂到父状态。</summary>
        public HierarchicalState<TContext> AddState(string id, string parentId = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (states.ContainsKey(id)) { Debug.LogWarning($"[HSM] 状态 {id} 已被覆盖。"); }
            var state = new HierarchicalState<TContext>(id);
            states[id] = state;
            if (!string.IsNullOrEmpty(parentId))
            {
                if (!states.TryGetValue(parentId, out var parent))
                    throw new ArgumentException($"[HSM] 父状态未注册：{parentId}");
                parent.AddChild(state);
            }
            return state;
        }

        public bool SetInitial(string id)
        {
            if (currentChain.Count > 0)
            {
                Debug.LogWarning("[HSM] SetInitial 仅在未进入状态前有效。");
                return false;
            }
            return Transition(id);
        }

        public bool Transition(string id)
        {
            var leaf = CurrentLeaf;
            if (leaf != null && leaf.Id == id)
            {
                Debug.LogWarning($"[HSM] 拒绝自转换：{id}");
                return false;
            }
            if (!states.TryGetValue(id, out var target))
            {
                Debug.LogWarning($"[HSM] 拒绝未注册转换：{leaf?.Id ?? "(null)"} → {id}");
                return false;
            }

            var prevLeaf = leaf;

            // 退出当前链（叶→根）
            for (int i = currentChain.Count - 1; i >= 0; i--)
                currentChain[i].OnExit(Context);
            currentChain.Clear();

            // 构建目标祖先链（root→target）并依次进入
            var chain = new List<HierarchicalState<TContext>>();
            var walk = target;
            while (walk != null) { chain.Add(walk); walk = walk.Parent; }
            chain.Reverse();
            foreach (var s in chain)
            {
                currentChain.Add(s);
                s.OnEnter(Context);
            }

            // 目标为复合状态时沿 InitialChild 下钻到叶子
            var descend = target.InitialChild;
            while (descend != null)
            {
                currentChain.Add(descend);
                descend.OnEnter(Context);
                descend = descend.InitialChild;
            }

            OnStateChanged?.Invoke(prevLeaf, CurrentLeaf);
            return true;
        }

        public void Update(float deltaTime)
        {
            // 父先于子
            for (int i = 0; i < currentChain.Count; i++)
                currentChain[i].OnUpdate(Context, deltaTime);
        }

        public void Tick(float deltaTime) => Update(deltaTime);
    }

    /// <summary>状态机帧驱动器（T8-03）：Update 驱动所有注册的状态机/可 Tick 对象。</summary>
    [AddComponentMenu("Cqw/Systems/StateMachineRunner")]
    public class StateMachineRunner : MonoBehaviour
    {
        static StateMachineRunner instance;
        public static StateMachineRunner Instance => instance;

        readonly List<IStateTickable> tickables = new List<IStateTickable>();

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>注册状态机（幂等）。</summary>
        public T Add<T>(T tickable) where T : IStateTickable
        {
            if (tickable != null && !tickables.Contains(tickable)) tickables.Add(tickable);
            return tickable;
        }

        public void Remove(IStateTickable tickable) => tickables.Remove(tickable);

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = tickables.Count - 1; i >= 0; i--)
                tickables[i]?.Tick(dt);
        }
    }
}
