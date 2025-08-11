namespace OSim.Shared.Messages;

public sealed record ThrustCommand(
    DateTime TimestampUtc,
    double ThrustPercent
);