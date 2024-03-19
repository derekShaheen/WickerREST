using Il2Cpp;
using MelonLoader;

namespace dm.ffmods.combattweaks
{
    public static class CombatTweaks
    {
        #region Public Methods

        public static void HandleVillagerDeath(Villager villager)
        {
            var occupation = villager.GetOccupation();

            if (occupation == VillagerOccupation.Occupation.Hunter)
            {
                if (!ConfigManager.HuntersDropEquipment)
                {
                    return;
                }
                if (Melon<CombatTweaksMelon>.Instance.Verbose)
                {
                    Melon<CombatTweaksMelon>.Logger.Msg($"Hunter died, dropping their inventory ...");
                }
                villager.DropCombatGearInventoryOnGround();
            }
            if (occupation == VillagerOccupation.Occupation.Soldier)
            {
                if (!ConfigManager.SoldiersDropEquipment)
                {
                    return;
                }
                if (Melon<CombatTweaksMelon>.Instance.Verbose)
                {
                    Melon<CombatTweaksMelon>.Logger.Msg($"Soldier died, dropping their inventory ...");
                }
                villager.DropCombatGearInventoryOnGround();
            }

            if (occupation == VillagerOccupation.Occupation.Guard)
            {
                if (!ConfigManager.GuardsDropEquipment)
                {
                    return;
                }
                if (Melon<CombatTweaksMelon>.Instance.Verbose)
                {
                    Melon<CombatTweaksMelon>.Logger.Msg($"Guard died, dropping their inventory ...");
                }
                villager.DropCombatGearInventoryOnGround();
            }
        }

        public static void UpdateRaidSetup(RaidGroupSetupData raidGroupSetupData)
        {
            Melon<CombatTweaksMelon>.Logger.Msg($"modifying raider group setup with name: {raidGroupSetupData.name}");
            raidGroupSetupData.numBatteringRamsToSpawnMin = (int)ConfigManager.MinRamsPerRaidGroup;
            raidGroupSetupData.numBatteringRamsToSpawnMax = (int)ConfigManager.MaxRamsPerRaidGroup;
            raidGroupSetupData.numCatapultsToSpawnMin = (int)ConfigManager.MinCatapultsPerRaidGroup;
            raidGroupSetupData.numCatapultsToSpawnMax = (int)ConfigManager.MaxCatapultsPerRaidGroup;
        }

        #endregion Public Methods
    }
}