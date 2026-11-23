using Mirror;
using UnityEngine;
using Cqw.MultilayerFramework.Player;

namespace Cqw.MultilayerFramework.Player
{
    /// <summary>Player movement settings (can be converted to ScriptableObject; this phase provides field-level config).</summary>
    [System.Serializable]
    public class PlayerMovementSettings
    {
        [Header("Movement")]
        [Tooltip("Max movement speed (m/s)")]
        public float moveSpeed = 6f;
        [Tooltip("Time to accelerate to target speed (seconds, lower = more responsive)")]
        public float accelerationTime = 0.15f;
        [Tooltip("Turn smoothing time (seconds)")]
        public float turnSmoothTime = 0.12f;

        [Header("Jump & Gravity")]
        [Tooltip("Jump initial velocity (m/s)")]
        public float jumpForce = 6.5f;
        [Tooltip("Gravity acceleration (m/s^2)")]
        public float gravity = -18f;
        [Tooltip("Coyote time: grace period after leaving ground where jump is still allowed (seconds)")]
        public float coyoteTime = 0.12f;

        [Header("Ground Detection")]
        [Tooltip("Ground check sphere radius")]
        public float groundCheckRadius = 0.25f;
        [Tooltip("Ground check distance (from feet)")]
        public float groundCheckDistance = 0.15f;
        public LayerMask groundMask = ~0;
    }

    /// <summary>
    /// Player controller: CharacterController-based movement (horizontal/gravity/jump/ground detection).
    /// Only isLocalPlayer accepts IPlayerInputSource input; non-local players driven by NetworkTransform (this component disabled).
    /// Uses NetworkTransform(syncDirection=ClientToServer): local movement is client-authoritative, server broadcasts to other clients, no rubber-banding.
    /// </summary>
    [AddComponentMenu("Cqw/Player/PlayerController")]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Config")]
        public PlayerMovementSettings settings = new PlayerMovementSettings();

        [Header("Input Source (empty = DefaultPlayerInputSource)")]
        [Tooltip("Input source; if empty, auto-resolves to same-object DefaultPlayerInputSource")]
        public MonoBehaviour inputSourceBehaviour;

        /// <summary>Current input source (replaceable, Phase 10 injection).</summary>
        public IPlayerInputSource InputSource { get; private set; }

        /// <summary>External input toggle (disable during pause/spectate, does not affect component).</summary>
        public bool InputEnabled { get; set; } = true;

        // Runtime state
        public bool IsGrounded { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float CurrentSpeed => new Vector3(Velocity.x, 0, Velocity.z).magnitude;

        CharacterController cc;
        float turnSmoothVel;
        Transform ownCamTransform; // Split-screen compat (T10-04): cache own camera to avoid Camera.main pointing to only one
        float coyoteTimer;
        float verticalVel;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            enabled = true;
            ResolveInputSource();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Non-local controlled objects: disable controller (position driven by NetworkTransform)
            // Note: use isOwned (authority) not isLocalPlayer — in local multiplayer, extra player objects
            // on the same connection are also locally controlled (isOwned=true but not isLocalPlayer).
            if (!isOwned)
            {
                enabled = false;
                if (cc != null) cc.enabled = false;
            }
            else
            {
                enabled = true;
                ResolveInputSource();
            }
        }

        void ResolveInputSource()
        {
            if (inputSourceBehaviour is IPlayerInputSource s) { InputSource = s; return; }
            InputSource = GetComponent<DefaultPlayerInputSource>();
        }

        /// <summary>External input source replacement (Phase 10 PlayerInputManager takeover entry point).</summary>
        public void SetInputSource(IPlayerInputSource source) => InputSource = source;

        void Update()
        {
            // Only locally controlled objects (isOwned: includes local multiplayer extra players) accept input
            if (!isOwned || !InputEnabled) return;
            InputSource?.Tick();

            GroundCheck();

            // ---- Horizontal movement (camera-relative direction; T10-04 split-screen compat: prefer own camera) ----
            var move = InputSource != null ? InputSource.Move : Vector2.zero;
            if (ownCamTransform == null)
            {
                var camCtl = GetComponent<PlayerCameraController>();
                if (camCtl != null && camCtl.isActiveAndEnabled && camCtl.CameraComponent != null)
                    ownCamTransform = camCtl.CameraComponent.transform;
            }
            Transform camT = ownCamTransform != null ? ownCamTransform : (Camera.main != null ? Camera.main.transform : null);
            Vector3 fwd = camT != null ? Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized : transform.forward;
            Vector3 right = camT != null ? Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized : transform.right;
            Vector3 targetDir = (fwd * move.y + right * move.x);
            if (targetDir.sqrMagnitude > 1f) targetDir.Normalize();

            Vector3 targetVel = targetDir * settings.moveSpeed;

            // Acceleration smoothing (time-independent: moveSpeed/accelerationTime per second, equally responsive at low frame rates)
            float maxDelta = settings.accelerationTime <= 0
                ? float.MaxValue
                : settings.moveSpeed / settings.accelerationTime * Time.deltaTime;
            Vector3 horiz = new Vector3(Velocity.x, 0, Velocity.z);
            horiz = Vector3.MoveTowards(horiz, targetVel, maxDelta);
            Velocity = new Vector3(horiz.x, Velocity.y, horiz.z);

            // Facing (turn toward movement direction when moving)
            if (targetDir.sqrMagnitude > 0.01f)
            {
                float targetAngle = Mathf.Atan2(targetDir.x, targetDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVel, settings.turnSmoothTime);
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }

            // ---- Jump & gravity ----
            if (IsGrounded)
            {
                coyoteTimer = settings.coyoteTime;
                if (verticalVel < 0) verticalVel = -1f; // Ground stick
                if (InputSource != null && InputSource.JumpPressed && coyoteTimer > 0)
                {
                    verticalVel = settings.jumpForce;
                    coyoteTimer = 0;
                }
            }
            else coyoteTimer -= Time.deltaTime;

            verticalVel += settings.gravity * Time.deltaTime;
            if (verticalVel < -40f) verticalVel = -40f; // Terminal velocity

            Velocity = new Vector3(Velocity.x, verticalVel, Velocity.z);

            // ---- Apply movement ----
            cc.Move(Velocity * Time.deltaTime);
        }

        void GroundCheck()
        {
            // Prefer CharacterController's built-in ground state (matches Move, most reliable)
            if (cc != null && cc.enabled)
            {
                IsGrounded = cc.isGrounded;
                if (IsGrounded) return;
            }
            // Fallback: SphereCast (CC disabled/edge cases)
            var origin = transform.position + Vector3.up * 0.1f;
            IsGrounded = Physics.SphereCast(origin, settings.groundCheckRadius, Vector3.down,
                out _, settings.groundCheckDistance + 0.1f, settings.groundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
