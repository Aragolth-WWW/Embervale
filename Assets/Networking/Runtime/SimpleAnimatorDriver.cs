using UnityEngine;
using Unity.Netcode;

namespace Embervale.Networking
{
    // Feeds Synty Base Locomotion Animator parameters based on motion.
    // Matches parameters found in AC_Polygon_[Masculine/Feminine].controller:
    //   - Float "MoveSpeed"
    //   - Float "StrafeDirectionX", Float "StrafeDirectionZ"
    //   - Bool  "IsGrounded", Bool "IsCrouching"
    //   - Bool  "MovementInputHeld" (Tapped/Pressed optional)
    public class SimpleAnimatorDriver : NetworkBehaviour
    {
        [SerializeField] private string speedParam = "MoveSpeed";
        [SerializeField] private string strafeXParam = "StrafeDirectionX";
        [SerializeField] private string strafeZParam = "StrafeDirectionZ";
        [SerializeField] private string groundedParam = "IsGrounded";
        [SerializeField] private string crouchParam = "IsCrouching";
        [SerializeField] private string heldParam = "MovementInputHeld";
        [SerializeField] private string pressedParam = "MovementInputPressed";
        [SerializeField] private string tappedParam = "MovementInputTapped";
        [SerializeField] private string isWalkingParam = "IsWalking";
        [SerializeField] private string isStoppedParam = "IsStopped";
        [SerializeField] private string isStartingParam = "IsStarting";
        [SerializeField] private string isStrafingParam = "IsStrafing"; // float 0..1 in Synty
        [SerializeField] private string forwardStrafeParam = "ForwardStrafe"; // float 0..1
        [SerializeField] private string cameraRotOffsetParam = "CameraRotationOffset"; // float degrees
        [SerializeField] private float speedDampTime = 0.1f; // Animator damping for MoveSpeed
        [SerializeField] private float movingThreshold = 0.1f;
        [SerializeField] private float strafeDampRate = 20f; // Synty _STRAFE_DIRECTION_DAMP_TIME ~20
        [SerializeField] private float rotationSmoothing = 10f;
        [SerializeField] private float forwardStrafeMinThreshold = -55f;
        [SerializeField] private float forwardStrafeMaxThreshold = 125f;
        [SerializeField] private float buttonHoldThreshold = 0.15f;
        [Header("Speed thresholds (m/s) to match Synty sample")]
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float runSpeed = 2.5f;
        [SerializeField] private float sprintSpeed = 7f;
        [Header("Grounding (for IsGrounded)")]
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private float groundedOffset = 0.1f;
        [SerializeField] private float groundedCheckDistance = 0.3f;
        [SerializeField] private bool grounded = true;
        [SerializeField] private bool crouching = false;

        private Animator _anim;
        private Vector3 _lastPos;
        private int _speedHash;
        private int _strafeXHash;
        private int _strafeZHash;
        private int _groundedHash;
        private int _crouchHash;
        private int _heldHash;
        private int _gaitHash;
        private int _pressedHash;
        private int _tappedHash;
        private int _isWalkingHash;
        private int _isStoppedHash;
        private int _isStartingHash;
        private int _isStrafingHash;
        private int _forwardStrafeHash;
        private int _cameraRotOffsetHash;
        private bool _hasSpeed;
        private bool _hasStrafeX;
        private bool _hasStrafeZ;
        private bool _hasGrounded;
        private bool _hasCrouch;
        private bool _hasHeld;
        private bool _hasGait;
        private bool _hasPressed;
        private bool _hasTapped;
        private bool _hasIsWalking;
        private bool _hasIsStopped;
        private bool _hasIsStarting;
        private bool _hasIsStrafing;
        private bool _hasForwardStrafe;
        private bool _hasCameraRotOffset;

