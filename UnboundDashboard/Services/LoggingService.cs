using System;
using System.IO;

namespace UnboundDashboard.Services
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public class LoggingService
    {
        private static readonly object _lock = new object();
        private readonly string _logPath;
        private readonly bool _enableFileLogging;

        public LoggingService(bool enableFileLogging = true)
        {
            _enableFileLogging = enableFileLogging;
            if (_enableFileLogging)
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                try
                {
                    Directory.CreateDirectory(logDir);
                    _logPath = Path.Combine(logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
                }
                catch
                {
                    // If we can't create log directory, disable file logging
                    _enableFileLogging = false;
                    _logPath = string.Empty;
                }
            }
            else
            {
                _logPath = string.Empty;
            }
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [{level}] {message}";

            if (exception != null)
            {
                logMessage += $"\nException: {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
            }

            // Console output (for debugging)
            try
            {
                Console.WriteLine(logMessage);
            }
            catch
            {
                // If console write fails, continue silently
            }

            // File output
            if (_enableFileLogging && !string.IsNullOrEmpty(_logPath))
            {
                try
                {
                    lock (_lock)
                    {
                        File.AppendAllText(_logPath, logMessage + Environment.NewLine);
                    }
                }
                catch
                {
                    // Can't log if logging fails - fail silently to avoid infinite loops
                }
            }
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);
        public void Critical(string message, Exception? ex = null) => Log(LogLevel.Critical, message, ex);
    }
}
