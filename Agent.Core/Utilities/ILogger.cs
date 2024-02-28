using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core
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
        void Info(string message) { Log(message, LogLevel.Info); }
        void Warning(string message) { Log(message, LogLevel.Warning); }
        void Error(string message) { Log(message, LogLevel.Error); }

    }

}
