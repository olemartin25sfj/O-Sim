namespace OSim.Shared.Messages;

public class RudderCommand
{
    public DateTime Timestamp { get; set; }
    public double RudderAngle { get; set; } // I grader, negativt for babord, positivt for styrbord
}