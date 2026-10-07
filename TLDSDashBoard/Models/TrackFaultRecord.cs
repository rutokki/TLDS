namespace TLDSDashBoard.Models;

/// <summary>
/// One row of the 궤도 장애 및 경보 리스트 (track fault &amp; alarm list): a fault event on one track
/// circuit, plus the full 6-channel (주파수 3 + 전압 3) measurement snapshot (표준/측정/점검/최소 each) taken at
/// that event — mirrors the same 6 channels as <see cref="ChannelDataSet"/>.
/// </summary>
public sealed class TrackFaultRecord
{
    public required string TrackName { get; init; }
    public required string Date { get; init; }
    public required string OccurredAt { get; init; }
    public required string RecoveredAt { get; init; }
    public required string FaultType { get; init; }
    public required string Imp { get; init; }
    public required string Af { get; init; }
    public required string TrackType { get; init; }

    public required ChannelMeasurement TRK_FREQ { get; init; } // 궤도 주파수
    public required ChannelMeasurement CAB_FREQ { get; init; }  // 차상 주파수
    public required ChannelMeasurement CODE_FREQ { get; init; } // 지상 주파수
    public required ChannelMeasurement TX_VOLTAGE { get; init; } // 송신 전압
    public required ChannelMeasurement RX_A_VOLTAGE { get; init; } // 수신 A 전압
    public required ChannelMeasurement RX_B_VOLTAGE { get; init; }  // 수신 B 전압

}
