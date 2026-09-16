namespace TLDSDashBoard.Models;

/// <summary>
/// The twelve TX/RX telemetry series, one sample per <see cref="Timestamps"/> entry (DB-backed:
/// one row per minute, all 12 channels sampled together — see Services/SqliteChannelDataRepository).
/// </summary>
public sealed class ChannelDataSet
{
    /// <summary>Sample time for index i of every channel array below (same length as each array). 1-minute sampling interval.</summary>
    public required DateTime[] Timestamps { get; init; }

    public required double[] TAC { get; init; } // 송신 AC
    public required double[] TV { get; init; } // 송신 출력전압 V
    public required double[] TFP { get; init; } // 송신 정펄스
    public required double[] TSP { get; init; } // 송신 부펄스
    public required double[] THZ { get; init; } // 송신 주파수
    public required double[] TA { get; init; } // 송신 전류
    public required double[] RFP { get; init; } // 수신 정펄스
    public required double[] RSP { get; init; } // 수신 부펄스
    public required double[] RHZ { get; init; } // 수신 주파수
    public required double[] RA { get; init; } // 수신 전류
    public required double[] RV1 { get; init; } // 수신 전압1
    public required double[] RV2 { get; init; } // 수신 전압2
}
