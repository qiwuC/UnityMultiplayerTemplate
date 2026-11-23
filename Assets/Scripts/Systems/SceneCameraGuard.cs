using UnityEngine;

namespace Cqw.MultilayerFramework.Systems
{
    /// <summary>
    /// 场景相机守护（B-02 稳定性）：检测到玩家相机（另一带 MainCamera tag 的启用相机）后
    /// 自动禁用自身相机与 AudioListener，避免双相机渲染 + 双监听。
    /// 挂在 SampleScene 等 EmptyScene 场景的 Main Camera 上。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Cqw/Systems/SceneCameraGuard")]
    public class SceneCameraGuard : MonoBehaviour
    {
        Camera _cam;
        AudioListener _listener;
        bool _disabled;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _listener = GetComponent<AudioListener>();
        }

        void LateUpdate()
        {
            if (_disabled) return;
            foreach (var cam in Camera.allCameras)
            {
                if (cam == _cam || !cam.enabled || !cam.CompareTag("MainCamera")) continue;
                _cam.enabled = false;
                if (_listener != null) _listener.enabled = false;
                _disabled = true;
                Debug.Log("[SceneCameraGuard] 玩家相机激活 → 场景相机已禁用。");
                return;
            }
        }
    }
}
