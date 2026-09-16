namespace TLDSDashBoard.Models;

public enum AlarmSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// One row of the alarm/event log table.
/// </summary>
public sealed class AlarmEventModel
{
    public required string Time { get; init; }
    public required string Channel { get; init; }
    public required string Message { get; init; }
    public required AlarmSeverity Severity { get; init; }

    public string SeverityLabel => Severity switch
    {
        AlarmSeverity.Critical => "Critical",
        AlarmSeverity.Warning => "Warning",
        _ => "Info"
    };
}
