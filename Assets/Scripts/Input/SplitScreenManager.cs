using System.Collections.Generic;
using UnityEngine;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.Input
{
    /// <summary>
    /// 分屏管理（T10-04）：1 玩家全屏；2 玩家左右分屏；3~4 玩家 2×2 网格。
    /// 监听 LocalPlayerJoinManager 槽位数量变化重算布局并应用视口；
    /// 补激活阶段三留下的"槽位 1+ 相机不激活"缺口（P-07 在此关闭）；
    /// 同一时间仅槽位 0 相机持有 AudioListener。
    /// </summary>
    [AddComponentMenu("Cqw/Input/SplitScreenManager")]
    public class SplitScreenManager : MonoBehaviour
    {
        public static SplitScreenManager Instance { get; private set; }

        [Tooltip("2 玩家布局：true=左右分屏，false=上下分屏")]
        public bool twoPlayerSideBySide = true;

        readonly List<PlayerCameraController> cams = new List<PlayerCameraController>();
        int lastCount = -1;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null) return;
            new GameObject("Systems_SplitScreenManager").AddComponent<SplitScreenManager>();
        }

        void Update()
        {
            var join = LocalPlayerJoinManager.Instance;
            int count = join != null ? join.Slots.Count : 0;
            if (count == lastCount) return;
            lastCount = count;
            RecalculateLayout();
        }

        /// <summary>重算并应用分屏布局（调试/预览可直调）。</summary>
        public void RecalculateLayout()
        {
            cams.Clear();
            var join = LocalPlayerJoinManager.Instance;
            if (join == null) return;

            foreach (var slot in join.Slots)
            {
                if (slot?.Player == null) continue;
                var cam = slot.Player.GetComponent<PlayerCameraController>();
                if (cam == null) continue;
                if (slot.SlotIndex > 0 && !cam.isActiveAndEnabled)
                    cam.ActivateForLocal(); // 补激活（阶段三分屏缺口占位）
                if (cam.CameraComponent != null) cams.Add(cam);
            }

            int n = cams.Count;
            for (int i = 0; i < n; i++)
            {
                var camComp = cams[i].CameraComponent;
                camComp.rect = ViewportFor(i, n);
                var listener = camComp.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = i == 0;
            }

            if (n > 1) Debug.Log($"[SplitScreen] 布局 → {n} 玩家。");
        }

        /// <summary>视口计算（公开：预览/调试用）。</summary>
        public Rect ViewportFor(int index, int count)
        {
            if (count <= 1) return new Rect(0f, 0f, 1f, 1f);
            if (count == 2)
            {
                return twoPlayerSideBySide
                    ? (index == 0 ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0.5f, 0f, 0.5f, 1f))
                    : (index == 0 ? new Rect(0f, 0.5f, 1f, 0.5f) : new Rect(0f, 0f, 1f, 0.5f));
            }
            // 3~4：2×2 网格（index 0=左上 1=右上 2=左下 3=右下）
            int col = index % 2;
            int row = index / 2;
            return new Rect(col * 0.5f, 1f - 0.5f - row * 0.5f, 0.5f, 0.5f);
        }
    }
}
