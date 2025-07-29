using System;

namespace OSim.Shared.Messages;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Level { get; set; } = "Info"; // Default to Info, can be changed to Warning, Error, Trace, Debug
}
