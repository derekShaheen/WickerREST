﻿using System.Net;
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

namespace SkRESTClient
{
    public static class BuildInfo
    {
        public const string Name = "SkRESTClient";
        public const string Description = "SkRestClient";
        public const string Author = "Skrip";
        public const string Version = "1.0.0";
        public const string DownloadLink = "";
    }

    public class SkRESTClient : MelonMod
    {
        private Dictionary<string, (MethodInfo Method, string[] Parameters)>? commandHandlers;
        private Dictionary<string, Func<object>>? gameVariableMethods;

        private HttpListener? listener;
        private Thread? listenerThread;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // Instance of this class
        private static SkRESTClient? instance;
        private MelonPreferences_Category? modCategory;
        private MelonPreferences_Entry<int>? listeningPort;

        // Singleton pattern
        public static SkRESTClient Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SkRESTClient();
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
                commandHandlers = DiscoverCommandHandlers();
                gameVariableMethods = DiscoverGameVariables();
                OnCommandHandlersDiscovered(); // Notify complete
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
                        else if (request.Url.AbsolutePath == "/commands")
                        {
                            if (commandHandlers != null && commandHandlers.Count > 0)
                            {
                                var commandsInfo = commandHandlers.Select(handler => new {
                                    Path = handler.Key,
                                    Parameters = handler.Value.Method.GetParameters()
                                                    .Select(p => new { p.Name, Type = p.ParameterType.Name })
                                                    .ToArray()
                                }).ToArray();

                                var commandsJson = JsonConvert.SerializeObject(new { commands = commandsInfo });
                                SendResponse(response, commandsJson, 200, "application/json");
                            }
                            else
                            {
                                SendResponse(response, "No commands found.", 404);
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

        private async System.Threading.Tasks.Task EnsureHtmlPageExistsAsync(string filePath, string url)
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
            var filePath = Path.Combine(MelonEnvironment.UserDataDirectory, "SkRESTClient", fileName);
            await EnsureHtmlPageExistsAsync(filePath, "https://raw.githubusercontent.com/derekShaheen/SkRESTClient/main/index.html");

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

        private Dictionary<string, (MethodInfo Method, string[] Parameters)> DiscoverCommandHandlers()
        {
            var handlers = new Dictionary<string, (MethodInfo, string[])>();
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var attribute = method.GetCustomAttribute<CommandHandlerAttribute>();
                            if (attribute != null)
                            {
                                var path = attribute.Path;
                                if (handlers.ContainsKey(path))
                                {
                                    // If the key already exists, append a unique identifier
                                    var originalPath = path;
                                    int counter = 2;
                                    while (handlers.ContainsKey(path))
                                    {
                                        path = $"{originalPath}_{counter}";
                                        counter++;
                                    }
                                }
                                var parameterInfos = method.GetParameters();
                                var parameters = parameterInfos
                                                    .Where(param => param.ParameterType != typeof(HttpListenerResponse))
                                                    .Select(param => param.Name)
                                                    .ToArray();
                                handlers[attribute.Path] = (method, parameters);
                            }
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Handle loading errors if necessary
                //LoggerInstance.Msg($"Error loading command handler: {ex.LoaderExceptions.FirstOrDefault()?.Message}");
            }
            return handlers;
        }

        private Dictionary<string, Func<object>> DiscoverGameVariables()
        {
            var gameVariables = new Dictionary<string, Func<object>>();
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var attribute = method.GetCustomAttribute<GameVariableAttribute>();
                            if (attribute != null)
                            {
                                if (method.ReturnType != typeof(void) && method.GetParameters().Length == 0)
                                {
                                    Func<object> valueProvider = () => method.Invoke(null, null);
                                    gameVariables.Add(attribute.VariableName, valueProvider);
                                }
                            }
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Handle loading errors if necessary
                //LoggerInstance.Msg($"Error loading command handler: {ex.LoaderExceptions.FirstOrDefault()?.Message}");
            }
            return gameVariables;
        }


        private void OnCommandHandlersDiscovered()
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
                LoggerInstance.Msg("No variable monitors found.");
            }
            else
            {
                LoggerInstance.Msg($"Discovered {gameVariableMethods.Count} variable monitors.");
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

