using UnityEngine;

namespace Embervale.Game.Combat
{
    [CreateAssetMenu(menuName = "Embervale/Combat/AttackDef", fileName = "AttackDef_", order = 1000)]
    public class AttackDef : ScriptableObject
    {
        [Tooltip("Unique ushort id for replication")] public ushort attackId = 1;
        [Tooltip("Input kind this attack maps to by default")] public AttackInputKind inputKind = AttackInputKind.Light;
        [Header("Timings (seconds)")]
        public float windup = 0.15f;
        public float active = 0.2f;
        public float recover = 0.35f;

        [Header("Animator")]
        [Tooltip("Animator Trigger name to fire when this attack starts")] public string animatorTrigger = "AttackLight";

        [Header("Melee")]
        public bool isMelee = true;
        [Tooltip("Optional bone name for hitbox origin (e.g., RightHand)")] public string boneName;
        [Tooltip("Local position offset from bone")] public Vector3 localOffset = new Vector3(0.2f, 0f, 0.6f);
        [Tooltip("Hitbox radius (sphere)")] public float hitboxRadius = 0.4f;
        [Tooltip("Damage dealt on hit")] public float damage = 10f;

        [Header("Ranged")]
        public bool isRanged = false;
        public GameObject projectilePrefab;
        public float projectileSpeed = 30f;
    }
}

