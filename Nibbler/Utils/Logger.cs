using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.Utils
{
    public class Logger : ILogger
    {
        public Logger(string name, bool debugEnable)
        {
            Name = name;
            DebugEnabled = debugEnable;
            WarningEnabled = true;
            TraceEnabled = false;
        }

        public string Name { get; }
        public bool DebugEnabled { get; private set; }
        public bool WarningEnabled { get; private set; }
        public bool TraceEnabled { get; private set; }

        public void SetDebugEnable(bool enabled)
        {
            DebugEnabled = enabled;
        }

        public void SetWarningEnable(bool enabled)
        {
            WarningEnabled = enabled;
        }

        public void SetTraceEnable(bool enabled)
        {
            TraceEnabled = enabled;
        }

        public void LogDebug(Exception ex, string message)
        {
            if (DebugEnabled)
            {
                Console.WriteLine($"[{Name}] {message}: {ex}");
            }
        }

        public void LogDebug(string message)
        {
            if (DebugEnabled)
            {
                Console.WriteLine($"[{Name}] {message}");
            }
        }

        public void LogWarning(string message)
        {
            if (WarningEnabled)
            {
                Console.WriteLine($"[{Name}] [WARN] {message}");
            }
        }

        public void LogTrace(string message)
        {
            if (TraceEnabled)
            {
                Console.WriteLine($"[{Name}] [TRACE] {message}");
            }
        }
    }
}
