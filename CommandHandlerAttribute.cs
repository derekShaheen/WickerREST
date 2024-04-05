using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wicker
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandHandlerAttribute : Attribute
    {
        public string Path { get; }
        public string Category { get; }
        public string Description { get; }

        // Adjust the constructor to accept category as an optional parameter
        public CommandHandlerAttribute(string path, string category = "Miscellaneous", string description = "")
        {
            Path = "/" + path;
            Category = category;
            Description = description;
        }
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
