using System.Collections.Generic;
using UnityEngine;

namespace Cqw.MultilayerFramework.Network
{
    /// <summary>
    /// RoundRobin 生成点轮询器：按 order 升序依次返回生成位置，循环复用。
    /// 优先使用 NetworkConfig.SpawnPoints；为空时回退场景中全部 SpawnPoint 组件；
    /// 再为空时回退 Mirror 注册的 NetworkStartPosition。
    /// </summary>
    public static class RoundRobinSpawner
    {
        static int nextIndex;

        /// <summary>重置轮询（场景切换/会话重开时由外部调用）。</summary>
        public static void Reset() => nextIndex = 0;

        /// <summary>获取下一个生成 Transform。</summary>
        public static Transform GetNext(NetworkConfig config = null)
        {
            var list = CollectSpawnPoints(config);
            if (list.Count == 0) return null;

            var idx = nextIndex % list.Count;
            nextIndex++;
            return list[idx];
        }

        /// <summary>取生成位置（含 jitter）。注意：会推进轮询索引。</summary>
        public static Vector3 GetNextPosition(NetworkConfig config = null)
        {
            var t = GetNext(config);
            return t == null ? Vector3.zero : PositionWithJitter(t);
        }

        /// <summary>给定生成点的带抖动位置（不推进索引，供已取点后复用）。</summary>
        public static Vector3 PositionWithJitter(Transform spawnPoint)
        {
            if (spawnPoint == null) return Vector3.zero;
            var sp = spawnPoint.GetComponent<SpawnPoint>();
            var jitter = sp != null ? sp.jitterRadius : 0f;
            if (jitter <= 0f) return spawnPoint.position;
            var r = Random.insideUnitCircle * jitter;
            return spawnPoint.position + new Vector3(r.x, 0, r.y);
        }

        /// <summary>当前场景全部可用生成点（order 升序）。config 列表优先。</summary>
        public static List<Transform> CollectSpawnPoints(NetworkConfig config = null)
        {
            var result = new List<Transform>();

            if (config != null)
                foreach (var t in config.SpawnPoints)
                    if (t != null) result.Add(t);

            if (result.Count == 0)
            {
                foreach (var sp in Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include))
                    result.Add(sp.transform);
            }

            if (result.Count > 0)
                result.Sort((a, b) => {
                    var oa = a.GetComponent<SpawnPoint>() != null ? a.GetComponent<SpawnPoint>().order : 0;
                    var ob = b.GetComponent<SpawnPoint>() != null ? b.GetComponent<SpawnPoint>().order : 0;
                    int c = oa.CompareTo(ob);
                    return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
                });
            else
            {
                // 回退 Mirror StartPositions
                foreach (var nsp in Object.FindObjectsByType<Mirror.NetworkStartPosition>(FindObjectsInactive.Include))
                    result.Add(nsp.transform);
            }
            return result;
        }
    }
}
