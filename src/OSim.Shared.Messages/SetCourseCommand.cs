namespace OSim.Shared.Messages;

public class SetCourseCommand
{
    public DateTime Timestamp { get; set; }
    public double DesiredCourse { get; set; }
}