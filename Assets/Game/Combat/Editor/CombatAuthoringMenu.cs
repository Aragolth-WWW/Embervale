using UnityEditor;
using UnityEngine;

namespace Embervale.Game.Combat.Editor
{
    public static class CombatAuthoringMenu
    {
        [MenuItem("Tools/Embervale/Combat/Create Sample Assets")] 
        public static void CreateSampleAssets()
        {
            var basePath = "Assets/Game/Combat/Resources";
            var attacksPath = basePath + "/Attacks";
            var weaponsPath = basePath + "/Weapons";
            System.IO.Directory.CreateDirectory(attacksPath);
            System.IO.Directory.CreateDirectory(weaponsPath);

            // Unarmed light
            var unarmedLight = ScriptableObject.CreateInstance<AttackDef>();
            unarmedLight.name = "AttackDef_Unarmed_Light";
            unarmedLight.attackId = 1;
            unarmedLight.inputKind = AttackInputKind.Light;
            unarmedLight.animatorTrigger = "AttackLight";
            unarmedLight.damage = 6f;
            AssetDatabase.CreateAsset(unarmedLight, attacksPath + "/AttackDef_Unarmed_Light.asset");

            // Sword light
            var swordLight = ScriptableObject.CreateInstance<AttackDef>();
            swordLight.name = "AttackDef_Sword_Light";
            swordLight.attackId = 100;
            swordLight.inputKind = AttackInputKind.Light;
            swordLight.animatorTrigger = "AttackLight";
            swordLight.damage = 12f;
            AssetDatabase.CreateAsset(swordLight, attacksPath + "/AttackDef_Sword_Light.asset");

            // Sword heavy
            var swordHeavy = ScriptableObject.CreateInstance<AttackDef>();
            swordHeavy.name = "AttackDef_Sword_Heavy";
            swordHeavy.attackId = 101;
            swordHeavy.inputKind = AttackInputKind.Heavy;
            swordHeavy.animatorTrigger = "AttackHeavy";
            swordHeavy.damage = 20f;
            AssetDatabase.CreateAsset(swordHeavy, attacksPath + "/AttackDef_Sword_Heavy.asset");

            // Bow fire
            var bowFire = ScriptableObject.CreateInstance<AttackDef>();
            bowFire.name = "AttackDef_Bow_Fire";
            bowFire.attackId = 200;
            bowFire.inputKind = AttackInputKind.Charged;
            bowFire.animatorTrigger = "BowFire";
            bowFire.isMelee = false; bowFire.isRanged = true;
            AssetDatabase.CreateAsset(bowFire, attacksPath + "/AttackDef_Bow_Fire.asset");

            AssetDatabase.SaveAssets();

            // Weapons
            var unarmed = ScriptableObject.CreateInstance<WeaponDef>();
            unarmed.name = "WeaponDef_Unarmed";
            unarmed.id = 1; unarmed.weaponType = WeaponType.Unarmed; unarmed.displayName = "Unarmed";
            unarmed.attacks.Add(unarmedLight);
            AssetDatabase.CreateAsset(unarmed, weaponsPath + "/WeaponDef_Unarmed.asset");

            var sword = ScriptableObject.CreateInstance<WeaponDef>();
            sword.name = "WeaponDef_Sword";
            sword.id = 10; sword.weaponType = WeaponType.Sword; sword.displayName = "Sword";
            sword.attacks.Add(swordLight); sword.attacks.Add(swordHeavy);
            AssetDatabase.CreateAsset(sword, weaponsPath + "/WeaponDef_Sword.asset");

            var bow = ScriptableObject.CreateInstance<WeaponDef>();
            bow.name = "WeaponDef_Bow";
            bow.id = 20; bow.weaponType = WeaponType.Bow; bow.displayName = "Bow";
            bow.attacks.Add(bowFire);
            AssetDatabase.CreateAsset(bow, weaponsPath + "/WeaponDef_Bow.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Embervale] Sample combat assets created under " + basePath);
        }
    }
}

