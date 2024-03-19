using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace dm.ffmods.combattweaks
{
    public class CombatTweaksMelon : MelonMod
    {
        #region Fields

        public const string ConfigPath = "UserData/CombatTweaksConfig.cfg";
        private uint checkIntervalInSeconds = 3;
        private ConfigManager configManager;
        private bool setupDone0 = false;
        private float timeSinceLastCheckInSeconds = 0f;
        private bool verbose = true;

        #endregion Fields

        #region Properties

        public ConfigManager ConfigManager { get => configManager; }
        public bool HasInitalised { get; private set; }
        public bool Verbose { get => verbose; }

        #endregion Properties

        #region Public Methods

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Setting up CombatTweaks mod ...");
            configManager = new ConfigManager(ConfigPath);
            verbose = ConfigManager.IsVerbose;
        }

        public override void OnLateUpdate()
        {
            if (HasInitalised)
            {
                return;
            }
            // only continue if timer says so
            timeSinceLastCheckInSeconds += Time.deltaTime;
            if (timeSinceLastCheckInSeconds < checkIntervalInSeconds)
            {
                return;
            }

            // print progress
            if (!setupDone0)
            {
                // parse config file
                configManager.UpdateLootSettings();
                LoggerInstance.Msg(" [1/1] Reading config file ...");
                setupDone0 = true;
            }

            if (!configManager.IsInitialised)
            {
                if (verbose)
                {
                    LoggerInstance.Warning($"could not initialise config, will try again in {checkIntervalInSeconds} seconds ...");
                }
                return;
            }

            // done!
            HasInitalised = true;
            LoggerInstance.Msg($"CombatTweaks mod initialised!");
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (!HasInitalised)
            {
                return;
            }
        }

        #endregion Public Methods
    }
}