using Unity.Netcode;
using UnityEngine;
using Embervale.CameraSystem;

namespace Embervale.Networking
{
    // Minimal server-authoritative movement: client sends input, server moves, NetworkTransform replicates.
    [RequireComponent(typeof(NetworkObject))]
    public class SimplePlayerController : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotateSpeed = 360f;

        private Vector2 _lastInput;
        private static int s_spawnIndex;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
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

                if (input != _lastInput)
                {
                    _lastInput = input;
                    SubmitInputServerRpc(input);
                    if (IsServer)
                    {
                        // When hosting, the ServerRpc executes locally but also
                        // update immediately to keep local responsiveness if needed.
                        _lastInput = input;
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
                transform.position += dir.normalized * moveSpeed * Time.fixedDeltaTime;
                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime);
            }
        }

        [ServerRpc]
        private void SubmitInputServerRpc(Vector2 input)
        {
            _lastInput = Vector2.ClampMagnitude(input, 1f);
        }
    }
}
