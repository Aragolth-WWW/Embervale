using Unity.Netcode;
using UnityEngine;
using Embervale.CameraSystem;
using Embervale.Animation;

namespace Embervale.Networking
{
    // Minimal server-authoritative movement: client sends input, server moves, NetworkTransform replicates.
    [RequireComponent(typeof(NetworkObject))]
    public class SimplePlayerController : NetworkBehaviour
    {
        [Header("Movement (Synty-calibrated m/s)")]
        [SerializeField] private float crouchSpeed = 1.4f; // Synty _walkSpeed
        [SerializeField] private float runSpeed = 2.5f;   // Synty _runSpeed
        [SerializeField] private float sprintSpeed = 7f;   // Synty _sprintSpeed
        [SerializeField] private float rotateSpeed = 360f;
        [Header("Grounding")]
        [SerializeField] private bool groundToSurface = true;
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private float groundRayStart = 0.5f;
        [SerializeField] private float groundRayLength = 5f;
        [SerializeField] private float groundYOffset = 0f; // adjust if pivot not at feet

        private Vector2 _lastInput;
        private bool _wantsSprint;
        private bool _wantsCrouch;
        private NetworkVariable<bool> _isCrouching = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsCrouching => _isCrouching.Value;
        private static int s_spawnIndex;

        public override void OnNetworkSpawn()
        {
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

            // Ensure animator driver exists so animations can react to movement when a controller is assigned
            var driver = GetComponent<SimpleAnimatorDriver>();
            if (driver == null) gameObject.AddComponent<SimpleAnimatorDriver>();

            // Ensure combat scaffolding exists
            var equip = GetComponent<Embervale.Game.Combat.EquipmentState>();
            if (equip == null) equip = gameObject.AddComponent<Embervale.Game.Combat.EquipmentState>();
            var attackCtl = GetComponent<Embervale.Game.Combat.AttackController>();
            if (attackCtl == null) attackCtl = gameObject.AddComponent<Embervale.Game.Combat.AttackController>();

            // Ensure simple foot IK (requires IK Pass enabled in Animator layer)
            var animForIk = GetComponentInChildren<Animator>();
            if (animForIk != null && animForIk.GetComponent<SimpleFootIK>() == null)
            {
                animForIk.gameObject.AddComponent<SimpleFootIK>();
            }

            // If the character has an Animator without a controller, emit a clear hint once.
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[Embervale] Player has an Animator but no Controller assigned (T-pose). Assign a Humanoid Animator Controller with Idle/Walk/Run, or import Synty/Mixamo animations.");
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
                var h = Input.GetAxisRaw("Horizontal");
                var v = Input.GetAxisRaw("Vertical");
                Vector2 input;
                var cam = PlayerCameraController.Current;
                if (cam != null)
                {
                    var dir = cam.PlanarRight * h + cam.PlanarForward * v;
                    if (dir.sqrMagnitude > 1f) dir.Normalize();
                    input = new Vector2(dir.x, dir.z);
                }
                else
                {
                    input = new Vector2(h, v);
                }

                var wantsSprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                var wantsCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (input != _lastInput || wantsSprint != _wantsSprint || wantsCrouch != _wantsCrouch)
                {
                    _lastInput = input;
                    _wantsSprint = wantsSprint;
                    _wantsCrouch = wantsCrouch;
                    SubmitInputServerRpc(input, wantsSprint, wantsCrouch);
                    if (IsServer)
                    {
                        // When hosting, the ServerRpc executes locally but also
                        // update immediately to keep local responsiveness if needed.
                        _lastInput = input;
                        _wantsSprint = wantsSprint;
                        _wantsCrouch = wantsCrouch;
                        _isCrouching.Value = wantsCrouch;
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            var dir = new Vector3(_lastInput.x, 0, _lastInput.y);
            if (dir.sqrMagnitude > 0.0001f)
            {
                var speed = _wantsCrouch ? crouchSpeed : (_wantsSprint ? sprintSpeed : runSpeed);
                transform.position += dir.normalized * speed * Time.fixedDeltaTime;
                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime);
            }
            if (groundToSurface)
            {
                var start = transform.position + Vector3.up * groundRayStart;
                if (Physics.Raycast(start, Vector3.down, out var hit, groundRayLength, groundLayers, QueryTriggerInteraction.Ignore))
                {
                    var p = transform.position; p.y = hit.point.y + groundYOffset; transform.position = p;
                }
            }
        }

        [ServerRpc]
        private void SubmitInputServerRpc(Vector2 input, bool wantsSprint, bool wantsCrouch)
        {
            _lastInput = Vector2.ClampMagnitude(input, 1f);
            _wantsSprint = wantsSprint;
            _wantsCrouch = wantsCrouch;
            _isCrouching.Value = wantsCrouch;
        }
    }
}
