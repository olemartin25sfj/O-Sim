namespace OSim.Shared.Messages;

public enum EnvironmentMode
{
    Static = 0,
    Dynamic = 1,
    Storm = 2,
    Calm = 3
}

public sealed record EnvironmentData(
    DateTime TimestampUtc,
    EnvironmentMode Mode,
    double WindSpeedKnots,
    double WindDirectionDegrees,
    double CurrentSpeedKnots,
    double CurrentDirectionDegrees,
    double WaveHeightMeters,
    double WaveDirectionDegrees,
    double WavePeriodSeconds
);