using System.Collections.Generic;
using UnityEngine;

namespace Embervale.Game.Combat
{
    [CreateAssetMenu(menuName = "Embervale/Combat/WeaponDef", fileName = "WeaponDef_", order = 1001)]
    public class WeaponDef : ScriptableObject
    {
        [Tooltip("Unique int id for this weapon for replication/equip")] public int id = 1;
        public string displayName = "Weapon";
        public WeaponType weaponType = WeaponType.Unarmed;

        [Header("Animator Integration")]
        [Tooltip("Optional Animator Override Controller to swap clips")] public AnimatorOverrideController animatorOverride;
        [Tooltip("Upper-body avatar mask to restrict to arms/hands")] public AvatarMask upperBodyMask;

        [Header("Attacks")]
        public List<AttackDef> attacks = new List<AttackDef>();

        public AttackDef GetById(ushort attackId)
        {
            for (int i = 0; i < attacks.Count; i++) if (attacks[i] != null && attacks[i].attackId == attackId) return attacks[i];
            return null;
        }

        public AttackDef GetByInput(AttackInputKind kind)
        {
            for (int i = 0; i < attacks.Count; i++) if (attacks[i] != null && attacks[i].inputKind == kind) return attacks[i];
            return null;
        }
    }
}

