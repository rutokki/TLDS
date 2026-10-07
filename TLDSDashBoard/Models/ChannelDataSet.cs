namespace TLDSDashBoard.Models;

/// <summary>
/// The six telemetry series (주파수 3 + 전압 3), one sample per <see cref="Timestamps"/> entry (DB-backed:
/// one row per second, all channels sampled together — see Services/RealTelemetryRepository).
/// </summary>
public sealed class ChannelDataSet
{
    /// <summary>Sample time for index i of every channel array below (same length as each array). 1-second sampling interval.</summary>
    public required DateTime[] Timestamps { get; init; }

    public required double[] TRK_FREQ { get; init; } // 궤도 주파수 (Value0)
    public required double[] CAB_FREQ { get; init; } // 차상 주파수 (Value1)
    public required double[] CODE_FREQ { get; init; } // 지상 주파수 (Value2)
    public required double[] TX_VOLTAGE { get; init; } // 송신 전압 (Value3)
    public required double[] RX_A_VOLTAGE { get; init; } // 수신 A 전압 (Value4)
    public required double[] RX_B_VOLTAGE { get; init; } // 수신 B 전압 (Value5)

    /// <summary>Series by channel id (Services/ChannelCatalog ids) — lets pages loop over the catalog
    /// instead of naming each property.</summary>
    public double[] Get(string channelId)
    {
        return channelId switch
        {
            "TRK_FREQ" => TRK_FREQ,
            "CAB_FREQ" => CAB_FREQ,
            "CODE_FREQ" => CODE_FREQ,
            "TX_VOLTAGE" => TX_VOLTAGE,
            "RX_A_VOLTAGE" => RX_A_VOLTAGE,
            "RX_B_VOLTAGE" => RX_B_VOLTAGE,
            _ => throw new ArgumentOutOfRangeException(nameof(channelId), channelId, "알 수 없는 채널"),
        };
    }
}
