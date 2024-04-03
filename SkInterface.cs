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

namespace SkInterface
{
    public static class BuildInfo
    {
        public const string Name = "SkInterface";
        public const string Description = "SkInterface";
        public const string Author = "Skrip";
        public const string Version = "1.0.0";
        public const string DownloadLink = "";
    }

    public class SkInterface : MelonMod
    {
        private Dictionary<string, (MethodInfo Method, string[] Parameters, string Category)>? commandHandlers;
        private Dictionary<string, Func<object>>? gameVariableMethods;

        private HttpListener? listener;
        private Thread? listenerThread;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // Instance of this class
        private static SkInterface? instance;
        private MelonPreferences_Category? modCategory;
        private MelonPreferences_Entry<int>? listeningPort;

        // Singleton pattern
        public static SkInterface Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SkInterface();
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

            var discoveryThread = new Thread(() =>
            {
                DiscoverHandlersAndVariables(); // Discover command handlers and game variables
            });
            discoveryThread.Start();
            HarmonyInstance.PatchAll();
            modCategory = MelonPreferences.CreateCategory("SkREST");
            modCategory.SetFilePath(Path.Combine(MelonEnvironment.UserDataDirectory, "SkRESTClient", "SkRESTClient.cfg"));
            listeningPort = modCategory.CreateEntry("ListeningPort", 6103, description: "Port server will listen on");

            //Verify listening port is valid, otherwise set to default 6103. Notify it was reset
            if (listeningPort.Value < 1 || listeningPort.Value > 65535)
            {
                listeningPort.Value = 6103;
                LoggerInstance.Msg("Listening port was invalid. Reset to default 6103.");
                MelonPreferences.Save();
            }

            StartServer(listeningPort.Value);

            LoggerInstance.WriteLine(37);
            LoggerInstance.Msg($"Server initialized on port {listeningPort.Value}");
            LoggerInstance.Msg($"Navigate to: http://localhost:{listeningPort.Value}/");
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
                        //LoggerInstance.Msg($"Request: {request.Url.AbsolutePath}");
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
                                    // Log param info
                                    LoggerInstance.Msg($"Parameter {paramInfo.Name}: {parameters[i]}");
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
                        else if (request.Url.AbsolutePath == "/favicon.ico")
                        {
                            ServeFavicon(response, Path.Combine(MelonEnvironment.UserDataDirectory, "SkRESTClient", "resources", "favicon.ico"));
                        }
                        else if (request.Url.AbsolutePath == "/commands")
                        {
                            if (commandHandlers != null && commandHandlers.Count > 0)
                            {
                                var commandsInfo = commandHandlers.Select(handler => new {
                                    Path = handler.Key,
                                    Parameters = handler.Value.Method.GetParameters()
                                                    .Select(p => new { p.Name, Type = p.ParameterType.Name })
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
                        else if (request.Url.AbsolutePath == "/game-variables")
                        {
                            if (gameVariableMethods != null && gameVariableMethods.Count > 0)
                            {

                                var variableValues = gameVariableMethods.Select(kvp => new
                                {
                                    VariableName = kvp.Key,
                                    Value = kvp.Value.Invoke().ToString()
                                }).ToDictionary(kvp => kvp.VariableName, kvp => kvp.Value);

                                var json = JsonConvert.SerializeObject(variableValues);
                                //Log json output
                                //LoggerInstance.Msg(json);
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
                        LoggerInstance.Msg($"Server error: {ex.Message}");
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

        private async System.Threading.Tasks.Task EnsureFileExists(string filePath, string url)
        {
            if (!File.Exists(filePath))
            {
                using (var httpClient = new HttpClient())
                {
                    var htmlContent = await httpClient.GetStringAsync(url);
                    File.WriteAllText(filePath, htmlContent);
                }
            }
        }

        private async System.Threading.Tasks.Task ServeHtmlPage(HttpListenerResponse response, string fileName)
        {
            var filePath = Path.Combine(MelonEnvironment.UserDataDirectory, "SkRESTClient", "resources", fileName);
            await EnsureFileExists(filePath, "https://raw.githubusercontent.com/derekShaheen/SkRESTClient/web/main/index.html");

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
            try
            {
                await EnsureFileExists(filePath, "https://raw.githubusercontent.com/derekShaheen/SkRESTClient/main/web/resources/favicon.ico");

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    response.ContentType = "image/x-icon";
                    response.ContentLength64 = fileInfo.Length;
                    using (var fileStream = fileInfo.OpenRead())
                    {
                        fileStream.CopyTo(response.OutputStream);
                    }
                    response.StatusCode = 200; // OK
                }
                else
                {
                    response.StatusCode = 404; // Not Found
                }
            }
            catch (Exception)
            {
                response.StatusCode = 500; // Internal Server Error
            }
            finally
            {
                response.Close();
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
            response.Headers.Add("Expires", "0");
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
                // Handle loading errors if necessary
                //LoggerInstance.Msg($"Error during discovery: {ex.LoaderExceptions.FirstOrDefault()?.Message}");
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
                LoggerInstance.Msg("No command handlers found.");
            }
            else
            {
                LoggerInstance.Msg($"Discovered {commandHandlers.Count} command handlers.");
            }
            if (gameVariableMethods == null || gameVariableMethods.Count == 0)
            {
                LoggerInstance.Msg("No game variable monitors found.");
            }
            else
            {
                LoggerInstance.Msg($"Discovered {gameVariableMethods.Count} game variable monitors.");
            }
        }

        public void LogResponse(HttpListenerResponse response, string message)
        {
            LoggerInstance.Msg(message);
            SendResponse(response, message);
        }

        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            if (listener != null)
            {
                LoggerInstance.Msg("Shutting down REST client...");
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

