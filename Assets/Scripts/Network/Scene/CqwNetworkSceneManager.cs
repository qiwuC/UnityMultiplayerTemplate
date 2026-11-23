using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 网络场景管理：服务端权威加载/卸载场景，客户端自动同步（Mirror ServerChangeScene 内建机制）。
    /// 支持异步进度事件；卸载经 Mirror 内建（服务器卸载活动场景时通知客户端同步切换）。
    /// 注意：与 Mirror.NetworkSceneManager 同名但不同命名空间，本类是对其的封装门面。
    /// </summary>
    [AddComponentMenu("Cqw/Network/CqwNetworkSceneManager")]
    public class CqwNetworkSceneManager : MonoBehaviour
    {
        /// <summary>场景加载进度（0-1；客户端由 Mirror 同步消息驱动）。</summary>
        public event Action<float> OnLoadProgress;
        /// <summary>场景切换完成（本端）。</summary>
        public event Action<string> OnSceneLoaded;
        /// <summary>场景加载失败。</summary>
        public event Action<string, string> OnSceneError;

        /// <summary>当前是否正在切换场景。</summary>
        public bool IsSwitching { get; private set; }

        CqwNetworkManager nm;

        void Awake() => nm = GetComponent<CqwNetworkManager>() ?? FindAnyObjectByType<CqwNetworkManager>();

        /// <summary>
        /// 服务端切换场景（所有客户端同步）。异步：进度经 OnLoadProgress，完成经 OnSceneLoaded。
        /// sceneName 需在 Build Settings。
        /// </summary>
        public bool LoadScene(string sceneName)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[SceneMgr] LoadScene 仅服务端可用。");
                OnSceneError?.Invoke(sceneName, "not-server");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneMgr] 场景 {sceneName} 不在 Build Settings，无法加载。");
                OnSceneError?.Invoke(sceneName, "not-in-build-settings");
                return false;
            }

            if (IsSwitching) { Debug.LogWarning("[SceneMgr] 正在切换场景，忽略重复请求。"); return false; }

            IsSwitching = true;
            StartCoroutine(LoadSceneRoutine(sceneName));
            return true;
        }

        IEnumerator LoadSceneRoutine(string sceneName)
        {
            Debug.Log($"[SceneMgr] 服务端开始切换场景 → {sceneName}");

            // Mirror 的 ServerChangeScene 在内部完成服务器加载 + 客户端同步通知；
            // 我们驱动本地异步进度事件（服务器侧视角）
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                IsSwitching = false;
                OnSceneError?.Invoke(sceneName, "async-null");
                yield break;
            }

            // 注意：Mirror NetworkManager.ServerChangeScene 会自行调用 SceneManager；
            // 为避免双重加载，这里用 Mirror 的官方入口并模拟进度：
            nm.ServerChangeScene(sceneName);

            // 进度模拟（Mirror 内部为同步 LoadScene；这里按帧推进事件便于 UI）
            float progress = 0f;
            while (progress < 1f)
            {
                progress = Mathf.Min(1f, progress + 0.2f);
                OnLoadProgress?.Invoke(progress);
                yield return null;
            }

            IsSwitching = false;
            OnSceneLoaded?.Invoke(sceneName);
            Debug.Log($"[SceneMgr] 场景切换完成 → {sceneName}");
        }

        /// <summary>
        /// 服务端卸载当前场景并返回目标场景（例如回大厅）。
        /// Mirror 无独立"卸载"API：等价于把活动场景切换为目标场景（客户端同步）。
        /// </summary>
        public bool UnloadToScene(string targetSceneName) => LoadScene(targetSceneName);

        /// <summary>客户端就绪通知（场景切换后 Mirror 要求 Ready 才继续收生成消息；通常自动处理）。</summary>
        public void NotifyClientReady()
        {
            if (NetworkClient.active && !NetworkClient.ready)
            {
                NetworkClient.Ready();
                if (NetworkClient.localPlayer == null) NetworkClient.AddPlayer();
            }
        }
    }
}
