using Unity.Netcode;
using UnityEngine;
using Embervale.CameraSystem;
using Embervale.Animation;
using Embervale.Game.Input;

namespace Embervale.Networking
{
    // Minimal server-authoritative movement: client sends input, server moves, NetworkTransform replicates.
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerInputBridge))]
    [RequireComponent(typeof(SimpleAnimatorDriver))]
    [RequireComponent(typeof(CharacterController))]
    public class SimplePlayerController : NetworkBehaviour
    {
        [Header("Movement (Synty-calibrated m/s)")]
        [SerializeField] private float crouchSpeed = 1.4f; // Synty _walkSpeed
        [SerializeField] private float runSpeed = 2.5f;   // Synty _runSpeed
        [SerializeField] private float sprintSpeed = 7f;   // Synty _sprintSpeed
        [SerializeField] private float rotateSpeed = 360f;
        [Header("Character Controller")]
        [SerializeField] private float controllerRadius = 0.4f;
        [SerializeField] private float controllerHeight = 1.8f;
        [SerializeField] private float controllerCenterY = 0.9f;
        [SerializeField] private float controllerStepOffset = 0.3f;
        [SerializeField] private float controllerSlopeLimit = 60f;
        [Header("Jump & Gravity")]
        [SerializeField, Tooltip("Meters above takeoff point the feet should reach at the jump apex.")]
        private float jumpApexHeight = 0.9f;
        [SerializeField, Tooltip("Negative value applied every second.")]
        private float gravity = -30f;
        [Header("Jump & Gravity")]
        private Vector2 _lastInput;
        private bool _wantsSprint;
        private bool _wantsCrouch;
        private NetworkVariable<bool> _isCrouching = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private PlayerInputBridge _input;
        private CharacterController _character;
        private float _verticalVelocity;
        private bool _jumpQueued;
        private bool _isGrounded = true;

        public bool IsCrouching => _isCrouching.Value;
        private static int s_spawnIndex;

        private void Awake()
        {
            _input = GetComponent<PlayerInputBridge>();
            EnsureCharacterController();
            EnsureAnimatorDriver();
            EnsureCombatComponents();
            EnsureFootIk();
            WarnIfAnimatorMissingController();
        }

        public override void OnNetworkSpawn()
        {
            if (_input == null) _input = GetComponent<PlayerInputBridge>();

            if (IsOwner)
            {
                Debug.Log($"[Embervale] Local player spawned: {gameObject.name}");
                var camCtrl = GetComponent<Embervale.CameraSystem.PlayerCameraController>();
                if (camCtrl == null)
                {
                    camCtrl = gameObject.AddComponent<Embervale.CameraSystem.PlayerCameraController>();
                }
                // Ensure rig even if added post-spawn
                var ensureRig = camCtrl as MonoBehaviour;
                if (ensureRig != null)
                {
                    // call via reflection-safe method name if available
                    var m = camCtrl.GetType().GetMethod("EnsureRig", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (m != null) m.Invoke(camCtrl, null);
                }
            }

            if (IsServer)
            {
                Debug.Log($"[Embervale] Server sees player object: {gameObject.name} for client {OwnerClientId}");
                // Spawn players around a circle so we don't overlap scene geometry at origin
                var radius = 5f;
                var height = 1.5f;
                var idx = s_spawnIndex++;
                var angle = (idx % 8) * Mathf.PI * 2f / 8f;
                var pos = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, -Mathf.Rad2Deg * angle, 0f);
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                Vector2 rawInput;
                if (_input != null)
                {
                    rawInput = Vector2.ClampMagnitude(_input.Move, 1f);
                }
                else
                {
                    rawInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                }

                Vector2 input;
                var cam = PlayerCameraController.Current;
                if (cam != null)
                {
                    var dir = cam.PlanarRight * rawInput.x + cam.PlanarForward * rawInput.y;
                    if (dir.sqrMagnitude > 1f) dir.Normalize();
                    input = new Vector2(dir.x, dir.z);
                }
                else
                {
                    input = rawInput;
                }

                var wantsSprint = _input != null ? _input.SprintHeld : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                var wantsCrouch = _input != null ? _input.CrouchHeld : (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
                var jumpPressed = _input != null ? _input.JumpPressedThisFrame : Input.GetKeyDown(KeyCode.Space);
                if (input != _lastInput || wantsSprint != _wantsSprint || wantsCrouch != _wantsCrouch || jumpPressed)
                {
                    _lastInput = input;
                    _wantsSprint = wantsSprint;
                    _wantsCrouch = wantsCrouch;
                    SubmitInputServerRpc(input, wantsSprint, wantsCrouch, jumpPressed);
                    if (IsServer)
                    {
                        // When hosting, the ServerRpc executes locally but also
                        // update immediately to keep local responsiveness if needed.
                        _lastInput = input;
                        _wantsSprint = wantsSprint;
                        _wantsCrouch = wantsCrouch;
                        _isCrouching.Value = wantsCrouch;
                        if (jumpPressed)
                        {
                            QueueJumpRequest();
                        }
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            EnsureCharacterController();
            if (_character == null) return;

            var dt = Time.fixedDeltaTime;
            var inputDir = new Vector3(_lastInput.x, 0f, _lastInput.y);
            Vector3 moveDir = inputDir.sqrMagnitude > 0.0001f ? inputDir.normalized : Vector3.zero;
            if (moveDir.sqrMagnitude > 0f)
            {
                var targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * dt);
            }

            var speed = _wantsCrouch ? crouchSpeed : (_wantsSprint ? sprintSpeed : runSpeed);
            var planarVelocity = moveDir * speed;

            if (_character.isGrounded)
            {
                _isGrounded = true;
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            }
            else
            {
                _isGrounded = false;
            }

            if (_jumpQueued && _isGrounded)
            {
                _verticalVelocity = ComputeJumpVelocity();
                _isGrounded = false;
                _jumpQueued = false;
            }

            _verticalVelocity += gravity * dt;

            var motion = planarVelocity * dt;
            motion.y = _verticalVelocity * dt;

            var flags = _character.Move(motion);

            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }

            if ((flags & CollisionFlags.Below) != 0 || _character.isGrounded)
            {
                _isGrounded = true;
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            }
        }

        [ServerRpc]
        private void SubmitInputServerRpc(Vector2 input, bool wantsSprint, bool wantsCrouch, bool jumpPressed)
        {
            _lastInput = Vector2.ClampMagnitude(input, 1f);
            _wantsSprint = wantsSprint;
            _wantsCrouch = wantsCrouch;
            _isCrouching.Value = wantsCrouch;
            if (jumpPressed)
            {
                QueueJumpRequest();
            }
        }

        private void EnsureAnimatorDriver()
        {
            if (GetComponent<SimpleAnimatorDriver>() == null)
            {
                gameObject.AddComponent<SimpleAnimatorDriver>();
            }
        }

        private void EnsureCombatComponents()
        {
            if (GetComponent<Embervale.Game.Combat.EquipmentState>() == null)
            {
                gameObject.AddComponent<Embervale.Game.Combat.EquipmentState>();
            }

            if (GetComponent<Embervale.Game.Combat.AttackController>() == null)
            {
                gameObject.AddComponent<Embervale.Game.Combat.AttackController>();
            }
        }

        private void EnsureFootIk()
        {
            var animForIk = GetComponentInChildren<Animator>();
            if (animForIk != null && animForIk.GetComponent<SimpleFootIK>() == null)
            {
                animForIk.gameObject.AddComponent<SimpleFootIK>();
            }
        }

        private void WarnIfAnimatorMissingController()
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[Embervale] Player has an Animator but no Controller assigned (T-pose). Assign a Humanoid Animator Controller with Idle/Walk/Run, or import Synty/Mixamo animations.");
            }
        }

        private void QueueJumpRequest()
        {
            if (_isGrounded)
            {
                _jumpQueued = true;
            }
        }

        private float ComputeJumpVelocity()
        {
            return Mathf.Sqrt(Mathf.Max(0f, 2f * jumpApexHeight * -gravity));
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                _character = GetComponent<CharacterController>();
                ConfigureCharacterController();
            }
        }
#endif

        private void EnsureCharacterController()
        {
            if (_character == null)
            {
                _character = GetComponent<CharacterController>() ?? gameObject.AddComponent<CharacterController>();
            }
            ConfigureCharacterController();
        }

        private void ConfigureCharacterController()
        {
            if (_character == null) return;
            _character.radius = controllerRadius;
            _character.height = controllerHeight;
            _character.stepOffset = controllerStepOffset;
            _character.slopeLimit = controllerSlopeLimit;
            var center = _character.center;
            center.y = controllerCenterY;
            _character.center = center;
        }
    }
}
