using System.Collections.Generic;
using UnityEngine;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.Player
{
    public enum CameraMode
    {
        ThirdPerson, // Third-person follow
        FirstPerson, // First-person
        Spectate     // Spectator (detached from local character)
    }

    /// <summary>
    /// Player camera: third-person follow / first-person / spectator.
    /// Only enabled for local player (or spectator); disabled for non-local players.
    /// Look input rotates; CameraSwitch input cycles modes; in Spectate, Attack/Interact switches target.
    /// Attached to Player.prefab; camera itself is dynamically created in Start (Camera + AudioListener).
    /// </summary>
    [AddComponentMenu("Cqw/Player/PlayerCameraController")]
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("Mode")]
        public CameraMode Mode = CameraMode.ThirdPerson;

        [Header("Third Person")]
        [Tooltip("Camera offset from target (behind and above)")]
        public Vector3 thirdPersonOffset = new Vector3(0f, 1.6f, -3.5f);
        [Tooltip("Look-at height offset (above head)")]
        public float lookAtHeightOffset = 1.6f;
        [Tooltip("Camera follow smooth time (seconds; 0=hard lock)")]
        public float followSmoothTime = 0.08f;

        [Header("First Person")]
        [Tooltip("First-person camera position offset (eye height)")]
        public Vector3 firstPersonOffset = new Vector3(0f, 1.55f, 0.1f);

        [Header("Rotation")]
        [Tooltip("Mouse sensitivity")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("Gamepad sensitivity")]
        public float gamepadSensitivity = 120f;
        [Range(-89f, 0f)] public float pitchMin = -60f;
        [Range(0f, 89f)] public float pitchMax = 75f;

        [Header("Field of View")]
        [Range(50f, 110f)] public float fieldOfView = 70f;

        [Header("Collision")]
        [Tooltip("Occlusion detection radius")]
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;

        [Header("Input Source (empty = same-object DefaultPlayerInputSource)")]
        public MonoBehaviour inputSourceBehaviour;

        /// <summary>Input source (Phase 10 replaceable injection).</summary>
        public IPlayerInputSource InputSource { get; private set; }

        /// <summary>Spectate target (null = local character).</summary>
        public Transform SpectateTarget { get; private set; }

        /// <summary>Mode change event.</summary>
        public event System.Action<CameraMode> OnModeChanged;

        Camera cam;
        Transform pivot; // Rotation pivot (above target head)
        bool activated;
        float yaw, pitch;
        Vector3 followVel;
        readonly List<Transform> spectateList = new List<Transform>();
        int spectateIndex;

        void Start()
        {
            // Dynamically create camera (avoids per-player pre-placed camera conflicts)
            var camGo = new GameObject(name + "_Camera");
            camGo.transform.SetParent(null);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.enabled = false; // Not rendering until ActivateForLocal — local multiplayer split-screen placeholder

            var pivotGo = new GameObject(name + "_CamPivot");
            pivot = pivotGo.transform;

            // If activation request arrived before camera creation (event timing in spawn flow), re-activate here;
            // If not activated (non-local/extra local player), disable this component: no LateUpdate, no rendering
            if (activated) ApplyActivation();
            else enabled = false;
        }

        /// <summary>Camera component (for diagnostics/render target; null or disabled when not activated).</summary>
        public Camera CameraComponent => cam;

        void OnDestroy()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (pivot != null) Destroy(pivot.gameObject);
        }

        /// <summary>Enable camera (only called for local player/spectator; non-local camera stays disabled).</summary>
        public void ActivateForLocal()
        {
            activated = true;
            enabled = true;
            ApplyActivation();
        }

        /// <summary>Apply activation effects (camera may not exist yet, Start will handle it).</summary>
        void ApplyActivation()
        {
            if (cam == null) return;
            cam.enabled = true;
            cam.tag = "MainCamera";
            // Only one AudioListener per scene (avoids multiple listener warnings)
            if (Object.FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
        }

        public void Deactivate()
        {
            enabled = false;
            if (cam != null) { cam.enabled = false; cam.tag = "Untagged"; }
        }

        public void SetInputSource(IPlayerInputSource source) => InputSource = source;

        void ResolveInputSource()
        {
            if (inputSourceBehaviour is IPlayerInputSource s) { InputSource = s; return; }
            InputSource = GetComponent<DefaultPlayerInputSource>();
        }

        void LateUpdate()
        {
            if (cam == null || !enabled) return;
            if (InputSource == null) ResolveInputSource();
            InputSource?.Tick();

            // Mode switch
            if (InputSource != null && InputSource.CameraSwitchPressed)
                CycleMode();

            // Look rotation
            if (InputSource != null)
            {
                var look = InputSource.Look;
                bool isMouse = InputSource.DeviceId == "keyboardMouse";
                yaw += isMouse ? look.x * mouseSensitivity : look.x * gamepadSensitivity * Time.deltaTime;
                pitch -= isMouse ? look.y * mouseSensitivity : look.y * gamepadSensitivity * Time.deltaTime;
                pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
            }

            var target = Mode == CameraMode.Spectate
                ? (SpectateTarget != null ? SpectateTarget : transform)
                : transform;

            if (target == null) return;

            // Pivot follows target (above-head look-at point)
            var pivotGoal = target.position + Vector3.up * lookAtHeightOffset;
            pivot.position = followSmoothTime <= 0
                ? pivotGoal
                : Vector3.SmoothDamp(pivot.position, pivotGoal, ref followVel, followSmoothTime);
            pivot.rotation = Quaternion.Euler(pitch, yaw, 0);

            switch (Mode)
            {
                case CameraMode.ThirdPerson:
                {
                    // Ideal position = pivot + rotated offset
                    Vector3 desired = pivot.position + pivot.rotation * thirdPersonOffset;
                    // Occlusion: sphere cast from look-at point toward ideal position
                    Vector3 dir = desired - pivot.position;
                    float dist = dir.magnitude;
                    if (Physics.SphereCast(pivot.position, collisionRadius, dir.normalized,
                            out var hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
                        dist = Mathf.Max(0.5f, hit.distance - collisionRadius);
                    cam.transform.position = pivot.position + dir.normalized * dist;
                    cam.transform.LookAt(pivot.position);
                    break;
                }
                case CameraMode.FirstPerson:
                {
                    cam.transform.position = target.position + target.rotation * firstPersonOffset;
                    cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                    break;
                }
                case CameraMode.Spectate:
                {
                    Vector3 desired = pivot.position + pivot.rotation * thirdPersonOffset;
                    Vector3 dir = desired - pivot.position;
                    float dist = dir.magnitude;
                    if (Physics.SphereCast(pivot.position, collisionRadius, dir.normalized,
                            out var hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
                        dist = Mathf.Max(0.5f, hit.distance - collisionRadius);
                    cam.transform.position = pivot.position + dir.normalized * dist;
                    cam.transform.LookAt(pivot.position);
                    break;
                }
            }

            cam.fieldOfView = fieldOfView;
        }

        /// <summary>Cycle modes: ThirdPerson -> FirstPerson -> Spectate -> ThirdPerson.</summary>
        public void CycleMode()
        {
            Mode = Mode switch
            {
                CameraMode.ThirdPerson => CameraMode.FirstPerson,
                CameraMode.FirstPerson => CameraMode.Spectate,
                _ => CameraMode.ThirdPerson
            };
            if (Mode == CameraMode.Spectate) RebuildSpectateList();
            Debug.Log($"[PlayerCam] Mode switched -> {Mode}");
            OnModeChanged?.Invoke(Mode);
        }

        /// <summary>Spectate: cycle through registered players in order.</summary>
        public void SpectateNext()
        {
            if (Mode != CameraMode.Spectate) return;
            RebuildSpectateList();
            if (spectateList.Count == 0) return;
            spectateIndex = (spectateIndex + 1) % spectateList.Count;
            SpectateTarget = spectateList[spectateIndex];
            Debug.Log($"[PlayerCam] Spectate target -> {SpectateTarget.name}");
        }

        void RebuildSpectateList()
        {
            spectateList.Clear();
            foreach (var p in PlayerRegistry.GetAll())
                if (p != null && p.transform != transform)
                    spectateList.Add(p.transform);
        }

        void Update()
        {
            // Spectate target switch (Attack/Interact both work)
            if (Mode == CameraMode.Spectate && InputSource != null && (InputSource.Attack || InputSource.Interact))
                SpectateNext();
        }
    }
}
