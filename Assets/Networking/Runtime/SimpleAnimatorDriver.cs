using UnityEngine;
using Unity.Netcode;
using Embervale.CameraSystem;
using Embervale.Game.Input;

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

        // Unarmed layer control so idle doesn't freeze upper body
        [Header("Unarmed Layer Control")]
        [SerializeField] private string unarmedLayerName = "UnarmedUpper";
        [SerializeField] private float unarmedFadeIn = 12f;   // weight per second
        [SerializeField] private float unarmedFadeOut = 8f;   // weight per second
        [SerializeField] private float unarmedLightDuration = 0.35f;
        [SerializeField] private float unarmedHeavyDuration = 0.7f;
        private int _unarmedLayer = -1;
        private float _unarmedWeight;
        private float _unarmedTimer;
        private PlayerInputBridge _input;
        private bool _loggedMissingAttackLight;
        private bool _loggedMissingAttackHeavy;

        private void Awake()
        {
            _anim = GetComponentInChildren<Animator>();
            _lastPos = transform.position;
            _input = GetComponent<PlayerInputBridge>();
            if (_anim != null)
            {
                _unarmedLayer = _anim.GetLayerIndex(unarmedLayerName);
                if (_unarmedLayer >= 0)
                {
                    _anim.SetLayerWeight(_unarmedLayer, 0f);
                    _unarmedWeight = 0f;
                    _unarmedTimer = 0f;
                }
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
                if (!_hasAttackLight && !_loggedMissingAttackLight)
                {
                    _loggedMissingAttackLight = true;
                    Debug.LogWarning($"[Embervale] {_anim.gameObject.name} Animator missing AttackLight Trigger.");
                }
                if (!_hasAttackHeavy && !_loggedMissingAttackHeavy)
                {
                    _loggedMissingAttackHeavy = true;
                    Debug.LogWarning($"[Embervale] {_anim.gameObject.name} Animator missing AttackHeavy Trigger.");
                }
                _hasIsAiming = _anim.HasParameterOfType(_isAimingHash, AnimatorControllerParameterType.Bool);
                _hasBowDraw = _anim.HasParameterOfType(_bowDrawHash, AnimatorControllerParameterType.Float);
                _hasBowFire = _anim.HasParameterOfType(_bowFireHash, AnimatorControllerParameterType.Trigger);
                _hasBowCancel = _anim.HasParameterOfType(_bowCancelHash, AnimatorControllerParameterType.Trigger);
                _hasIsBlocking = _anim.HasParameterOfType(_isBlockingHash, AnimatorControllerParameterType.Bool);
                _hasRoll = _anim.HasParameterOfType(_rollHash, AnimatorControllerParameterType.Trigger);
            }
        }

        private void OnEnable()
        {
            if (_input == null) _input = GetComponent<PlayerInputBridge>();
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
                var moveInputIS = _input != null ? Vector2.ClampMagnitude(_input.Move, 1f) : Vector2.zero;
                var moveInputLegacy = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                var moveInput = moveInputIS.sqrMagnitude > 0f ? moveInputIS : moveInputLegacy;
                var cam = PlayerCameraController.Current;
                Vector3 moveWS = Vector3.zero;
                if (cam != null)
                {
                    moveWS = cam.PlanarRight * moveInput.x + cam.PlanarForward * moveInput.y;
                }
                else
                {
                    moveWS = new Vector3(moveInput.x, 0f, moveInput.y);
                }
                var mag = moveWS.magnitude;
                desiredDirWS = mag > 0.001f ? moveWS.normalized : transform.forward;

                // Determine target speed tier (crouch/run/sprint)
                var ctrl = GetComponent<SimplePlayerController>();
                bool isCrouching = ctrl != null ? ctrl.IsCrouching : crouching;
                if (_input != null && _input.CrouchHeld) isCrouching = true;
                bool isSprinting = (_input != null && _input.SprintHeld) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
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
                var jumpPressed = (_input != null && _input.JumpPressedThisFrame) || Input.GetKeyDown(KeyCode.Space);
                var aimHeld = (_input != null && _input.AimHeld) || Input.GetMouseButton(1);
                var blockHeld = (_input != null && _input.BlockHeld) || Input.GetKey(KeyCode.Q);
                var rollPressed = (_input != null && _input.RollPressedThisFrame) || Input.GetKeyDown(KeyCode.LeftAlt);
                var attackPressedIs = _input != null && _input.AttackPressedThisFrame;
                var attackPressedMouse = Input.GetMouseButtonDown(0);
                var attackPressed = attackPressedIs || attackPressedMouse;
                var attackHeld = Input.GetMouseButton(0) || (_input != null && _input.AttackHeld);
                var attackReleased = Input.GetMouseButtonUp(0) || (_input != null && _input.AttackReleasedThisFrame);
                var cancelPressed = (_input != null && _input.CancelPressedThisFrame) || Input.GetKeyDown(KeyCode.Escape);

                // Jump (Space): pulse IsJumping while grounded
                if (_hasIsJumping && isGrounded)
                {
                    if (jumpPressed)
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
                    _anim.SetBool(_isAimingHash, aimHeld);
                    var equip = GetComponent<Embervale.Game.Combat.EquipmentState>();
                    if (equip != null && equip.IsAiming.Value != aimHeld) equip.IsAiming.Value = aimHeld;
                }

                // Block (Q hold)
                if (_hasIsBlocking)
                {
                    _anim.SetBool(_isBlockingHash, blockHeld);
                    var equip = GetComponent<Embervale.Game.Combat.EquipmentState>();
                    if (equip != null && equip.IsBlocking.Value != blockHeld) equip.IsBlocking.Value = blockHeld;
                }

                // Roll (LeftAlt press)
                if (_hasRoll && rollPressed)
                {
                    _anim.ResetTrigger(_rollHash);
                    _anim.SetTrigger(_rollHash);
                }

                // Unarmed mapping: tap = light, hold = heavy (only when not aiming)
                var equipState = GetComponent<Embervale.Game.Combat.EquipmentState>();
                bool isUnarmed = equipState == null || equipState.EquippedWeapon.Value == Embervale.Game.Combat.WeaponType.Unarmed;
                bool isAimingNow = _hasIsAiming && _anim.GetBool(_isAimingHash);
                if (isUnarmed && !isAimingNow)
                {
                    if (attackPressed)
                    {
                        _lmbHeld = true;
                        _lmbDownElapsed = 0f;
                        _heavyFired = false;
                        // Fire light immediately for responsive feel (escalates to heavy if held)
                        if (_hasAttackLight)
                        {
                            _anim.ResetTrigger(_attackLightHash);
                            _anim.SetTrigger(_attackLightHash);
                        }
                        var atkCtlDown = GetComponent<Embervale.Game.Combat.AttackController>();
                        var aimDown = transform.forward;
                        if (atkCtlDown != null && IsOwner) atkCtlDown.TryAttackServerRpc(Embervale.Game.Combat.AttackInputKind.Light, aimDown, 0f);
                        if (_unarmedLayer >= 0) _unarmedTimer = Mathf.Max(_unarmedTimer, unarmedLightDuration);
                    }
                    if (_lmbHeld)
                    {
                        _lmbDownElapsed += Time.deltaTime;
                        if (!_heavyFired && _lmbDownElapsed >= _heavyHoldSeconds && attackHeld)
                        {
                            // Fire heavy once when threshold passed
                            if (_hasAttackHeavy)
                            {
                                _anim.ResetTrigger(_attackHeavyHash);
                                _anim.SetTrigger(_attackHeavyHash);
                            }
                            var atkCtl = GetComponent<Embervale.Game.Combat.AttackController>();
                            var aim = transform.forward;
                            if (atkCtl != null && IsOwner) atkCtl.TryAttackServerRpc(Embervale.Game.Combat.AttackInputKind.Heavy, aim, 0f);
                            _heavyFired = true;
                            if (_unarmedLayer >= 0) _unarmedTimer = Mathf.Max(_unarmedTimer, unarmedHeavyDuration);
                        }
                        if (attackReleased)
                        {
                            _lmbHeld = false;
                        }
                    }
                }
                // Bow draw/fire when aiming
                if (_hasIsAiming && _anim.GetBool(_isAimingHash))
                {
                    if (_hasBowDraw)
                    {
                        if (attackHeld)
                        {
                            _bowCharge = Mathf.Clamp01(_bowCharge + Time.deltaTime / _bowMaxChargeTime);
                        }
                        else
                        {
                            _bowCharge = Mathf.MoveTowards(_bowCharge, 0f, Time.deltaTime * 2f);
                        }
                        _anim.SetFloat(_bowDrawHash, _bowCharge);
                    }
                    if (_hasBowFire && attackReleased)
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
                    if (_hasBowCancel && cancelPressed)
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
            // Drive unarmed layer weight
            if (_unarmedLayer >= 0)
            {
                if (_unarmedTimer > 0f) _unarmedTimer -= Time.deltaTime;
                float target = _unarmedTimer > 0f ? 1f : 0f;
                float rate = target > _unarmedWeight ? unarmedFadeIn : unarmedFadeOut;
                _unarmedWeight = Mathf.MoveTowards(_unarmedWeight, target, rate * Time.deltaTime);
                _anim.SetLayerWeight(_unarmedLayer, _unarmedWeight);
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
        // Unarmed heavy-hold detection
        private bool _lmbHeld;
        private bool _heavyFired;
        private float _lmbDownElapsed;
        [SerializeField] private float _heavyHoldSeconds = 0.35f;
        // (moved to top of class) duplicate definitions removed
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


