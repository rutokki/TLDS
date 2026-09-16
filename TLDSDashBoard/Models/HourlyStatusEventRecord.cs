namespace TLDSDashBoard.Models;

/// <summary>The left-hand category list in the 시간별 상태 출력 (hourly status output) modal.</summary>
public enum HourlyStatusCategory
{
    TrackFault,          // 궤도 장애
    SignalFailure,       // 신호기 고장
    SwitchMachineFailure,// 선로전환기 고장
    BlockDeviceFailure,  // 폐색장치 고장
    LeuFailure,          // LEU 고장
    SignalEquipmentEvent,// 신호설비 이벤트
    SystemEvent          // 시스템 이벤트
}

/// <summary>One row of the 시간별 상태 출력 (hourly status output) result table.</summary>
public sealed class HourlyStatusEventRecord
{
    public required string OccurredAt { get; init; }
    public required string StationName { get; init; }
    public required string DeviceName { get; init; }
    public required string Content { get; init; }
}
