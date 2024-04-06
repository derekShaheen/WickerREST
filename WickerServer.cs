using MelonLoader;
using MelonLoader.Utils;
using System.Net;
using WickerREST;

namespace Wicker
{
    public static class BuildInfo
    {
        public const string Name = "WickerREST";
        public const string Description = "WickerREST";
        public const string Author = "Skrip";
        public const string Version = "0.93.0";
        public const string DownloadLink = "";
    }

    public class WickerServer : MelonMod
    {
        internal static string userDataPath   = "WickerREST";
        internal static string resourcesPath  = userDataPath + "/resources";

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private Queue<(Action, HttpListenerResponse)> mainThreadActions = new Queue<(Action, HttpListenerResponse)>();

        private MelonPreferences_Category?      modCategory;
        private MelonPreferences_Entry<int>?    listeningPort;
        private MelonPreferences_Entry<int>?    debugLevel;

        // Instance of this class
        private static WickerServer?            instance;

        // Singleton pattern
        public static WickerServer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new WickerServer();
                }
                return instance;
            }
            private set
            {
                instance = value;
            }
        }

        public override void OnInitializeMelon()
        {
            Instance = this;

            userDataPath = Path.Combine(MelonEnvironment.UserDataDirectory, userDataPath);
            Directory.CreateDirectory(userDataPath);

            resourcesPath = Path.Combine(MelonEnvironment.UserDataDirectory, resourcesPath);
            Directory.CreateDirectory(resourcesPath);

            modCategory = MelonPreferences.CreateCategory("WickerREST");
            modCategory.SetFilePath(Path.Combine(userDataPath, "WickerREST.cfg"));

            listeningPort = modCategory.CreateEntry("ListeningPort", 6103, description: "Port server will listen on");
            debugLevel = modCategory.CreateEntry("DebugLevel", 0, description: "Debug level for logging (0: None, 1: Raised, 2: Verbose)");

            if (!File.Exists(Path.Combine(userDataPath, "WickerREST.cfg")))
            {
                MelonPreferences.Save();
            }

            //Verify listening port is valid, otherwise set to default 6103. Notify it was reset
            if (listeningPort.Value < 1 || listeningPort.Value > 65535)
            {
                listeningPort.Value = 6103;
                LogMessage("Listening port was invalid. Reset to default 6103.");
                MelonPreferences.Save();
            }

            //Verify debug level is valid, otherwise set to default 0. Notify it was reset
            if (debugLevel.Value < 0 || debugLevel.Value > 2)
            {
                debugLevel.Value = 0;
                LogMessage("Debug level was invalid. Reset to default 0.");
                MelonPreferences.Save();
            }

            var discoveryThread = new Thread(() =>
            {
                Commands.Instance.DiscoverHandlersAndVariables(); // Discover command handlers and game variables
            });
            discoveryThread.Start();

            WickerNetwork.Instance.StartServer(listeningPort.Value, cancellationTokenSource);

            LoggerInstance.WriteLine(39);
            LogMessage($"Server initialized on port {listeningPort.Value}");
            LogMessage($"Navigate to: http://localhost:{listeningPort.Value}/");
            LoggerInstance.WriteLine(39);
        }

        public override void OnUpdate()
        {
            try
            {
                while (mainThreadActions.Count > 0)
                {
                    var (action, response) = mainThreadActions.Dequeue();
                    try
                    {
                        action.Invoke();
                    }
                    finally
                    {
                        // Try to close the response in case the action failed or didn't send a response
                        try
                        {
                            response.Close();
                        }
                        catch (Exception)
                        {
                            // No need to log this exception, it's a cleanup in case this didn't already happen
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error(ex.ToString());
            }
        }

        internal void ExecuteOnMainThread(Action action, HttpListenerResponse response)
        {
            mainThreadActions.Enqueue((action, response));
        }

        public void LogMessage(string message, int requiredDebugLevel = 0)
        {
            // Check if current debug level allows logging this message
            if ((debugLevel == null && requiredDebugLevel == 0) 
                    || (debugLevel != null && debugLevel.Value >= requiredDebugLevel))
            {
                LoggerInstance.Msg(message);
            }
        }

        public override void OnApplicationQuit()
        {
            WickerNetwork.Instance.StopServer();
            base.OnApplicationQuit();
        }
    }
}

