using System;

namespace OSim.Shared.Messages;

public class ThrustCommand
{
    public DateTime Timestamp { get; set; }
    public double Thrust { get; set; }
}