using System;

namespace OSim.Shared.Messages;

public class SetPositionCommand
{
    public DateTime Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
