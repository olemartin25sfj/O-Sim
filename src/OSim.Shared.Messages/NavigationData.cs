namespace OSim.Shared.Messages;

// Immutable navigasjonsdata brukt på meldingsbussen
public sealed record NavigationData(
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    double SpeedKnots,
    double HeadingDegrees,
    double CourseOverGroundDegrees,
    bool IsAnchored = false,
    // Destinasjonsinformasjon for frontend
    bool HasDestination = false,
    double? TargetLatitude = null,
    double? TargetLongitude = null,
    bool HasArrived = false
);