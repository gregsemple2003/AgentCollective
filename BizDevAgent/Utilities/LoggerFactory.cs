using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BizDevAgent.Utilities
{
    public class Logger : ILogger
    {
        private readonly LogLevel _configuredLevel;
        private readonly string _category;

        public Logger(string category, LogLevel configuredLevel)
        {
            _category = category;
            _configuredLevel = configuredLevel;
        }

        public void Log(string message, LogLevel level)
        {
            if (level >= _configuredLevel)
            {
                Console.WriteLine($"[{DateTime.Now}][{_category}] {level}: {message}");

                Debug.WriteLine(message);
            }
        }

        public static void LogFileAndLine(string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            Console.WriteLine($"[{file}:{line}] {message}");

            Debug.WriteLine(message);
        }
    }

    public class LoggerFactory
    {
        private readonly Dictionary<string, LogLevel> _categoryLogLevels;

        public LoggerFactory()
        {
            _categoryLogLevels = new Dictionary<string, LogLevel>();
        }

        public ILogger CreateLogger(string category)
        {
            if (_categoryLogLevels.TryGetValue(category, out var level))
            {
                return new Logger(category, level);
            }
            else
            {
                // Fallback or default log level if the category is not configured
                return new Logger(category, LogLevel.Info);
            }
        }
    }
}