        private void Awake()
        {
            _anim = GetComponentInChildren<Animator>();
            _lastPos = transform.position;
            if (_anim != null)
            {
                _speedHash = Animator.StringToHash(speedParam);
                _strafeXHash = Animator.StringToHash(strafeXParam);
                _strafeZHash = Animator.StringToHash(strafeZParam);
                _groundedHash = Animator.StringToHash(groundedParam);
                _crouchHash = Animator.StringToHash(crouchParam);
                _heldHash = Animator.StringToHash(heldParam);
                _gaitHash = Animator.StringToHash("CurrentGait");
                _pressedHash = Animator.StringToHash(pressedParam);
                _tappedHash = Animator.StringToHash(tappedParam);
                _isWalkingHash = Animator.StringToHash(isWalkingParam);
                _isStoppedHash = Animator.StringToHash(isStoppedParam);
                _isStartingHash = Animator.StringToHash(isStartingParam);
                _isStrafingHash = Animator.StringToHash(isStrafingParam);
                _forwardStrafeHash = Animator.StringToHash(forwardStrafeParam);
                _cameraRotOffsetHash = Animator.StringToHash(cameraRotOffsetParam);
                _hasSpeed = _anim.HasParameterOfType(_speedHash, AnimatorControllerParameterType.Float);
                _hasStrafeX = _anim.HasParameterOfType(_strafeXHash, AnimatorControllerParameterType.Float);
                _hasStrafeZ = _anim.HasParameterOfType(_strafeZHash, AnimatorControllerParameterType.Float);
                _hasGrounded = _anim.HasParameterOfType(_groundedHash, AnimatorControllerParameterType.Bool);
                _hasCrouch = _anim.HasParameterOfType(_crouchHash, AnimatorControllerParameterType.Bool);
                _hasHeld = _anim.HasParameterOfType(_heldHash, AnimatorControllerParameterType.Bool);
                _hasGait = _anim.HasParameterOfType(_gaitHash, AnimatorControllerParameterType.Int);
                _hasPressed = _anim.HasParameterOfType(_pressedHash, AnimatorControllerParameterType.Bool);
                _hasTapped = _anim.HasParameterOfType(_tappedHash, AnimatorControllerParameterType.Bool);
                _hasIsWalking = _anim.HasParameterOfType(_isWalkingHash, AnimatorControllerParameterType.Bool);
                _hasIsStopped = _anim.HasParameterOfType(_isStoppedHash, AnimatorControllerParameterType.Bool);
                _hasIsStarting = _anim.HasParameterOfType(_isStartingHash, AnimatorControllerParameterType.Bool);
                _hasIsStrafing = _anim.HasParameterOfType(_isStrafingHash, AnimatorControllerParameterType.Float);
                _hasForwardStrafe = _anim.HasParameterOfType(_forwardStrafeHash, AnimatorControllerParameterType.Float);
                _hasCameraRotOffset = _anim.HasParameterOfType(_cameraRotOffsetHash, AnimatorControllerParameterType.Float);
            }
        }

