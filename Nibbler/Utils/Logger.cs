using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.Utils
{
    public class Logger : ILogger
    {
        public Logger(string name, bool enabled)
        {
            Name = name;
            Enabled = enabled;
        }

        public string Name { get; }
        public bool Enabled { get; private set; }

        public void SetEnable(bool enabled)
        {
            Enabled = enabled;
        }

        public void LogDebug(Exception ex, string message)
        {
            if (Enabled)
            {
                Console.WriteLine($"[{Name}] {message}: {ex}");
            }
        }

        public void LogDebug(string message)
        {
            if (Enabled)
            {
                Console.WriteLine($"[{Name}] {message}");
            }
        }

        public void LogWarning(string message)
        {
            if (Enabled)
            {
                Console.WriteLine($"[{Name}] [WARN] {message}");
            }
        }
    }
}
