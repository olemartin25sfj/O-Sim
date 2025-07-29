namespace OSim.Shared.Messages;

public class SetEnvironmentModeCommand
{
    public DateTime Timestamp { get; set; }
    public string Mode { get; set; } = string.Empty;
}