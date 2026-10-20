namespace TLDSDashBoard.Models;

public enum AlarmSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>Overview 페이지 "최근 10분 알람" 로그의 한 행. 알람은 시스템 전체 단위(어느 장치든 발생시킬 수
/// 있음)이며, 위쪽 주파수/전압 차트에서 현재 선택된 장치에 한정되지 않는다 — 그래서 각 행이 자신의
/// <see cref="DeviceName"/>을 들고 어느 장치에서 왔는지 나타낸다.</summary>
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
