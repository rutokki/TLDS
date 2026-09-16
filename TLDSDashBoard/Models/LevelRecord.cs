namespace TLDSDashBoard.Models;

/// <summary>One row of the 레벨 기록 리스트 (level record list): a periodic RX-channel measurement snapshot for one track circuit, one recording date + hour block.</summary>
public sealed class LevelRecord
{
    public required string StationName { get; init; }
    public required string TrackName { get; init; }
    public required string RecordDate { get; init; }
    public required string HourRangeLabel { get; init; }

    public required ChannelMeasurement Rfp { get; init; } // 수신 정펄스
    public required ChannelMeasurement Rsp { get; init; } // 수신 부펄스
    public required ChannelMeasurement Rhz { get; init; } // 수신 주파수
    public required ChannelMeasurement Ra { get; init; }  // 수신 평균전류
    public required ChannelMeasurement Rv1 { get; init; } // 수신 V1전압
    public required ChannelMeasurement Rv2 { get; init; } // 수신 V2전압
}
