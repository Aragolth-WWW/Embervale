using UnityEngine;
using Unity.Netcode;

namespace Embervale.Game.Combat
{
    // Handles authoritative attack events and client-side animation triggers.
    [RequireComponent(typeof(EquipmentState))]
    public class AttackController : NetworkBehaviour
    {
        private EquipmentState _equip;
        private Animator _anim;

        public NetworkVariable<AttackEvent> LastAttack = new NetworkVariable<AttackEvent>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _equip = GetComponent<EquipmentState>();
            _anim = GetComponentInChildren<Animator>();
        }

        public override void OnNetworkSpawn()
        {
            LastAttack.OnValueChanged += OnAttackChanged;
        }

        private void OnAttackChanged(AttackEvent prev, AttackEvent cur)
        {
            if (_anim == null) return;
            var def = _equip != null && _equip.CurrentDef != null ? _equip.CurrentDef.GetById(cur.AttackId) : null;
            if (def != null && !string.IsNullOrEmpty(def.animatorTrigger))
            {
                _anim.ResetTrigger(def.animatorTrigger);
                _anim.SetTrigger(def.animatorTrigger);
            }
        }

        // Client calls; server validates and emits event
        [ServerRpc]
        public void TryAttackServerRpc(AttackInputKind inputKind, Vector3 aimDir, float charge)
        {
            var w = _equip != null ? _equip.CurrentDef : null;
            if (w == null) return;
            var atk = w.GetByInput(inputKind);
            if (atk == null) return;
            // TODO: validate cooldowns, stamina, state machine
            var ev = new AttackEvent { AttackId = atk.attackId, ServerTime = (float)NetworkManager.ServerTime.TimeAsFloat }; // NGO time
            LastAttack.Value = ev;
            // TODO: schedule hitboxes / spawn projectiles server-side
        }
    }
}

