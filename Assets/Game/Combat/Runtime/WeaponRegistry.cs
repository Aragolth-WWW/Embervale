using System.Collections.Generic;
using UnityEngine;

namespace Embervale.Game.Combat
{
    // Runtime registry for WeaponDef assets loaded from Resources/Weapons
    public static class WeaponRegistry
    {
        private static readonly Dictionary<int, WeaponDef> ById = new Dictionary<int, WeaponDef>();
        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            ById.Clear();
            var defs = Resources.LoadAll<WeaponDef>("Weapons");
            foreach (var def in defs)
            {
                if (def == null) continue;
                if (!ById.ContainsKey(def.id)) ById.Add(def.id, def);
            }
            _loaded = true;
        }

        public static WeaponDef Get(int id)
        {
            EnsureLoaded();
            return ById.TryGetValue(id, out var def) ? def : null;
        }
    }
}

