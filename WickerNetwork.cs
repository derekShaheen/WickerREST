using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using UnityEngine;
using Wicker;
using static MelonLoader.MelonLogger;

namespace WickerREST
{
    public class WickerNetwork
    {
        private HttpListener? listener;
        private Thread? listenerThread;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private const string COMMANDS_PATH = "/commands";
        private const string GAME_VARIABLES_PATH = "/game-variables";
        private const string FAVICON_PATH = "/favicon.ico";
        private const string INDEX_URL = @"https://raw.githubusercontent.com/derekShaheen/WickerREST/main/web/index.html";
        private const string FAVICON_URL = @"https://raw.githubusercontent.com/derekShaheen/WickerREST/main/web/resources/favicon.ico";
        private const string DWYL_URL = @"https://hits.dwyl.com/derekShaheen/WickerREST.svg";

        // Instance of this class
        private static WickerNetwork? instance;

        // Singleton pattern
        public static WickerNetwork Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new WickerNetwork();
                }
                return instance;
            }
            private set
            {
                instance = value;
            }
        }

        internal void StartServer(int port, CancellationTokenSource cancellationToken)
        {
            cancellationTokenSource = cancellationToken;
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");


            listener.Start();
            listenerThread = new Thread(() => HandleRequests(cancellationTokenSource.Token));
            WickerServer.Instance.LogMessage("Starting listener thread...", 1);
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

                    if (request == null || response == null || request.Url == null || Commands.Instance.CommandHandlers == null)
                        return;

                    try
                    {
                        if (request.HttpMethod == "GET" && Commands.Instance.CommandHandlers.TryGetValue(request.Url.AbsolutePath, out var handlerTuple))
                        {
                            MethodInfo methodToInvoke = handlerTuple.Method;
                            var parameterInfos = methodToInvoke.GetParameters();

                            // Assuming all parameters are strings for simplicity; adjust as needed.
                            object[]? parameters = new object[parameterInfos.Length];

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
                                    if(paramValue != null)
                                        parameters[i] = Convert.ChangeType(paramValue, paramInfo.ParameterType);
                                }
                            }
                            // Queue the action to be executed on the main thread
                            WickerServer.Instance.ExecuteOnMainThread(() =>
                            {
                                methodToInvoke.Invoke(null, parameters);
                            }, response);
                        }
                        else if (request.Url.AbsolutePath == "/")
                        {
                            await ServeHtmlPage(response, "index.html");
                        }
                        else if (request.Url.AbsolutePath == FAVICON_PATH)
                        {
                            await ServeFavicon(response, Path.Combine(WickerServer.resourcesPath, "favicon.ico"));
                        }
                        else if (request.Url.AbsolutePath == COMMANDS_PATH)
                        {
                            ServeCommandHandlers(response);
                        }
                        else if (request.Url.AbsolutePath == GAME_VARIABLES_PATH)
                        {
                            if (Commands.Instance.GameVariableMethods != null && Commands.Instance.GameVariableMethods.Count > 0)
                            {

                                var variableValues = Commands.Instance.GameVariableMethods.Select(kvp => new
                                {
                                    VariableName = kvp.Key,
                                    Value = kvp.Value?.Invoke()?.ToString()
                                }).ToDictionary(kvp => kvp.VariableName, kvp => kvp.Value);

                                var json = JsonConvert.SerializeObject(variableValues);
                                SendResponse(response, json, statusCode: 200, contentType: "application/json");
                            }
                            else
                            {
                                SendResponse(response, "No game variables found.", statusCode: 200);
                            }
                        }
                        else
                        {
                            SendResponse(response, "Invalid request.", statusCode: 404);
                        }
                        //response.OutputStream.Close();
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException) break;
                        WickerServer.Instance.LogMessage($"Server error: {ex.Message}", 1);
                        SendResponse(response, $"Server error. {ex.Message}", statusCode: 500);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is HttpListenerException || ex is ObjectDisposedException)
                {
                    //User is trying to close the server improperly. Suppress here, handle on application quit.
                }
            }
        }

        private void ServeCommandHandlers(HttpListenerResponse response)
        {
            if (Commands.Instance.CommandHandlers != null && Commands.Instance.CommandHandlers.Count > 0)
            {
                //await EnsureFileExists("/dwyl", DWYL_URL, true);
                var commandsInfo = Commands.Instance.CommandHandlers.Select(handler => new {
                    Path = handler.Key,
                    Parameters = handler.Value.Method.GetParameters()
                                    .Select(p => new {
                                        p.Name,
                                        Type = p.ParameterType.Name,
                                        DefaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                                    })
                                    .ToArray(),
                    Category = handler.Value.Category,
                    Description = handler.Value.Description
                }).ToArray();

                var productName = Application.productName;
                var responseContent = new
                {
                    productName = productName,
                    commands = commandsInfo,
                };

                var commandsJson = JsonConvert.SerializeObject(responseContent);
                SendResponse(response, commandsJson, statusCode: 200, contentType: "application/json");
            }
            else
            {
                SendResponse(response, @"", statusCode: 200);
            }
        }

        private async System.Threading.Tasks.Task ServeHtmlPage(HttpListenerResponse response, string fileName)
        {
            var filePath = Path.Combine(WickerServer.resourcesPath, fileName);
            await Utilities.EnsureFileExists(filePath, INDEX_URL);

            if (File.Exists(filePath))
            {
                var pageContent = File.ReadAllBytes(filePath);
                SendResponse(response, System.Text.Encoding.UTF8.GetString(pageContent), statusCode: 200, contentType: "text/html");
            }
            else
            {
                SendResponse(response, "Page not found.", statusCode: 404);
            }
        }

        private async System.Threading.Tasks.Task ServeFavicon(HttpListenerResponse response, string filePath)
        {
            await Utilities.EnsureFileExists(filePath, FAVICON_URL, true, true);

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
                catch (Exception)
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

        internal void StopServer()
        {
            if (listener != null)
            {
                WickerServer.Instance.LogMessage("Shutting down REST client...");
                listener.Stop(); // Stop the listener
                listener.Close(); // Clean up listener resources
                cancellationTokenSource.Cancel(); // Signal cancellation
            }
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(); // Wait for the thread to complete execution
            }
        }

        public void SendResponse(HttpListenerResponse response, string message, bool closeResponse = true, int statusCode = 200, string contentType = "text/plain")
        {
            try
            {
                // Attempt to set response properties and write the response
                response.StatusCode = statusCode;
                response.ContentType = contentType;
                var buffer = System.Text.Encoding.UTF8.GetBytes(message);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (InvalidOperationException ex)
            {
                // This exception is thrown if we try to modify the response after it's been sent
                // Log the exception or handle it as needed
                WickerServer.Instance.LogMessage($"Attempted to write to an already closed response: {ex.Message}");
            }
            finally
            {
                // Always ensure the output stream is closed in a finally block to avoid resource leaks
                try
                {
                    if (closeResponse)
                    {
                        response.OutputStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    // Handle or log exceptions thrown from trying to close the output stream
                    WickerServer.Instance.LogMessage($"Error closing response output stream: {ex.Message}");
                }
            }
        }

        public void LogResponse(HttpListenerResponse response, string message)
        {
            // Replace newline characters with HTML line breaks to preserve formatting in the web page
            string formattedMessage = message.Replace(Environment.NewLine, "<br>");
            SendResponse(response, formattedMessage, statusCode: 200, contentType: "text/html");
        }

    }
}
