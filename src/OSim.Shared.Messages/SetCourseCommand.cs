namespace OSim.Shared.Messages;

public sealed record SetCourseCommand(
    DateTime TimestampUtc,
    double TargetCourseDegrees
);