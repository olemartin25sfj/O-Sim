namespace OSim.Shared.Messages;

public sealed record RudderCommand(
    DateTime TimestampUtc,
    double RudderAngleDegrees
); // Negativ babord, positiv styrbord