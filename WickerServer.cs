using System.Net;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using System.Web;
using System.Diagnostics;

namespace SkInterface
{
    public static class BuildInfo
    {
        public const string Name = "WickerREST";
        public const string Description = "WickerREST";
        public const string Author = "Skrip";
        public const string Version = "1.0.0";
        public const string DownloadLink = "";
    }

    public class WickerServer : MelonMod
    {
        private const string COMMANDS_PATH  = "/commands";
        private const string GAME_VARIABLES_PATH    = "/game-variables";
        private const string FAVICON_PATH   = "/favicon.ico";
        private const string INDEX_URL      = "https://raw.githubusercontent.com/derekShaheen/WickerREST/main/web/index.html";
        private const string FAVICON_URL    = "https://raw.githubusercontent.com/derekShaheen/WickerREST/main/web/resources/favicon.ico";

        private Dictionary<string, (MethodInfo Method, string[] Parameters, string Category)>? commandHandlers;
        private Dictionary<string, Func<object>>? gameVariableMethods;

        private HttpListener?   listener;
        private Thread?         listenerThread;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // Instance of this class
        private static WickerServer?            instance;
        private MelonPreferences_Category?      modCategory;
        private MelonPreferences_Entry<int>?    listeningPort;
        private MelonPreferences_Entry<int>?    debugLevel;

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
            var discoveryStopwatch = new Stopwatch();

            var discoveryThread = new Thread(() =>
            {
                LogMessage("Starting handler and variable discovery thread...", 1);
                discoveryStopwatch.Start();
                DiscoverHandlersAndVariables(); // Discover command handlers and game variables
                discoveryStopwatch.Stop();
                LogMessage($"Handler and variable discovery thread completed in {discoveryStopwatch.ElapsedMilliseconds}ms.", 1);
            });
            discoveryThread.Start();
            HarmonyInstance.PatchAll();
            modCategory = MelonPreferences.CreateCategory("WickerREST");
            modCategory.SetFilePath(Path.Combine(MelonEnvironment.UserDataDirectory, "WickerREST", "WickerREST.cfg"));
            listeningPort = modCategory.CreateEntry("ListeningPort", 6103, description: "Port server will listen on");
            debugLevel = modCategory.CreateEntry("DebugLevel", 0, description: "Debug level for logging (0: None, 1: Raised, 2: Verbose)");

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

            StartServer(listeningPort.Value);

            LoggerInstance.WriteLine(37);
            LogMessage($"Server initialized on port {listeningPort.Value}");
            LogMessage($"Navigate to: http://localhost:{listeningPort.Value}/");
            LoggerInstance.WriteLine(37);
        }

        private void StartServer(int port)
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");


