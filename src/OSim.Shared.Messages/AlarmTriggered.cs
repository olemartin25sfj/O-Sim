namespace OSim.Shared.Messages;

public enum AlarmSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public sealed record AlarmTriggered(
    DateTime TimestampUtc,
    string AlarmType,
    string Message,
    AlarmSeverity Severity
);