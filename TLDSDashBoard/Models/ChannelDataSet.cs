namespace TLDSDashBoard.Models;

/// <summary>
/// 6개 텔레메트리 계열(주파수 3 + 전압 3). <see cref="Timestamps"/> 항목 하나당 샘플 하나(DB 기반:
/// 초당 1행, 모든 채널을 함께 샘플링 — Services/RealTelemetryRepository 참고).
/// </summary>
public sealed class ChannelDataSet
{
    /// <summary>아래 모든 채널 배열의 인덱스 i에 해당하는 샘플 시각(각 배열과 길이가 같음). 샘플링 간격은 1초.</summary>
    public required DateTime[] Timestamps { get; init; }

    public required double[] TRK_FREQ { get; init; } // 궤도 주파수 (Value0)
    public required double[] CAB_FREQ { get; init; } // 차상 주파수 (Value1)
    public required double[] CODE_FREQ { get; init; } // 지상 주파수 (Value2)
    public required double[] TX_VOLTAGE { get; init; } // 송신 전압 (Value3)
    public required double[] RX_A_VOLTAGE { get; init; } // 수신 A 전압 (Value4)
    public required double[] RX_B_VOLTAGE { get; init; } // 수신 B 전압 (Value5)

    /// <summary>채널 id(Services/ChannelCatalog의 id)로 찾는 계열 — 각 프로퍼티 이름을 일일이 쓰지 않고
    /// 페이지가 카탈로그를 순회할 수 있게 한다.</summary>
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
