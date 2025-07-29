using System;

namespace OSim.Shared.Messages;

public class EnvironmentData
{
    public DateTime Timestamp { get; set; }

    public double WindSpeedKnots { get; set; }
    public double WindDirection { get; set; }

    public double CurrentSpeed { get; set; }
    public double CurrentDirection { get; set; }

    public double WaveHeight { get; set; }
    public double WaveDirection { get; set; }
    public double WavePeriod { get; set; }
}