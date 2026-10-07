namespace TLDSDashBoard.Models;

public enum AlarmSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>One row of the Overview page's "최근 10분 알람" log. Alarms are system-wide (any device can
/// raise one), not scoped to whichever device is currently selected in the 주파수/전압 charts above — so each
/// row carries its own <see cref="DeviceName"/> to say which device it came from.</summary>
public sealed class AlarmEventModel
{
    public required string Time { get; init; }
    public required string DeviceName { get; init; }
    public required string Message { get; init; }
    public required AlarmSeverity Severity { get; init; }

    public string SeverityLabel => Severity switch
    {
        AlarmSeverity.Critical => "Critical",
        AlarmSeverity.Warning => "Warning",
        _ => "Info"
    };
}
