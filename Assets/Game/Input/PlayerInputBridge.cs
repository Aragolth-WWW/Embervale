using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Embervale.Game.Input
{
    /// <summary>
    /// Centralises Unity Input System actions for a networked player and exposes
    /// sampled values that other components (movement, animation, camera) can query.
    /// Only the owning client has the action map enabled.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PlayerInputBridge : NetworkBehaviour
    {
        private const string DefaultResource = "InputSystem_Actions";

        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string fallbackResourcePath = DefaultResource;

        private InputActionAsset _runtimeAsset;
        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _attackAction;
        private InputAction _crouchAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _aimAction;
        private InputAction _blockAction;
        private InputAction _rollAction;
        private InputAction _cancelAction;
        private bool _actionsEnabled;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool AttackPressedThisFrame { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool AttackReleasedThisFrame { get; private set; }
        public bool AimHeld { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool RollPressedThisFrame { get; private set; }
        public bool CancelPressedThisFrame { get; private set; }

        private void Awake()
        {
            if (actionsAsset == null && !string.IsNullOrEmpty(fallbackResourcePath))
            {
                actionsAsset = Resources.Load<InputActionAsset>(fallbackResourcePath);
                if (actionsAsset == null)
                {
                    Debug.LogError($"[InputBridge] Unable to load InputActionAsset from Resources/{fallbackResourcePath}. Assign one explicitly in the inspector.");
                }
            }

            if (actionsAsset != null)
            {
                _runtimeAsset = Instantiate(actionsAsset);
                _actionMap = _runtimeAsset.FindActionMap(actionMapName, throwIfNotFound: false);
                if (_actionMap == null)
                {
                    Debug.LogError($"[InputBridge] Action map '{actionMapName}' not found in {actionsAsset.name}.");
                    return;
                }

                _moveAction = _actionMap.FindAction("Move", false);
                _lookAction = _actionMap.FindAction("Look", false);
                _attackAction = _actionMap.FindAction("Attack", false);
                _crouchAction = _actionMap.FindAction("Crouch", false);
                _jumpAction = _actionMap.FindAction("Jump", false);
                _sprintAction = _actionMap.FindAction("Sprint", false);
                _aimAction = _actionMap.FindAction("Aim", false);
                _blockAction = _actionMap.FindAction("Block", false);
                _rollAction = _actionMap.FindAction("Roll", false);
                _cancelAction = _actionMap.FindAction("Cancel", false);
            }
        }

        private void OnEnable()
        {
            if (IsOwner) EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsOwner) EnableActions();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            DisableActions();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            EnableActions();
        }

        public override void OnLostOwnership()
        {
            base.OnLostOwnership();
            DisableActions();
        }

        private void Update()
        {
            if (!_actionsEnabled || _actionMap == null)
            {
                ClearSampledValues();
                return;
            }

            Move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
            SprintHeld = _sprintAction != null && _sprintAction.IsPressed();
            CrouchHeld = _crouchAction != null && _crouchAction.IsPressed();
            JumpPressedThisFrame = _jumpAction != null && _jumpAction.WasPressedThisFrame();
            if (_attackAction != null)
            {
                AttackPressedThisFrame = _attackAction.WasPressedThisFrame();
                AttackHeld = _attackAction.IsPressed();
                AttackReleasedThisFrame = _attackAction.WasReleasedThisFrame();
            }
            else
            {
                AttackPressedThisFrame = false;
                AttackHeld = false;
                AttackReleasedThisFrame = false;
            }
            AimHeld = _aimAction != null && _aimAction.IsPressed();
            BlockHeld = _blockAction != null && _blockAction.IsPressed();
            RollPressedThisFrame = _rollAction != null && _rollAction.WasPressedThisFrame();
            CancelPressedThisFrame = _cancelAction != null && _cancelAction.WasPressedThisFrame();
        }

        private void EnableActions()
        {
            if (_actionMap == null || _actionsEnabled) return;
            _actionMap.Enable();
            _actionsEnabled = true;
        }

        private void DisableActions()
        {
            if (_actionMap == null || !_actionsEnabled) return;
            _actionMap.Disable();
            _actionsEnabled = false;
            ClearSampledValues();
        }

        private void ClearSampledValues()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            CrouchHeld = false;
            JumpPressedThisFrame = false;
            AttackPressedThisFrame = false;
            AttackHeld = false;
            AttackReleasedThisFrame = false;
            AimHeld = false;
            BlockHeld = false;
            RollPressedThisFrame = false;
            CancelPressedThisFrame = false;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_runtimeAsset != null)
            {
                Destroy(_runtimeAsset);
                _runtimeAsset = null;
            }
        }
    }
}
