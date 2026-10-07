namespace TLDSDashBoard.Models;

/// <summary>One row of the 레벨 기록 리스트 (level record list): a periodic full 6-channel (주파수 3 + 전압 3)
/// measurement snapshot for one track circuit, one recording date + hour block — mirrors the same
/// 6 channels as <see cref="TrackFaultRecord"/>/<see cref="ChannelDataSet"/> (see Services/ChannelCatalog).</summary>
public sealed class LevelRecord
{
    public required string StationName { get; init; }
    public required string TrackName { get; init; }
    public required string RecordDate { get; init; }
    public required string HourRangeLabel { get; init; }
    /// <summary>The row's exact sample time ("HH:mm:ss") — one row exists per second of real telemetry
    /// within the selected 시간대, unlike <see cref="HourRangeLabel"/> which is just the coarse hour block.</summary>
    public required string TimeLabel { get; init; }

    public required ChannelMeasurement TRK_FREQ { get; init; } // 궤도 주파수
    public required ChannelMeasurement CAB_FREQ { get; init; }  // 차상 주파수
    public required ChannelMeasurement CODE_FREQ { get; init; } // 지상 주파수
    public required ChannelMeasurement TX_VOLTAGE { get; init; } // 송신 전압
    public required ChannelMeasurement RX_A_VOLTAGE { get; init; } // 수신 A 전압
    public required ChannelMeasurement RX_B_VOLTAGE { get; init; }  // 수신 B 전압

}
