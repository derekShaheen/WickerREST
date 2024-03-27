using MelonLoader;

namespace dm.ffmods.combattweaks
{
    public class ConfigManager
    {
        #region Fields

        public static bool GuardsDropEquipment = true;

        public static bool HuntersDropEquipment = true;

        public static bool IsVerbose = false;

        public static uint MaxCatapultsPerRaidGroup = 0;

        public static uint MaxRamsPerRaidGroup = 0;

        public static uint MinCatapultsPerRaidGroup = 0;

        public static uint MinRamsPerRaidGroup = 0;

        // pref category for the mod
        public static MelonPreferences_Category RaidPrefs;

        public static MelonPreferences_Category SetupPrefs;
        public static bool SoldiersDropEquipment = true;
        public static MelonPreferences_Category VillagerPrefs;
        private MelonPreferences_Entry<bool> guardDropsEntry;
        private MelonPreferences_Entry<bool> hunterDropsEntry;
        private MelonPreferences_Entry<bool> isVerboseEntry;
        private MelonPreferences_Entry<uint> maxCatapultsEntry;
        private MelonPreferences_Entry<uint> maxRamsEntry;
        private MelonPreferences_Entry<uint> minCatapultsEntry;
        private MelonPreferences_Entry<uint> minRamsEntry;
        private MelonPreferences_Entry<bool> soldierDropsEntry;

        #endregion Fields

        #region Public Constructors

        public ConfigManager(string prefsPath)
        {
            RaidPrefs = MelonPreferences.CreateCategory("RaidSettings");
            RaidPrefs.SetFilePath(prefsPath);
            SetupPrefs = MelonPreferences.CreateCategory("Setup");
            SetupPrefs.SetFilePath(prefsPath);
            VillagerPrefs = MelonPreferences.CreateCategory("VillagerSettings");
            VillagerPrefs.SetFilePath(prefsPath);

            isVerboseEntry = SetupPrefs.CreateEntry<bool>("verboseLogging", IsVerbose);
            minCatapultsEntry = RaidPrefs.CreateEntry<uint>("MinCatapultsPerRaidGroup", MinCatapultsPerRaidGroup);
            maxCatapultsEntry = RaidPrefs.CreateEntry<uint>("MaxCatapultsPerRaidGroup", MaxCatapultsPerRaidGroup);
            minRamsEntry = RaidPrefs.CreateEntry<uint>("MinRamsPerRaidGroup", MinRamsPerRaidGroup);
            maxRamsEntry = RaidPrefs.CreateEntry<uint>("MaxRamsPerRaidGroup", MaxRamsPerRaidGroup);
            hunterDropsEntry = VillagerPrefs.CreateEntry<bool>("HuntersDropEquipment", HuntersDropEquipment);
            guardDropsEntry = VillagerPrefs.CreateEntry<bool>("GuardsDropEquipment", GuardsDropEquipment);
            soldierDropsEntry = VillagerPrefs.CreateEntry<bool>("SoldiersDropEquipment", SoldiersDropEquipment);
        }

        #endregion Public Constructors

        #region Properties

        public bool IsInitialised { get; private set; }

        #endregion Properties

        #region Public Methods

        public void UpdateLootSettings()
        {
            IsVerbose = isVerboseEntry.Value;

            MinCatapultsPerRaidGroup = minCatapultsEntry.Value;
            MaxCatapultsPerRaidGroup = maxCatapultsEntry.Value;
            MinRamsPerRaidGroup = minRamsEntry.Value;
            MaxRamsPerRaidGroup = maxRamsEntry.Value;
            HuntersDropEquipment = hunterDropsEntry.Value;
            GuardsDropEquipment = guardDropsEntry.Value;
            SoldiersDropEquipment = soldierDropsEntry.Value;

            IsInitialised = true;
        }

        #endregion Public Methods
    }
}