using UnityEngine;
using Unity.Netcode;

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

        private Camera _cam;
        private Transform _camTransform;
        private float _yaw;
        private float _pitch;
        private bool _isFirstPerson;

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

            // Create/attach camera
            var go = new GameObject("PlayerCamera");
            go.transform.SetParent(followTarget, false);
            _cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            _camTransform = go.transform;

            _isFirstPerson = startInFirstPerson;
            var euler = followTarget.eulerAngles;
            _yaw = euler.y; _pitch = 0f;

            // Disable other cameras to ensure we see through the player camera
            var others = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in others)
            {
                if (c != _cam) c.enabled = false;
            }

            // Lock cursor for look; toggle with Esc/Right Mouse
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
            if (_cam != null) Destroy(_cam.gameObject);
        }

        private void Update()
        {
            if (!IsOwner || _camTransform == null) return;

            // Toggle view
            if (Input.GetKeyDown(KeyCode.V)) _isFirstPerson = !_isFirstPerson;

            // Cursor lock toggle
            if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Input.GetMouseButtonDown(1)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            // Mouse look
            var dx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            var dy = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            _yaw += dx;
            _pitch = Mathf.Clamp(_pitch - dy, clampPitchMin, clampPitchMax);
        }

        private void LateUpdate()
        {
            if (!IsOwner || _camTransform == null || followTarget == null) return;

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);

            if (_isFirstPerson)
            {
                _camTransform.position = followTarget.position + Vector3.up * fpsHeight;
                _camTransform.rotation = rot;
            }
            else
            {
                var pivot = followTarget.position + Vector3.up * tpsHeight + followTarget.right * tpsShoulder;
                var offset = rot * new Vector3(0, 0, -tpsDistance);
                _camTransform.position = pivot + offset;
                _camTransform.rotation = rot;
            }
        }
    }
}
