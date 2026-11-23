using Mirror;
using System;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// P-01 暂停状态网络广播（NetworkBehaviour，随 Host 启动 spawn）。
    /// SyncVar&lt;bool&gt; IsPaused 服务端权威；客户端 hook 收到后禁用输入并显示暂停遮罩。
    /// SessionManager.PauseSession/ResumeSession 调用 ServerSetPaused。
    /// </summary>
    public class NetworkPauseState : NetworkBehaviour
    {
        public static NetworkPauseState Instance { get; private set; }

        [SyncVar(hook = nameof(OnPauseChangedHook))]
        bool isPaused;

        public bool IsPaused => isPaused;

        /// <summary>暂停状态变更（双端触发）。</summary>
        public event Action<bool> OnPauseStateChanged;

        [Server]
        public void ServerSetPaused(bool value)
        {
            if (isPaused == value) return;
            isPaused = value;
            Debug.Log($"[PauseState] 服务端设置暂停={value}。");
        }

        void OnPauseChangedHook(bool oldV, bool newV)
        {
            Debug.Log($"[PauseState] 客户端收到暂停状态：{newV}。");
            OnPauseStateChanged?.Invoke(newV);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (Instance == this) Instance = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
            // 客户端加入时同步当前暂停状态
            OnPauseStateChanged?.Invoke(isPaused);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Instance == this) Instance = null;
        }
    }
}
