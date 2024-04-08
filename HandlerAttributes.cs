using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WickerREST
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandHandlerAttribute : Attribute
    {
        public string Path { get; }
        public string Category { get; }
        public string Description { get; }
        public string AutoCompleteMethodName { get; set; } // New property

        public CommandHandlerAttribute(string path, string category = "Miscellaneous", string description = "", string autoCompleteMethodName = "")
        {
            Path = "/" + path;
            Category = category;
            Description = description;
            AutoCompleteMethodName = autoCompleteMethodName;
        }

        //Example usage with autocomplete

        //[CommandHandler("SpawnResource", "Resources", "desc", "SpawnResourceAutoComplete")]
        //public static void SpawnResourceHttp(HttpListenerResponse response, string resource = "Gold", string isInfinite = "False")

        //public static Dictionary<string, string[]> SpawnResourceAutoComplete()
        //{
        //    Dictionary<string, string[]> responseOptions = new Dictionary<string, string[]>();

        //    responseOptions["resource"] = Enum.GetNames(typeof(Minerals.MineralTypes));
        //    responseOptions["isInfinite"] = new[] { "False", "True" };

        //    return responseOptions;
        //}
    }


    [AttributeUsage(AttributeTargets.Method)]
    public class GameVariableAttribute : Attribute
    {
        public string VariableName { get; }

        public GameVariableAttribute(string variableName)
        {
            VariableName = variableName;
        }
    }

}
