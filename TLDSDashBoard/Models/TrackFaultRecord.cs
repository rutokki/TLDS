namespace TLDSDashBoard.Models;

/// <summary>
/// One row of the 궤도 장애 및 경보 리스트 (track fault &amp; alarm list): a fault event on one track
/// circuit, plus the full 12-channel TX/RX measurement snapshot (표준/측정/점검/최소 each) taken at
/// that event — mirrors the same 12 channels as <see cref="ChannelDataSet"/>.
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

    public required ChannelMeasurement Tac { get; init; } // 송신 AC
    public required ChannelMeasurement Tv { get; init; }  // 송신 출력전압
    public required ChannelMeasurement Tfp { get; init; } // 송신 정펄스
    public required ChannelMeasurement Tsp { get; init; } // 송신 부펄스
    public required ChannelMeasurement Thz { get; init; } // 송신 주파수
    public required ChannelMeasurement Ta { get; init; }  // 송신 전류
    public required ChannelMeasurement Rfp { get; init; } // 수신 정펄스
    public required ChannelMeasurement Rsp { get; init; } // 수신 부펄스
    public required ChannelMeasurement Rhz { get; init; } // 수신 주파수
    public required ChannelMeasurement Ra { get; init; }  // 수신 전류
    public required ChannelMeasurement Rv1 { get; init; } // 수신 전압1
    public required ChannelMeasurement Rv2 { get; init; } // 수신 전압2
}
