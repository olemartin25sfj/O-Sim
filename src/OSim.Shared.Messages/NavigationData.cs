namespace OSim.Shared.Messages;

public class NavigationData
{
    public DateTime Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SpeedKnots { get; set; }
    public double Heading { get; set; }
    public double CourseOverGround { get; set; }
}