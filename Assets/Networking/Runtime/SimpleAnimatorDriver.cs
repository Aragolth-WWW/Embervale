using UnityEngine;
using Unity.Netcode;
using Embervale.CameraSystem;

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
        [Header("Jump / Combat Params (Synty packs)")]
        [SerializeField] private string isJumpingParam = "IsJumping"; // Bool
        [SerializeField] private string attackLightTriggerParam = "AttackLight"; // Trigger
        [SerializeField] private string attackHeavyTriggerParam = "AttackHeavy"; // Trigger
        [SerializeField] private string isAimingParam = "IsAiming"; // Bool (Bow)
        [SerializeField] private string bowDrawParam = "BowDraw";   // Float 0..1 (Bow)
        [SerializeField] private string bowFireTriggerParam = "BowFire"; // Trigger
        [SerializeField] private string bowCancelTriggerParam = "BowCancel"; // Trigger
        [SerializeField] private string isBlockingParam = "IsBlocking"; // Bool (Sword/Bow)
        [SerializeField] private string rollTriggerParam = "Roll"; // Trigger
        [SerializeField] private float speedDampTime = 0.1f; // Animator damping for MoveSpeed
        [SerializeField] private float movingThreshold = 0.1f;
        [SerializeField] private float strafeDampRate = 20f; // Synty _STRAFE_DIRECTION_DAMP_TIME ~20
        [SerializeField] private float rotationSmoothing = 10f;
        [SerializeField] private float speedChangeDamping = 10f; // match Synty sample feel
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
        private int _isJumpingHash;
        private int _attackLightHash;
        private int _attackHeavyHash;
        private int _isAimingHash;
        private int _bowDrawHash;
        private int _bowFireHash;
        private int _bowCancelHash;
        private int _isBlockingHash;
        private int _rollHash;
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
        private bool _hasIsJumping;
        private bool _hasAttackLight;
        private bool _hasAttackHeavy;
        private bool _hasIsAiming;
        private bool _hasBowDraw;
        private bool _hasBowFire;
        private bool _hasBowCancel;
        private bool _hasIsBlocking;
        private bool _hasRoll;

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
                _isJumpingHash = Animator.StringToHash(isJumpingParam);
                _attackLightHash = Animator.StringToHash(attackLightTriggerParam);
                _attackHeavyHash = Animator.StringToHash(attackHeavyTriggerParam);
                _isAimingHash = Animator.StringToHash(isAimingParam);
                _bowDrawHash = Animator.StringToHash(bowDrawParam);
                _bowFireHash = Animator.StringToHash(bowFireTriggerParam);
                _bowCancelHash = Animator.StringToHash(bowCancelTriggerParam);
                _isBlockingHash = Animator.StringToHash(isBlockingParam);
                _rollHash = Animator.StringToHash(rollTriggerParam);
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
                _hasIsJumping = _anim.HasParameterOfType(_isJumpingHash, AnimatorControllerParameterType.Bool);
                _hasAttackLight = _anim.HasParameterOfType(_attackLightHash, AnimatorControllerParameterType.Trigger);
                _hasAttackHeavy = _anim.HasParameterOfType(_attackHeavyHash, AnimatorControllerParameterType.Trigger);
                _hasIsAiming = _anim.HasParameterOfType(_isAimingHash, AnimatorControllerParameterType.Bool);
                _hasBowDraw = _anim.HasParameterOfType(_bowDrawHash, AnimatorControllerParameterType.Float);
                _hasBowFire = _anim.HasParameterOfType(_bowFireHash, AnimatorControllerParameterType.Trigger);
                _hasBowCancel = _anim.HasParameterOfType(_bowCancelHash, AnimatorControllerParameterType.Trigger);
                _hasIsBlocking = _anim.HasParameterOfType(_isBlockingHash, AnimatorControllerParameterType.Bool);
                _hasRoll = _anim.HasParameterOfType(_rollHash, AnimatorControllerParameterType.Trigger);
            }
        }

        private void Update()
        {
            if (_anim == null || _anim.runtimeAnimatorController == null) return;

            // Compute owner vs non-owner motion and desired direction
            var pos = transform.position;
            var delta = pos - _lastPos; delta.y = 0f;
            _lastPos = pos;

            Vector3 desiredDirWS;
            float desiredSpeed;

            // Prefer input-driven speed for owner to avoid FixedUpdate sampling spikes
            if (IsOwner)
            {
                var h = Input.GetAxisRaw("Horizontal");
                var v = Input.GetAxisRaw("Vertical");
                var cam = PlayerCameraController.Current;
                Vector3 moveWS = Vector3.zero;
                if (cam != null)
                {
                    moveWS = cam.PlanarRight * h + cam.PlanarForward * v;
                }
                else
                {
                    moveWS = new Vector3(h, 0f, v);
                }
                var mag = moveWS.magnitude;
                desiredDirWS = mag > 0.001f ? moveWS.normalized : transform.forward;

                // Determine target speed tier (crouch/run/sprint)
                var ctrl = GetComponent<SimplePlayerController>();
                bool isCrouching = ctrl != null ? ctrl.IsCrouching : crouching;
                bool isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                float tier = isCrouching ? walkSpeed : (isSprinting ? sprintSpeed : runSpeed);
                desiredSpeed = tier * Mathf.Clamp01(mag);
            }
            else
            {
                // For non-owners, estimate speed from transform delta and smooth via Animator damping
                var instantaneous = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                desiredSpeed = instantaneous;
                desiredDirWS = delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
            }

            // Ground check
            var rayStart = transform.position + Vector3.up * groundedOffset;
            var isGrounded = Physics.Raycast(rayStart, Vector3.down, groundedCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
            if (_hasGrounded) _anim.SetBool(_groundedHash, isGrounded);

            // Crouch state: read from SimplePlayerController if present, else use local flag
            var ctrlC = GetComponent<SimplePlayerController>();
            var isCrouchingFlag = ctrlC != null ? ctrlC.IsCrouching : crouching;
            if (_hasCrouch) _anim.SetBool(_crouchHash, isCrouchingFlag);

            // Speed smoothing like sample (velocity damping) then feed MoveSpeed
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, desiredSpeed, Mathf.Clamp01(speedChangeDamping * Time.deltaTime));
            if (_hasSpeed) _anim.SetFloat(_speedHash, _smoothedSpeed, speedDampTime, Time.deltaTime);
            var moving = _smoothedSpeed > movingThreshold;
            if (_hasHeld) _anim.SetBool(_heldHash, moving);

            if (_hasGait)
            {
                int gait;
                var runThreshold = (walkSpeed + runSpeed) * 0.5f;
                var sprintThreshold = (runSpeed + sprintSpeed) * 0.5f;
                if (_smoothedSpeed < 0.01f) gait = 0; // Idle
                else if (_smoothedSpeed < runThreshold) gait = 1; // Walk
                else if (_smoothedSpeed < sprintThreshold) gait = 2; // Run
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

            // Jump + Combat input (owner only)
            if (IsOwner)
            {
                // Jump (Space): pulse IsJumping while grounded
                if (_hasIsJumping && isGrounded)
                {
                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        _jumpTimer = _jumpPulseDuration;
                        _anim.SetBool(_isJumpingHash, true);
                    }
                    if (_jumpTimer > 0f)
                    {
                        _jumpTimer -= Time.deltaTime;
                        if (_jumpTimer <= 0f)
                        {
                            _anim.SetBool(_isJumpingHash, false);
                        }
                    }
                }

                // Aim (RMB hold)
                if (_hasIsAiming)
                {
                    var aiming = Input.GetMouseButton(1);
                    _anim.SetBool(_isAimingHash, aiming);
                    var equip = GetComponent<Embervale.Game.Combat.EquipmentState>();
                    if (equip != null && equip.IsAiming.Value != aiming) equip.IsAiming.Value = aiming;
                }

                // Block (Q hold)
                if (_hasIsBlocking)
                {
                    var blocking = Input.GetKey(KeyCode.Q);
                    _anim.SetBool(_isBlockingHash, blocking);
                    var equip = GetComponent<Embervale.Game.Combat.EquipmentState>();
                    if (equip != null && equip.IsBlocking.Value != blocking) equip.IsBlocking.Value = blocking;
                }

                // Roll (LeftAlt press)
                if (_hasRoll && Input.GetKeyDown(KeyCode.LeftAlt))
                {
                    _anim.ResetTrigger(_rollHash);
                    _anim.SetTrigger(_rollHash);
                }

                // Sword attacks (LMB press) / Heavy (LMB+Shift)
                if (!_hasIsAiming || (_hasIsAiming && !_anim.GetBool(_isAimingHash)))
                {
                    if (_hasAttackLight && Input.GetMouseButtonDown(0))
                    {
                        _anim.ResetTrigger(_attackLightHash);
                        _anim.SetTrigger(_attackLightHash);
                        var atkCtl = GetComponent<Embervale.Game.Combat.AttackController>();
                        var aim = transform.forward;
                        if (atkCtl != null && IsOwner) atkCtl.TryAttackServerRpc(Embervale.Game.Combat.AttackInputKind.Light, aim, 0f);
                    }
                    if (_hasAttackHeavy && (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftShift)))
                    {
                        _anim.ResetTrigger(_attackHeavyHash);
                        _anim.SetTrigger(_attackHeavyHash);
                        var atkCtl = GetComponent<Embervale.Game.Combat.AttackController>();
                        var aim = transform.forward;
                        if (atkCtl != null && IsOwner) atkCtl.TryAttackServerRpc(Embervale.Game.Combat.AttackInputKind.Heavy, aim, 0f);
                    }
                }

                // Bow draw/fire when aiming
                if (_hasIsAiming && _anim.GetBool(_isAimingHash))
                {
                    if (_hasBowDraw)
                    {
                        if (Input.GetMouseButton(0))
                        {
                            _bowCharge = Mathf.Clamp01(_bowCharge + Time.deltaTime / _bowMaxChargeTime);
                        }
                        else
                        {
                            _bowCharge = Mathf.MoveTowards(_bowCharge, 0f, Time.deltaTime * 2f);
                        }
                        _anim.SetFloat(_bowDrawHash, _bowCharge);
                    }
                    if (_hasBowFire && Input.GetMouseButtonUp(0))
                    {
                        var charge = _bowCharge;
                        _anim.ResetTrigger(_bowFireHash);
                        _anim.SetTrigger(_bowFireHash);
                        _bowCharge = 0f;
                        if (_hasBowDraw) _anim.SetFloat(_bowDrawHash, 0f);
                        var atkCtl = GetComponent<Embervale.Game.Combat.AttackController>();
                        var aim = transform.forward;
                        if (atkCtl != null && IsOwner) atkCtl.TryAttackServerRpc(Embervale.Game.Combat.AttackInputKind.Charged, aim, charge);
                    }
                    if (_hasBowCancel && Input.GetKeyDown(KeyCode.Escape))
                    {
                        _anim.ResetTrigger(_bowCancelHash);
                        _anim.SetTrigger(_bowCancelHash);
                        _bowCharge = 0f;
                        if (_hasBowDraw) _anim.SetFloat(_bowDrawHash, 0f);
                    }
                }
            }

            // Strafe direction using dot products vs character axes
            var forward = transform.forward; forward.y = 0f; forward.Normalize();
            var right = new Vector3(forward.z, 0f, -forward.x);
            if (moving)
            {
                var targetZ = Vector3.Dot(forward, desiredDirWS);
                var targetX = Vector3.Dot(right, desiredDirWS);
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
            if (_hasIsStopped) _anim.SetBool(_isStoppedHash, !moving && _smoothedSpeed < 0.5f);
            if (_hasIsStarting) _anim.SetBool(_isStartingHash, moving && _moveChangeTimer < 0.2f && _smoothedSpeed < 1f);

            // Strafing/Cam offset/ForwardStrafe (assume always strafing for our 3rd-person camera)
            if (_hasIsStrafing) _anim.SetFloat(_isStrafingHash, 1f);
            var currentCam = PlayerCameraController.Current;
            if (currentCam != null && IsOwner) // only local has a camera
            {
                var camFwd = currentCam.transform.forward; camFwd.y = 0f; camFwd.Normalize();
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
            var mvDir = moving ? desiredDirWS : forward;
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
        private float _smoothedSpeed;
        private float _jumpTimer;
        [SerializeField] private float _jumpPulseDuration = 0.2f;
        private float _bowCharge;
        [SerializeField] private float _bowMaxChargeTime = 1.0f;
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


