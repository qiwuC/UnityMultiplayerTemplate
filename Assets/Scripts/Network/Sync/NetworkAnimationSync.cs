using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 网络动画同步：服务端采集 Animator 参数（Int/Float/Bool/Trigger），
    /// 变化时广播 SyncVar 快照；Trigger 以一次性事件同步（可靠）。
    /// 挂在带 Animator 的网络对象上（如玩家）。服务端权威驱动动画。
    /// </summary>
    [AddComponentMenu("Cqw/Network/NetworkAnimationSync")]
    public class NetworkAnimationSync : NetworkBehaviour
    {
        [Header("目标（留空取同对象/子物体 Animator）")]
        public Animator target;

        // 同步间隔复用 Mirror NetworkBehaviour.syncInterval（SyncVar 同步间隔，默认 0.1s）。
        // 不再声明同名字段：Unity 不允许同名字段在父子类重复序列化。

        [Tooltip("不同步的参数名（如本地驱动的混合树参数）")]
        public string[] excludedParams = System.Array.Empty<string>();

        // 参数快照（SyncVar：整包广播）
        [SyncVar(hook = nameof(OnSnapshotChanged))]
        string snapshotJson = "";

        // Trigger 事件通道：递增计数（客户端检测变化即播放一次）
        [SyncVar(hook = nameof(OnTriggerCountChanged))]
        int triggerCounter;
        [SyncVar] string lastTriggerName = "";

        float lastAnimSyncTime;
        int[] lastInts; float[] lastFloats; bool[] lastBools;
        string[] paramNames; AnimatorControllerParameterType[] paramTypes;

        void Awake()
        {
            if (target == null) target = GetComponentInChildren<Animator>(true);
            CacheParameters();
        }

        void CacheParameters()
        {
            if (target == null || target.runtimeAnimatorController == null)
            {
                paramNames = System.Array.Empty<string>();
                paramTypes = System.Array.Empty<AnimatorControllerParameterType>();
                return;
            }
            var ps = target.parameters;
            var names = new System.Collections.Generic.List<string>();
            var types = new System.Collections.Generic.List<AnimatorControllerParameterType>();
            foreach (var p in ps)
            {
                if (p.type == AnimatorControllerParameterType.Trigger) continue; // Trigger 走事件通道
                if (System.Array.IndexOf(excludedParams, p.name) >= 0) continue;
                names.Add(p.name);
                types.Add(p.type);
            }
            paramNames = names.ToArray();
            paramTypes = types.ToArray();
            lastInts = new int[paramNames.Length];
            lastFloats = new float[paramNames.Length];
            lastBools = new bool[paramNames.Length];
        }

        void Update()
        {
            // 仅服务端采集（netIdentity 未就绪时跳过：spawn 前的几帧）
            if (target == null || netIdentity == null || !isServer) return;
            // 无 AnimatorController 时安全降级（避免 Unity 的 "not playing an AnimatorController" 警告刷屏）
            if (target.runtimeAnimatorController == null)
            {
                if (paramNames.Length != 0) CacheParameters(); // 重新缓存（可能后续挂上控制器）
                return;
            }
            if (Time.unscaledTime - lastAnimSyncTime < syncInterval) return;
            lastAnimSyncTime = Time.unscaledTime;

            bool changed = false;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < paramNames.Length; i++)
            {
                switch (paramTypes[i])
                {
                    case AnimatorControllerParameterType.Int:
                        var iv = target.GetInteger(paramNames[i]);
                        if (iv != lastInts[i]) { lastInts[i] = iv; changed = true; }
                        sb.Append(iv);
                        break;
                    case AnimatorControllerParameterType.Float:
                        var fv = target.GetFloat(paramNames[i]);
                        if (Mathf.Abs(fv - lastFloats[i]) > 0.001f) { lastFloats[i] = fv; changed = true; }
                        sb.Append(fv.ToString("R"));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        var bv = target.GetBool(paramNames[i]);
                        if (bv != lastBools[i]) { lastBools[i] = bv; changed = true; }
                        sb.Append(bv ? "1" : "0");
                        break;
                }
                sb.Append('|');
            }

            if (changed)
            {
                snapshotJson = sb.ToString();
            }
        }

        /// <summary>服务端触发动画事件（Trigger 参数）。</summary>
        [Server]
        public void ServerTrigger(string triggerName)
        {
            if (target == null) return;
            target.SetTrigger(triggerName);
            lastTriggerName = triggerName;
            triggerCounter++;
        }

        // ---- 客户端应用 ----

        void OnSnapshotChanged(string oldV, string newV) => ApplySnapshot(newV);

        void ApplySnapshot(string json)
        {
            if (target == null || string.IsNullOrEmpty(json)) return;
            var parts = json.Split('|');
            for (int i = 0; i < paramNames.Length && i < parts.Length; i++)
            {
                switch (paramTypes[i])
                {
                    case AnimatorControllerParameterType.Int:
                        if (int.TryParse(parts[i], out var iv)) target.SetInteger(paramNames[i], iv);
                        break;
                    case AnimatorControllerParameterType.Float:
                        if (float.TryParse(parts[i], out var fv))
                            target.SetFloat(paramNames[i], fv);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        target.SetBool(paramNames[i], parts[i] == "1");
                        break;
                }
            }
        }

        void OnTriggerCountChanged(int oldV, int newV)
        {
            if (newV <= oldV) return; // 忽略初始化/重置
            if (target != null && !string.IsNullOrEmpty(lastTriggerName))
                target.SetTrigger(lastTriggerName);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // 初始快照应用
            if (!isServer) ApplySnapshot(snapshotJson);
        }
    }
}
