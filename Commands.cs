using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wicker;

namespace WickerREST
{
    internal class Commands
    {
        // Instance of this class
        private static Commands? instance;

        // Singleton pattern
        public static Commands Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Commands();
                }
                return instance;
            }
            private set
            {
                instance = value;
            }
        }

        public Dictionary<string, (MethodInfo Method, string[] Parameters, string Category)>? CommandHandlers { get => commandHandlers; set => commandHandlers = value; }
        public Dictionary<string, Func<object>>? GameVariableMethods { get => gameVariableMethods; set => gameVariableMethods = value; }

        private Dictionary<string, (MethodInfo Method, string[] Parameters, string Category)>? commandHandlers;
        private Dictionary<string, Func<object>>? gameVariableMethods;

        public void DiscoverHandlersAndVariables()
        {
            CommandHandlers = new Dictionary<string, (MethodInfo, string[], string)>();
            GameVariableMethods = new Dictionary<string, Func<object>>();

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
                                string path = Utilities.EnsureUniqueKey(CommandHandlers.Keys, commandAttribute.Path);
                                var parameters = method.GetParameters()
                                                        .Where(param => param.ParameterType != typeof(HttpListenerResponse))
                                                        .Select(param => param.Name)
                                                        .ToArray();
                                CommandHandlers[path] = (method, parameters, commandAttribute.Category ?? string.Empty);
                            }

                            // Discover Game Variables
                            var gameVariableAttribute = method.GetCustomAttribute<GameVariableAttribute>();
                            if (gameVariableAttribute != null && method.ReturnType != typeof(void) && method.GetParameters().Length == 0)
                            {
                                string variableName = Utilities.EnsureUniqueKey(GameVariableMethods.Keys, gameVariableAttribute.VariableName);
                                Func<object> valueProvider = () => method.Invoke(null, null);
                                GameVariableMethods[variableName] = valueProvider;
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

        public void OnCommandHandlersDiscovered()
        {
            if (CommandHandlers == null || CommandHandlers.Count == 0)
            {
                WickerServer.Instance.LogMessage("No command handlers found.");
            }
            else
            {
                WickerServer.Instance.LogMessage($"Discovered {CommandHandlers.Count} command handlers.");
            }
            if (GameVariableMethods == null || GameVariableMethods.Count == 0)
            {
                WickerServer.Instance.LogMessage("No game variable monitors found.");
            }
            else
            {
                WickerServer.Instance.LogMessage($"Discovered {GameVariableMethods.Count} game variable monitors.");
            }
        }
    }
}
