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

        private void Update()
        {
            if (IsOwner && !IsServer)
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
