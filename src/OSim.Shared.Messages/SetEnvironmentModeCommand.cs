namespace OSim.Shared.Messages;

public sealed record SetEnvironmentModeCommand(
    DateTime TimestampUtc,
    EnvironmentMode Mode
);