        private void Update()
        {
            if (_anim == null || _anim.runtimeAnimatorController == null) return;

            // Compute planar motion in world space
            var pos = transform.position;
            var delta = pos - _lastPos; delta.y = 0f;
            var speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPos = pos;

            // Ground check
            var rayStart = transform.position + Vector3.up * groundedOffset;
            var isGrounded = Physics.Raycast(rayStart, Vector3.down, groundedCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
            if (_hasGrounded) _anim.SetBool(_groundedHash, isGrounded);

            // Crouch state: read from SimplePlayerController if present, else use local flag
            var ctrl = GetComponent<SimplePlayerController>();
            var isCrouching = ctrl != null ? ctrl.IsCrouching : crouching;
            if (_hasCrouch) _anim.SetBool(_crouchHash, isCrouching);

            // Speed + gait
            if (_hasSpeed) _anim.SetFloat(_speedHash, speed, speedDampTime, Time.deltaTime);
            var moving = speed > movingThreshold;
            if (_hasHeld) _anim.SetBool(_heldHash, moving);

            if (_hasGait)
            {
                int gait;
                var runThreshold = (walkSpeed + runSpeed) * 0.5f;
                var sprintThreshold = (runSpeed + sprintSpeed) * 0.5f;
                if (speed < 0.01f) gait = 0; // Idle
                else if (speed < runThreshold) gait = 1; // Walk
                else if (speed < sprintThreshold) gait = 2; // Run
                else gait = 3; // Sprint
                _anim.SetInteger(_gaitHash, gait);
                if (_hasIsWalking) _anim.SetBool(_isWalkingHash, gait == 1);
            }

            // Movement input cadence (Tapped/Pressed/Held)
            _moveChangeTimer += Time.deltaTime;
            if (moving != _wasMoving)
            {
                _wasMoving = moving;
                _moveChangeTimer = 0f;
            }
            if (_hasPressed) _anim.SetBool(_pressedHash, moving && _moveChangeTimer > 0f && _moveChangeTimer < buttonHoldThreshold);
            if (_hasTapped) _anim.SetBool(_tappedHash, moving && _moveChangeTimer == 0f);

            // Strafe direction using dot products vs character axes
            var forward = transform.forward; forward.y = 0f; forward.Normalize();
            var right = new Vector3(forward.z, 0f, -forward.x);
            if (moving)
            {
                var dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.zero;
                var targetZ = Vector3.Dot(forward, dir);
                var targetX = Vector3.Dot(right, dir);
                var t = Mathf.Clamp01(strafeDampRate * Time.deltaTime);
                if (_hasStrafeZ) _anim.SetFloat(_strafeZHash, Mathf.Lerp(_anim.GetFloat(_strafeZHash), targetZ, t));
                if (_hasStrafeX) _anim.SetFloat(_strafeXHash, Mathf.Lerp(_anim.GetFloat(_strafeXHash), targetX, t));
                _shuffleZ = targetZ; _shuffleX = targetX; // immediate update like sample
            }
            else
            {
                // keep last shuffle values; strafe blend tree wants non-zero forward when idle
                if (_hasStrafeZ) _anim.SetFloat(_strafeZHash, Mathf.Lerp(_anim.GetFloat(_strafeZHash), 1f, Mathf.Clamp01(strafeDampRate * Time.deltaTime)));
                if (_hasStrafeX) _anim.SetFloat(_strafeXHash, Mathf.Lerp(_anim.GetFloat(_strafeXHash), 0f, Mathf.Clamp01(strafeDampRate * Time.deltaTime)));
            }

            // IsStopped / IsStarting
            if (_hasIsStopped) _anim.SetBool(_isStoppedHash, !moving && speed < 0.5f);
            if (_hasIsStarting) _anim.SetBool(_isStartingHash, moving && _moveChangeTimer < 0.2f && speed < 1f);

            // Strafing/Cam offset/ForwardStrafe (assume always strafing for our 3rd-person camera)
            if (_hasIsStrafing) _anim.SetFloat(_isStrafingHash, 1f);
            var cam = Embervale.CameraSystem.PlayerCameraController.Current;
            if (cam != null && IsOwner) // only local has a camera
            {
                var camFwd = cam.transform.forward; camFwd.y = 0f; camFwd.Normalize();
                var characterForward = forward;
                if (moving)
                {
                    // While moving, bias offset to 0
                    if (_hasCameraRotOffset) _anim.SetFloat(_cameraRotOffsetHash, Mathf.Lerp(_anim.GetFloat(_cameraRotOffsetHash), 0f, rotationSmoothing * Time.deltaTime));
                }
                else
                {
                    var offset = Vector3.SignedAngle(characterForward, camFwd, Vector3.up);
                    if (_hasCameraRotOffset) _anim.SetFloat(_cameraRotOffsetHash, Mathf.Lerp(_anim.GetFloat(_cameraRotOffsetHash), offset, rotationSmoothing * Time.deltaTime));
                    if (_hasIsStarting) _anim.SetBool(_isStartingHash, Mathf.Abs(offset) > 10f);
                }
            }
            // ForwardStrafe based on strafe angle
            var mvDir = moving ? (delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward) : forward;
            var strafeAngle = Vector3.SignedAngle(forward, mvDir, Vector3.up);
            if (_hasForwardStrafe)
            {
                var target = (strafeAngle > forwardStrafeMinThreshold && strafeAngle < forwardStrafeMaxThreshold) ? 1f : 0f;
                var cur = _anim.GetFloat(_forwardStrafeHash);
                var t = Mathf.Clamp01(strafeDampRate * Time.deltaTime);
                var next = Mathf.Abs(cur - target) <= 0.001f ? target : Mathf.SmoothStep(cur, target, t);
                _anim.SetFloat(_forwardStrafeHash, next);
            }
        }

        private bool _wasMoving;
        private float _moveChangeTimer;
        private float _shuffleX;
        private float _shuffleZ;
    }

    internal static class AnimatorExtensions
    {
        public static bool HasParameterOfType(this Animator self, int id, AnimatorControllerParameterType type)
        {
            if (self == null || self.runtimeAnimatorController == null) return false;
            foreach (var p in self.parameters)
            {
                if (p.type == type && p.nameHash == id) return true;
            }
            return false;
        }
    }
}
