using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.Utils
{
    public interface ILogger
    {
        bool DebugEnabled { get; }
        bool WarningEnabled { get; }
        void SetDebugEnable(bool enabled);
        void SetWarningEnable(bool enabled);

        void LogDebug(Exception ex, string message);
        void LogDebug(string message);
        void LogWarning(string message);
    }
}
