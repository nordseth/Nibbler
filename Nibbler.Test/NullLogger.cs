namespace Nibbler.Test;

public class NullLogger : ILogger
{
    private static ILogger _instance = new NullLogger();
    public static ILogger Instance => _instance;

    public bool DebugEnabled => false;

    public bool WarningEnabled => false;

    public bool TraceEnabled => false;

    public void LogDebug(Exception ex, string message)
    {
    }

    public void LogDebug(string message)
    {
    }

    public void LogTrace(string message)
    {
    }

    public void LogWarning(string message)
    {
    }

    public void SetDebugEnable(bool enabled)
    {
    }

    public void SetTraceEnable(bool enabled)
    {
    }

    public void SetWarningEnable(bool enabled)
    {
    }
}
