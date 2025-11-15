using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering.Universal;
using Embervale.Game.Input;

namespace Embervale.CameraSystem
{
    // Local-only camera that can switch between FPS and TPS.
    // Works without Cinemachine to keep dependencies minimal.
    public class PlayerCameraController : NetworkBehaviour
    {
        public static PlayerCameraController Current { get; private set; }

        [Header("General")]
        [SerializeField] private Transform followTarget; // set to player root if null
        [SerializeField] private bool startInFirstPerson = true;
        [SerializeField] private bool hideBodyInFirstPerson = true;

        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float clampPitchMin = -80f;
        [SerializeField] private float clampPitchMax = 80f;

        [Header("First Person")]
        [SerializeField] private float fpsHeight = 1.7f;

        [Header("Third Person")]
        [SerializeField] private float tpsDistance = 4.0f;
        [SerializeField] private float tpsHeight = 1.7f;
        [SerializeField] private float tpsShoulder = 0.5f;
        [Header("Camera Collision")]
        [SerializeField] private LayerMask cameraCollisionMask = ~0;
        [SerializeField] private float cameraCollisionRadius = 0.2f;
        [SerializeField] private float cameraCollisionBuffer = 0.1f;
        [SerializeField] private float cameraCollisionSmoothing = 10f;

        private Camera _cam;
        private Transform _camTransform;
        private float _yaw;
        private float _pitch;
        private bool _isFirstPerson;
        private SkinnedMeshRenderer[] _renderers;
        private PlayerInputBridge _input;
        private float _currentTpsDistance;

        public Vector3 PlanarForward
        {
            get
            {
                if (_camTransform == null) return Vector3.forward;
                var f = _camTransform.forward; f.y = 0f; return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
            }
        }
        public Vector3 PlanarRight
        {
            get
            {
                var f = PlanarForward; return new Vector3(f.z, 0, -f.x);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            Current = this;
            if (followTarget == null) followTarget = transform;
            _input = GetComponent<PlayerInputBridge>();
            _currentTpsDistance = tpsDistance;
            EnsureRig();
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
            if (_cam != null) Destroy(_cam.gameObject);
            _input = null;
        }

        private void Update()
        {
            if (!IsOwner || _camTransform == null) return;

            // Toggle view
            if (Input.GetKeyDown(KeyCode.V)) _isFirstPerson = !_isFirstPerson;

            // Cursor lock toggle
            if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            var aimHeld = _input != null ? _input.AimHeld : Input.GetMouseButton(1);
            if (aimHeld && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (_input == null && Input.GetMouseButtonDown(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Mouse look
            var look = _input != null
                ? _input.Look
                : new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            const float mouseDeltaScalar = 0.25f;
            var dx = look.x * mouseSensitivity * mouseDeltaScalar;
            var dy = look.y * mouseSensitivity * mouseDeltaScalar;
            _yaw += dx;
            _pitch = Mathf.Clamp(_pitch - dy, clampPitchMin, clampPitchMax);
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            if (_camTransform == null) { EnsureRig(); if (_camTransform == null) return; }
            if (followTarget == null) followTarget = transform;

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);

            if (_isFirstPerson)
            {
                _camTransform.position = followTarget.position + Vector3.up * fpsHeight;
                _camTransform.rotation = rot;
            }
            else
            {
                var pivot = followTarget.position + Vector3.up * tpsHeight + followTarget.right * tpsShoulder;
                var desiredOffset = rot * new Vector3(0, 0, -tpsDistance);
                var safeOffset = ResolveCameraCollision(pivot, desiredOffset);
                var targetDistance = safeOffset.magnitude;
                if (targetDistance > 0.001f)
                {
                    _currentTpsDistance = Mathf.Lerp(_currentTpsDistance, targetDistance, Time.deltaTime * cameraCollisionSmoothing);
                    safeOffset = safeOffset.normalized * _currentTpsDistance;
                }
                else
                {
                    _currentTpsDistance = 0f;
                }
                _camTransform.position = pivot + safeOffset;
                _camTransform.rotation = rot;
            }

            if (hideBodyInFirstPerson && _renderers != null)
            {
                var hide = _isFirstPerson;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) _renderers[i].enabled = !hide;
                }
            }
        }

        public void EnsureSetup()
        {
            EnsureRig();
        }

        private void EnsureRig()
        {
            // Create the camera rig if missing (works even if added after spawn)
            if (_camTransform != null) return;
            if (followTarget == null) followTarget = transform;

            var go = new GameObject("PlayerCamera");
            go.transform.SetParent(followTarget, false);
            _cam = go.AddComponent<Camera>();
            if (go.GetComponent<UniversalAdditionalCameraData>() == null)
                go.AddComponent<UniversalAdditionalCameraData>();
            var myListener = go.AddComponent<AudioListener>();
            _camTransform = go.transform;
            Debug.Log($"[Embervale] PlayerCamera created under {followTarget.name}");

            _isFirstPerson = startInFirstPerson;
            var euler = followTarget.eulerAngles; _yaw = euler.y; _pitch = 0f;

            var others = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in others)
            {
                if (c != _cam) c.enabled = false;
            }
            // Ensure exactly one active AudioListener (ours)
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var l in listeners)
            {
                if (l != myListener) l.enabled = false;
            }

            if (hideBodyInFirstPerson)
            {
                _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredOffset)
        {
            var desiredDistance = desiredOffset.magnitude;
            if (desiredDistance < 0.001f) return desiredOffset;
            var dir = desiredOffset / desiredDistance;
            if (Physics.SphereCast(pivot, cameraCollisionRadius, dir, out var hit, desiredDistance, cameraCollisionMask, QueryTriggerInteraction.Ignore))
            {
                // Don't collide with our own hierarchy
                if (!hit.collider.transform.IsChildOf(transform))
                {
                    var clippedDistance = Mathf.Max(0f, hit.distance - cameraCollisionBuffer);
                    return dir * clippedDistance;
                }
            }
            return desiredOffset;
        }
    }
}
