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

namespace SkRest
{
    public static class BuildInfo
    {
        public const string Name = "SkRESTClient";
        public const string Description = "SkRestClient";
        public const string Author = "Skrip";
        public const string Version = "1.0.0";
        public const string DownloadLink = "";
    }

    public class SkRest : MelonMod
    {
        private Dictionary<string, MethodInfo>? commandHandlers;

        private HttpListener? listener;
        private Thread? listenerThread;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // Instance of this class
        private static SkRest? instance;
        private MelonPreferences_Category? modCategory;
        private MelonPreferences_Entry<int>? listeningPort;

        // Singleton pattern
        public static SkRest Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SkRest();
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

        private void HandleRequests(CancellationToken cancellationToken)
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
                        if (request.HttpMethod == "GET" && commandHandlers.TryGetValue(request.Url.AbsolutePath, out var handler))
                        {
                            MelonCoroutines.Start(ExecuteOnMainThread(() =>
                            {
                                handler.Invoke(null, new object[] { response });
                            }));
                        }
                        else if (request.Url.AbsolutePath == "/")
                        {
                            ServeHtmlPage(response, "index.html");
                        }
                        else if (request.Url.AbsolutePath == "/commands")
                        {
                            if (commandHandlers != null && commandHandlers.Count > 0)
                            {
                                var commandsJson = JsonConvert.SerializeObject(new { commands = commandHandlers.Keys.ToArray() });
                                SendResponse(response, commandsJson, 200, "application/json");
                            }
                            else
                            {
                                SendResponse(response, "No commands found.", 404);
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
                    //User is trying to close the server improperly. Handle on application close.
                }
            }
        }

        private void ServeHtmlPage(HttpListenerResponse response, string fileName)
        {
            var filePath = Path.Combine(MelonEnvironment.UserDataDirectory, "SkRESTClient", fileName);
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
            response.StatusCode = statusCode;
            response.ContentType = contentType;
            var buffer = System.Text.Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private Dictionary<string, MethodInfo> DiscoverCommandHandlers()
        {
            var handlers = new Dictionary<string, MethodInfo>();
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
                                handlers[path] = method;
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
        }

        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            if (listener != null)
            {
                cancellationTokenSource.Cancel(); // Signal cancellation
                listener.Stop(); // Stop the listener
                listener.Close(); // Clean up listener resources
            }
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(); // Wait for the thread to complete execution
            }
        }

    }
}

