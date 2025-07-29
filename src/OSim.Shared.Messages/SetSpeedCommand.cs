using System;

namespace OSim.Shared.Messages;

public class SetSpeedCommand
{
    public DateTime Timestamp { get; set; }
    public double DesiredSpeed { get; set; }
}