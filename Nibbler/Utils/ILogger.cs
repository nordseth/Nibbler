using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.Utils
{
    public interface ILogger
    {
        bool Enabled { get; }
        void SetEnable(bool enabled);

        void LogDebug(Exception ex, string message);
        void LogDebug(string message);
        void LogWarning(string message);
    }
}
