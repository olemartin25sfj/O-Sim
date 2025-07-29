using System;

namespace OSim.Shared.Messages;

public class AlarmTriggered
{
    public DateTime Timestamp { get; set; }
    public string AlarmType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // Default to Warning, can be changed to Critical or Info
}