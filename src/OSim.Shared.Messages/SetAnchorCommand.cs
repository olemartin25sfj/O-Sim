namespace OSim.Shared.Messages;

public record SetAnchorCommand(
    DateTime TimestampUtc,
    bool IsAnchored
);
