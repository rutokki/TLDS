namespace TLDSDashBoard.Models;

/// <summary>레벨 기록 리스트의 한 행: 한 궤도회로의 6채널(주파수 3 + 전압 3) 전체 측정 스냅샷(기록일자 + 시간 블록
/// 하나) — <see cref="TrackFaultRecord"/>/<see cref="ChannelDataSet"/>과 같은 6채널을 따른다
/// (Services/ChannelCatalog 참고).</summary>
public sealed class LevelRecord
{
    public required string StationName { get; init; }
    public required string TrackName { get; init; }
    public required string RecordDate { get; init; }
    public required string HourRangeLabel { get; init; }
    /// <summary>행의 정확한 샘플 시각("HH:mm:ss") — 선택한 시간대 안에서 실데이터 1초당 1행이 있으며,
    /// 대략적인 시간 블록일 뿐인 <see cref="HourRangeLabel"/>과 다르다.</summary>
    public required string TimeLabel { get; init; }

    public required ChannelMeasurement TRK_FREQ { get; init; } // 궤도 주파수
    public required ChannelMeasurement CAB_FREQ { get; init; }  // 차상 주파수
    public required ChannelMeasurement CODE_FREQ { get; init; } // 지상 주파수
    public required ChannelMeasurement TX_VOLTAGE { get; init; } // 송신 전압
    public required ChannelMeasurement RX_A_VOLTAGE { get; init; } // 수신 A 전압
    public required ChannelMeasurement RX_B_VOLTAGE { get; init; }  // 수신 B 전압

}