            listener.Start();
            listenerThread = new Thread(() => HandleRequests(cancellationTokenSource.Token));
            listenerThread?.Start(); // Start the listener thread

        }

        private async void HandleRequests(CancellationToken cancellationToken)
        {
            if (listener == null)
                return;
            try
            {
                while (listener.IsListening && !cancellationToken.IsCancellationRequested)
                {
                    var context = listener.GetContext();
                    var request = context.Request;
                    var response = context.Response;

                    if (request == null || response == null || request.Url == null || commandHandlers == null)
                        return;

                    try
                    {
                        if (request.HttpMethod == "GET" && commandHandlers.TryGetValue(request.Url.AbsolutePath, out var handlerTuple))
                        {
                            MethodInfo methodToInvoke = handlerTuple.Method;
                            var parameterInfos = methodToInvoke.GetParameters();

                            // Assuming all parameters are strings for simplicity; adjust as needed.
                            object[] parameters = new object[parameterInfos.Length];

                            // Parse query parameters
                            var query = HttpUtility.ParseQueryString(request.Url.Query);
                            parameters[0] = response; // Pass the response object
                            if (parameterInfos.Length > 1)
                            {
                                for (int i = 1; i < parameterInfos.Length; i++)
                                {
                                    var paramInfo = parameterInfos[i];
                                    var paramValue = query[paramInfo.Name];
                                    // Convert paramValue to the correct type as needed
                                    parameters[i] = Convert.ChangeType(paramValue, paramInfo.ParameterType);
                                }
                            }
                            MelonCoroutines.Start(ExecuteOnMainThread(() =>
                            {
                                methodToInvoke.Invoke(null, parameters);
                            }));
                        }
                        else if (request.Url.AbsolutePath == "/")
                        {
                            await ServeHtmlPage(response, "index.html");
                        }
                        else if (request.Url.AbsolutePath == FAVICON_PATH)
                        {
                            await ServeFavicon(response, Path.Combine(MelonEnvironment.UserDataDirectory, "WickerREST", "resources", "favicon.ico"));
                        }
                        else if (request.Url.AbsolutePath == COMMANDS_PATH)
                        {
                            if (commandHandlers != null && commandHandlers.Count > 0)
                            {
                                var commandsInfo = commandHandlers.Select(handler => new {
                                    Path = handler.Key,
                                    Parameters = handler.Value.Method.GetParameters()
                                                    .Select(p => new {
                                                        p.Name,
                                                        Type = p.ParameterType.Name,
                                                        DefaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                                                    })
                                                    .ToArray(),
                                    Category = handler.Value.Category
                                }).ToArray();

                                var productName = Application.productName;
                                var responseContent = new
                                {
                                    productName = productName,
                                    commands = commandsInfo,
                                };

                                var commandsJson = JsonConvert.SerializeObject(responseContent);
                                SendResponse(response, commandsJson, 200, "application/json");
                            }
                            else
                            {
                                SendResponse(response, @"", 200);
                            }
                        }
                        else if (request.Url.AbsolutePath == GAME_VARIABLES_PATH)
                        {
                            if (gameVariableMethods != null && gameVariableMethods.Count > 0)
                            {

                                var variableValues = gameVariableMethods.Select(kvp => new
                                {
                                    VariableName = kvp.Key,
                                    Value = kvp.Value.Invoke().ToString()
                                }).ToDictionary(kvp => kvp.VariableName, kvp => kvp.Value);

                                var json = JsonConvert.SerializeObject(variableValues);
                                SendResponse(response, json, 200, "application/json");
                            } else
                            {
                                SendResponse(response, "No game variables found.", 200);
                            }
                        }
                        else
                        {
                            SendResponse(response, "Invalid request.", 404);
                        }
                        response.OutputStream.Close();
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException) break;
                        LogMessage($"Server error: {ex.Message}", 1);
                        SendResponse(response, $"Server error. {ex.Message}", 500);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is HttpListenerException || ex is ObjectDisposedException)
                {
                    //User is trying to close the server improperly. Handle on application quit.
                }
            }
        }

        private async System.Threading.Tasks.Task EnsureFileExists(string filePath, string url, bool isBinary = false)
        {
            if (!File.Exists(filePath))
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        if (isBinary)
                        {
                            LogMessage($"Attempting binary download of {url}", 1);
                            var response = await httpClient.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                var contentBytes = await response.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(filePath, contentBytes);
                                LogMessage($"Downloaded binary file to {filePath}", 1);
                            }
                            else
                            {
                                LogMessage($"Failed to download binary file. Status code: {response.StatusCode}", 1);
                            }
                        }
                        else
                        {
                            var contentString = await httpClient.GetStringAsync(url);
                            await File.WriteAllTextAsync(filePath, contentString);
                            LogMessage($"Downloaded text file to {filePath}", 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Exception during file download: {ex.Message}", 1);
                }
            }
        }


        private async System.Threading.Tasks.Task ServeHtmlPage(HttpListenerResponse response, string fileName)
        {
            var filePath = Path.Combine(MelonEnvironment.UserDataDirectory, "WickerREST", "resources", fileName);
            await EnsureFileExists(filePath, "https://raw.githubusercontent.com/derekShaheen/WickerREST/main/web/index.html");

            if (File.Exists(filePath))
            {
                var pageContent = File.ReadAllBytes(filePath);
                SendResponse(response, System.Text.Encoding.UTF8.GetString(pageContent), 200, "text/html");
            }
            else
            {
                SendResponse(response, "Page not found.", 404);
            }
        }

        private async System.Threading.Tasks.Task ServeFavicon(HttpListenerResponse response, string filePath)
        {
            await EnsureFileExists(filePath, "https://raw.githubusercontent.com/derekShaheen/WickerREST/main/web/resources/favicon.ico", true);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists)
            {
                try
                {
                    response.ContentType = "image/x-icon";
                    response.ContentLength64 = fileInfo.Length;
                    using (var fileStream = fileInfo.OpenRead())
                    {
                        await fileStream.CopyToAsync(response.OutputStream);
                    }
                    response.StatusCode = 200; // OK
                }
                catch (Exception ex)
                {
                    //
                }
            }
            else
            {
                response.StatusCode = 404; // Not Found
                                           // Make sure not to close the response here if it's managed elsewhere
            }
        }

        private IEnumerator ExecuteOnMainThread(Action action)
        {
            action.Invoke();
            yield return null;
        }

        public void SendResponse(HttpListenerResponse response, string message, int statusCode = 200, string contentType = "text/plain")
        {
            response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            response.Headers.Add("Pragma", "no-cache");
            //
            response.StatusCode = statusCode;
            response.ContentType = contentType;
            var buffer = System.Text.Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void DiscoverHandlersAndVariables()
        {
            commandHandlers = new Dictionary<string, (MethodInfo, string[], string)>();
            gameVariableMethods = new Dictionary<string, Func<object>>();

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            // Discover Command Handlers
                            var commandAttribute = method.GetCustomAttribute<CommandHandlerAttribute>();
                            if (commandAttribute != null)
                            {
                                string path = EnsureUniqueKey(commandHandlers.Keys, commandAttribute.Path);
                                var parameters = method.GetParameters()
                                                        .Where(param => param.ParameterType != typeof(HttpListenerResponse))
                                                        .Select(param => param.Name)
                                                        .ToArray();
                                commandHandlers[path] = (method, parameters, commandAttribute.Category ?? string.Empty);
                            }

                            // Discover Game Variables
                            var gameVariableAttribute = method.GetCustomAttribute<GameVariableAttribute>();
                            if (gameVariableAttribute != null && method.ReturnType != typeof(void) && method.GetParameters().Length == 0)
                            {
                                string variableName = EnsureUniqueKey(gameVariableMethods.Keys, gameVariableAttribute.VariableName);
                                Func<object> valueProvider = () => method.Invoke(null, null);
                                gameVariableMethods[variableName] = valueProvider;
                            }
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                //LoggerInstance.Msg($"Error during discovery: {ex.LoaderExceptions.FirstOrDefault()?.Message}", 2);
            }

            // Notify discovery completion
            OnCommandHandlersDiscovered();
        }

        private string EnsureUniqueKey(IEnumerable<string> existingKeys, string originalKey)
        {
            string uniqueKey = originalKey;
            int counter = 2;
            while (existingKeys.Contains(uniqueKey))
            {
                uniqueKey = $"{originalKey}_{counter}";
                counter++;
            }
            return uniqueKey;
        }

        public void OnCommandHandlersDiscovered()
        {
            if (commandHandlers == null || commandHandlers.Count == 0)
            {
                LogMessage("No command handlers found.");
            }
            else
            {
                LogMessage($"Discovered {commandHandlers.Count} command handlers.");
            }
            if (gameVariableMethods == null || gameVariableMethods.Count == 0)
            {
                LogMessage("No game variable monitors found.");
            }
            else
            {
                LogMessage($"Discovered {gameVariableMethods.Count} game variable monitors.");
            }
        }

        public void LogMessage(string message, int requiredDebugLevel = 0)
        {
            // Check if current debug level allows logging this message
            if (debugLevel.Value >= requiredDebugLevel)
            {
                LoggerInstance.Msg(message);
            }
        }

        public void LogResponse(HttpListenerResponse response, string message)
        {
            LogMessage("Sending to client: " + message, 2);
            SendResponse(response, message);
        }

        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            if (listener != null)
            {
                LogMessage("Shutting down REST client...");
                listener.Stop(); // Stop the listener
                listener.Close(); // Clean up listener resources
                cancellationTokenSource.Cancel(); // Signal cancellation
            }
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(); // Wait for the thread to complete execution
            }
        }

    }
}

