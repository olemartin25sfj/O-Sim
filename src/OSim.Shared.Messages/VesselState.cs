namespace OSim.Shared.Messages;

public sealed record VesselState(
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    double HeadingDegrees,
    double SpeedKnots
);
