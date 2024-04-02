using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkRESTClient
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandHandlerAttribute : Attribute
    {
        public string Path { get; }
        public CommandHandlerAttribute(string path)
        {
            Path = path;
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
