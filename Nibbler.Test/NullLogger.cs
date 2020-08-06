using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.Test
{
    public class NullLogger : ILogger
    {
        private static ILogger _instance = new NullLogger();
        public static ILogger Instance => _instance;

        public bool Enabled => false;

        public void LogDebug(Exception ex, string message)
        {
        }

        public void LogDebug(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void SetEnable(bool enabled)
        {
        }
    }
}
