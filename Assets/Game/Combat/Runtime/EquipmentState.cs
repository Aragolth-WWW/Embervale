using Unity.Netcode;
using UnityEngine;

namespace Embervale.Game.Combat
{
    // Holds replicated equip state and exposes local events for visuals.
    public class EquipmentState : NetworkBehaviour
    {
        public NetworkVariable<int> EquippedItemId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<WeaponType> EquippedWeapon = new NetworkVariable<WeaponType>(WeaponType.Unarmed, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> IsAiming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsBlocking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public WeaponDef CurrentDef { get; private set; }

        public override void OnNetworkSpawn()
        {
            EquippedItemId.OnValueChanged += OnEquippedChanged;
            EquippedWeapon.OnValueChanged += OnWeaponTypeChanged;
            if (IsServer && EquippedItemId.Value == 0)
            {
                // Default to Unarmed (id 1)
                EquipInternal(1, WeaponType.Unarmed);
            }
            else
            {
                ApplyLocal();
            }
        }

        private void OnEquippedChanged(int prev, int next) => ApplyLocal();
        private void OnWeaponTypeChanged(WeaponType prev, WeaponType next) => ApplyLocal();

        private void ApplyLocal()
        {
            WeaponRegistry.EnsureLoaded();
            CurrentDef = WeaponRegistry.Get(EquippedItemId.Value);
            // Visuals/Animator overrides can be applied here by consumer code.
        }

        [ServerRpc]
        public void EquipServerRpc(int itemId)
        {
            var def = WeaponRegistry.Get(itemId);
            if (def == null) return;
            EquipInternal(itemId, def.weaponType);
        }

        private void EquipInternal(int itemId, WeaponType type)
        {
            EquippedItemId.Value = itemId;
            EquippedWeapon.Value = type;
        }
    }
}

