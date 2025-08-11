namespace OSim.Shared.Messages;

public sealed record SetSpeedCommand(
    DateTime TimestampUtc,
    double TargetSpeedKnots
);