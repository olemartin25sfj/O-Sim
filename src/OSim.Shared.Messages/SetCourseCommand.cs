using System;

namespace OSim.Shared.Messages;

public class SetCourseCommand
{
    public DateTime Timestamp { get; set; }
    public double TargetCourseDegrees { get; set; }
}