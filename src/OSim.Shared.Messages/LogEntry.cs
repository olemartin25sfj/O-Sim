namespace OSim.Shared.Messages;

public sealed record LogEntry(
    DateTime TimestampUtc,
    string Service,
    string Level,
    string? Message = null,
    string? CorrelationId = null
);
