using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// 网络对象池：服务端权威 Spawn/Despawn，客户端缓存复用（不销毁，仅失活）。
    /// 用法：
    ///   服务端：var obj = pool.Spawn(prefab, pos, rot); ... pool.Despawn(obj);
    ///   预制体需在 NetworkIdentity 上有合法 assetId（普通预制体自动满足）。
    /// 客户端经 RegisterPrefab 自定义 handler 接收：从本地缓存取或 Instantiate 后失活回收。
    /// </summary>
    [AddComponentMenu("Cqw/Network/NetworkObjectPool")]
    public class NetworkObjectPool : NetworkBehaviour
    {
        public static NetworkObjectPool Instance { get; private set; }

        // 服务端：prefab → 未用实例栈
        readonly Dictionary<uint, Stack<GameObject>> serverPools = new Dictionary<uint, Stack<GameObject>>();
        // 客户端：prefab → 未用实例栈（缓存复用）
        readonly Dictionary<uint, Stack<GameObject>> clientPools = new Dictionary<uint, Stack<GameObject>>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>为指定预制体启用池化（服务器启动后调用；注册客户端 handler）。</summary>
        public void RegisterPrefab(GameObject prefab)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Pool] RegisterPrefab 需在服务器启动后调用。");
                return;
            }
            var id = prefab.GetComponent<NetworkIdentity>();
            if (id == null)
            {
                Debug.LogError($"[Pool] 预制体 {prefab.name} 缺少 NetworkIdentity，无法池化。");
                return;
            }
            if (id.assetId == 0)
            {
                Debug.LogError($"[Pool] {prefab.name} 的 assetId 为空（场景对象无资产 ID）。请使用保存在 Assets 下的预制体；场景内对象请改用 RegisterSpawnHandler。");
                return;
            }
            // 客户端自定义 spawn/unspawn handler（含本机 Host 客户端）
            NetworkClient.RegisterPrefab(prefab,
                (SpawnHandlerDelegate)SpawnHandler,
                (UnSpawnDelegate)UnspawnHandler);
            Debug.Log($"[Pool] 已注册池化预制体 {prefab.name} (assetId={id.assetId})");
        }

        // ============ 服务端 API ============

        /// <summary>服务端生成池化网络对象（从池取或新建），并 NetworkServer.Spawn。</summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Pool] Spawn 仅服务端可用。");
                return null;
            }

            var identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"[Pool] 预制体 {prefab.name} 缺少 NetworkIdentity。");
                return null;
            }

            var obj = GetFromPool(serverPools, identity.assetId, prefab, position, rotation);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            NetworkServer.Spawn(obj);
            return obj;
        }

        /// <summary>服务端回收（UnSpawn 不销毁；客户端经 unspawn handler 失活缓存）。</summary>
        public void Despawn(GameObject obj)
        {
            if (!NetworkServer.active) return;
            if (obj == null) { Debug.LogWarning("[Pool] Despawn(null)。"); return; }

            var identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Object.Destroy(obj);
                return;
            }

            NetworkServer.UnSpawn(obj); // 客户端走 unspawn handler（失活缓存）
            obj.SetActive(false);
            PushToPool(serverPools, identity.assetId, obj);
        }

        // ============ 客户端 handler ============

        GameObject SpawnHandler(SpawnMessage msg)
        {
            // 从客户端缓存取（有则复用），否则由 Mirror 默认实例化流程兜底
            var pooled = TakeFromClientPool(msg.assetId);
            if (pooled != null)
            {
                pooled.transform.position = msg.position;
                pooled.transform.rotation = msg.rotation;
                Debug.Log($"[Pool] 客户端复用缓存实例 assetId={msg.assetId}");
            }
            return pooled;
        }

        void UnspawnHandler(GameObject obj)
        {
            // 客户端收到 despawn：不销毁，失活进缓存
            var identity = obj.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                obj.SetActive(false);
                PushToPool(clientPools, identity.assetId, obj);
            }
            else Object.Destroy(obj);
        }

        // ============ 池操作 ============

        GameObject GetFromPool(Dictionary<uint, Stack<GameObject>> pools, uint assetId, GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (pools.TryGetValue(assetId, out var stack))
                while (stack.Count > 0)
                {
                    var pooled = stack.Pop();
                    if (pooled != null) return pooled;
                }
            var created = Instantiate(prefab, pos, rot);
            created.name = prefab.name; // 去掉 (Clone) 便于诊断
            return created;
        }

        void PushToPool(Dictionary<uint, Stack<GameObject>> pools, uint assetId, GameObject obj)
        {
            if (!pools.TryGetValue(assetId, out var stack))
            {
                stack = new Stack<GameObject>();
                pools[assetId] = stack;
            }
            stack.Push(obj);
        }

        GameObject TakeFromClientPool(uint assetId)
        {
            if (clientPools.TryGetValue(assetId, out var stack))
                while (stack.Count > 0)
                {
                    var pooled = stack.Pop();
                    if (pooled != null)
                    {
                        pooled.SetActive(true);
                        return pooled;
                    }
                }
            return null; // Mirror 默认实例化兜底
        }

        // ============ 诊断 ============

        /// <summary>池统计（服务端/客户端各 prefab 缓存数）。</summary>
        public string Describe()
        {
            int serverCount = 0, clientCount = 0;
            foreach (var kv in serverPools) serverCount += kv.Value.Count;
            foreach (var kv in clientPools) clientCount += kv.Value.Count;
            return $"serverPool={serverPools.Count} types/{serverCount} cached, clientPool={clientPools.Count} types/{clientCount} cached";
        }

        /// <summary>清空（场景卸载时调用，防跨场景残留）。</summary>
        public void Clear()
        {
            serverPools.Clear();
            clientPools.Clear();
        }
    }
}
