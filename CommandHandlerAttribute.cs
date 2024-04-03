using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkInterface
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandHandlerAttribute : Attribute
    {
        public string Path { get; }
        public string Category { get; }

        // Adjust the constructor to accept category as an optional parameter
        public CommandHandlerAttribute(string path, string category = "Miscellaneous")
        {
            Path = "/" + path;
            Category = category;
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
