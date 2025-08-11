namespace OSim.Shared.Messages;

// Immutable navigasjonsdata brukt på meldingsbussen
public sealed record NavigationData(
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    double SpeedKnots,
    double HeadingDegrees,
    double CourseOverGroundDegrees
);