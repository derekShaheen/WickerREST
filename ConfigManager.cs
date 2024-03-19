using MelonLoader;

namespace dm.ffmods.combattweaks
{
    public class ConfigManager
    {
        #region Fields

        // pref category for the mod
        public static MelonPreferences_Category CombatTweaksPrefs;

        public static bool GuardsDropEquipment = true;
        public static bool HuntersDropEquipment = true;
        public static bool IsVerbose = false;
        public static uint MaxCatapultsPerRaidGroup = 0;
        public static uint MaxRamsPerRaidGroup = 0;
        public static uint MinCatapultsPerRaidGroup = 0;
        public static uint MinRamsPerRaidGroup = 0;
        public static bool SoldiersDropEquipment = true;
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
            CombatTweaksPrefs = MelonPreferences.CreateCategory("CombatTweaks");
            CombatTweaksPrefs.SetFilePath(prefsPath);

            var prefs = CombatTweaksPrefs;

            isVerboseEntry = prefs.CreateEntry<bool>("verboseLogging", IsVerbose);
            minCatapultsEntry = prefs.CreateEntry<uint>("MinCatapultsPerRaidGroup", MinCatapultsPerRaidGroup);
            maxCatapultsEntry = prefs.CreateEntry<uint>("MaxCatapultsPerRaidGroup", MaxCatapultsPerRaidGroup);
            minRamsEntry = prefs.CreateEntry<uint>("MinRamsPerRaidGroup", MinRamsPerRaidGroup);
            maxRamsEntry = prefs.CreateEntry<uint>("MaxRamsPerRaidGroup", MaxRamsPerRaidGroup);
            hunterDropsEntry = prefs.CreateEntry<bool>("HuntersDropEquipment", HuntersDropEquipment);
            guardDropsEntry = prefs.CreateEntry<bool>("GuardsDropEquipment", GuardsDropEquipment);
            soldierDropsEntry = prefs.CreateEntry<bool>("SoldiersDropEquipment", SoldiersDropEquipment);
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