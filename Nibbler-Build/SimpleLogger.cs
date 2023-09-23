using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NibblerBuild;

public class SimpleLogger : Logger
{
    private readonly string _prefix;
    private readonly TextWriter _writer;

    public SimpleLogger(string prefix, TextWriter writer)
    {
        _prefix = prefix;
        _writer = writer;
    }

    public override void Initialize(IEventSource eventSource)
    {
        eventSource.WarningRaised += EventSource_WarningRaised;
        eventSource.ErrorRaised += EventSource_ErrorRaised;
        eventSource.MessageRaised += EventSource_MessageRaised;
    }

    private void EventSource_MessageRaised(object sender, BuildMessageEventArgs e)
    {
        if (e.Importance == MessageImportance.High && e.SenderName != "Csc")
        {
            _writer.Write(_prefix);
            _writer.Write(" ");
            _writer.WriteLine(e.Message);
        }
    }

    private void EventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
    {
        _writer.WriteLine($"{_prefix} {e.File}({e.LineNumber},{e.ColumnNumber}): error {e.Code}: {e.Message}");
    }

    private void EventSource_WarningRaised(object sender, BuildWarningEventArgs e)
    {
        _writer.WriteLine($"{_prefix} {e.File}({e.LineNumber},{e.ColumnNumber}): warning {e.Code}: {e.Message}");
    }
}
