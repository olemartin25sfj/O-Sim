using System;

namespace OSim.Shared.Messages;

public class VesselState
{
    public DateTime Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Heading { get; set; }
    public double Speed { get; set; }
}
