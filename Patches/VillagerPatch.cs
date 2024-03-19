using dm.ffmods.combattweaks;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using static MelonLoader.MelonLogger;

namespace dm.ffmods.combattweaks
{
    [HarmonyPatch(typeof(Villager), "OnDeath", new Type[] { typeof(float), typeof(GameObject), typeof(DamageType) })]
    public static class VillagerPatch
    {
        #region Private Methods

        private static bool Prefix(float damageTaken, GameObject damageCauser, Villager __instance)
        {
            if (!Melon<CombatTweaksMelon>.Instance.HasInitalised)
            {
                if (Melon<CombatTweaksMelon>.Instance.Verbose)
                {
                    Melon<CombatTweaksMelon>.Logger.Warning($"mod not initialised, skipping Villager OnDeath hook ...");
                }
                return true;
            }

            // __instance gets us the instance of the Villager class
            Villager villager = __instance;

            CombatTweaks.HandleVillagerDeath(villager);

            return true;
        }

        #endregion Private Methods
    }
}