namespace TLDSDashBoard.Models;

public enum AlarmSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>One row of the Overview page's "최근 10분 알람" log — scoped to the same 10-minute window as the TX/RX charts above it.</summary>
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
