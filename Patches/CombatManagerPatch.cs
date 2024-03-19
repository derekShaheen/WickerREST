using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace dm.ffmods.combattweaks
{
    [HarmonyPatch(typeof(CombatManager), "CreateRaiderGroup", new Type[] { typeof(RaidGroupSetupData) })]
    public static class CombatManagerPatch1
    {
        #region Private Methods

        private static bool Prefix(RaidGroupSetupData raidGroupSetupData)
        {
            Melon<CombatTweaksMelon>.Logger.Msg($"CreateRaiderGroup is being called!");
            if (!Melon<CombatTweaksMelon>.Instance.HasInitalised)
            {
                if (Melon<CombatTweaksMelon>.Instance.Verbose)
                {
                    Melon<CombatTweaksMelon>.Logger.Warning($"mod not initialised, skipping CombatManager CreateRaiderGroup hook ...");
                }
                return true;
            }

            Melon<CombatTweaksMelon>.Logger.Msg($"Editing Raider Group in 'CreateRaiderGroup' ...");
            CombatTweaks.UpdateRaidSetup(raidGroupSetupData);

            return true;
        }

        #endregion Private Methods
    }

    [HarmonyPatch(typeof(CombatManager), "SpawnRaid", new Type[] { typeof(RaidIncursionSetupData) })]
    public static class CombatManagerPatch2
    {
        #region Private Methods

        private static bool Prefix(RaidIncursionSetupData raidIncursionSetupData)
        {
            if (!Melon<CombatTweaksMelon>.Instance.HasInitalised)
            {
                if (Melon<CombatTweaksMelon>.Instance.Verbose)
                {
                    Melon<CombatTweaksMelon>.Logger.Warning($"mod not initialised, skipping CombatManager SpawnRaid hook ...");
                }
                return true;
            }
            Melon<CombatTweaksMelon>.Logger.Msg($"Editing Raider Groups in 'SpawnRaid' ...");
            var groups = raidIncursionSetupData.groupEntries;
            foreach (var groupEntry in groups)
            {
                CombatTweaks.UpdateRaidSetup(groupEntry.group);
            }

            return true;
        }

        #endregion Private Methods
    }

    [HarmonyPatch(typeof(CombatManager), "SpawnRaidersFindEdgePoint", new Type[] { typeof(RaidGroupSetupData), typeof(RaidIncursionTracker), typeof(int), typeof(bool), typeof(bool) })]
    public static class CombatManagerPatch3
    {
        #region Private Methods

        private static bool Prefix(
            RaidGroupSetupData raidGroupSetupData,
            RaidIncursionTracker incursionTracker,
            int numRaidersToSpawn,
            bool checkLastSuccessfulEdgePointFirst,
            bool askForRansom)
        {
            if (!Melon<CombatTweaksMelon>.Instance.Verbose)
            {
                return true;
            }
            Melon<CombatTweaksMelon>.Logger.Msg($"SpawnRaidersFindEdgePoint is being called. " +
                $"Used raider group setup was: {raidGroupSetupData.name}. " +
                $"It can spawn [{raidGroupSetupData.numBatteringRamsToSpawnMin}-{raidGroupSetupData.numBatteringRamsToSpawnMax}] rams " +
                $"and [{raidGroupSetupData.numCatapultsToSpawnMin}-{raidGroupSetupData.numCatapultsToSpawnMax}] catapults.");
            return true;
        }

        #endregion Private Methods
    }

    [HarmonyPatch(typeof(CombatManager), "SpawnRaidersAtDirection", new Type[] { typeof(RaidGroupSetupData), typeof(RaidIncursionTracker), typeof(int), typeof(bool), typeof(bool), typeof(Vector3), typeof(int), typeof(int) })]
    public static class CombatManagerPatch4
    {
        #region Private Methods

        private static bool Prefix(
            RaidGroupSetupData raidGroupSetupData,
            RaidIncursionTracker incursionTracker,
            int numRaidersToSpawn,
            bool checkLastSuccessfulEdgePointFirst,
            bool askForRansom,
            Vector3 target,
            int numBuildingsToConsider,
            int townCenterWeight)
        {
            if (!Melon<CombatTweaksMelon>.Instance.Verbose)
            {
                return true;
            }
            Melon<CombatTweaksMelon>.Logger.Msg($"SpawnRaidersAtDirection is being called. " +
                $"Used raider group setup was: {raidGroupSetupData.name}. " +
                $"It can spawn [{raidGroupSetupData.numBatteringRamsToSpawnMin}-{raidGroupSetupData.numBatteringRamsToSpawnMax}] rams " +
                $"and [{raidGroupSetupData.numCatapultsToSpawnMin}-{raidGroupSetupData.numCatapultsToSpawnMax}] catapults.");
            return true;
        }

        #endregion Private Methods
    }

    [HarmonyPatch(typeof(CombatManager), "SpawnRaidersWithEdgePoint", new Type[] { typeof(RaidGroupSetupData), typeof(RaidIncursionTracker), typeof(int), typeof(Vector3), typeof(bool) })]
    public static class CombatManagerPatch5
    {
        #region Private Methods

        private static bool Prefix(
            RaidGroupSetupData raidGroupSetupData,
            RaidIncursionTracker incursionTracker,
            int raidersToSpawn,
            Vector3 edgePoint,
            bool askForRansom)
        {
            if (!Melon<CombatTweaksMelon>.Instance.Verbose)
            {
                return true;
            }
            Melon<CombatTweaksMelon>.Logger.Msg($"SpawnRaidersWithEdgePoint is being called. " +
                $"Used raider group setup was: {raidGroupSetupData.name}. " +
                $"It can spawn [{raidGroupSetupData.numBatteringRamsToSpawnMin}-{raidGroupSetupData.numBatteringRamsToSpawnMax}] rams " +
                $"and [{raidGroupSetupData.numCatapultsToSpawnMin}-{raidGroupSetupData.numCatapultsToSpawnMax}] catapults.");
            return true;
        }

        #endregion Private Methods
    }
}