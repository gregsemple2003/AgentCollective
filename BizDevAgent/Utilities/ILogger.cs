using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.Utilities
{
    public enum LogLevel
    {
        VeryVerbose,
        Verbose,
        Info,
        Warning,
        Error,
        Critical
    }

    public interface ILogger
    {
        void Log(string message = "", LogLevel level = LogLevel.Info);
    }

}
