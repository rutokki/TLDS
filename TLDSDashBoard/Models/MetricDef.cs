namespace TLDSDashBoard.Models;

/// <summary>
/// 표시되는 채널/지표 계열 하나의 정적 정의(id, 라벨, 단위, 색, 전체 샘플 배열).
/// </summary>
public sealed class MetricDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }
    public required double[] Data { get; init; }

    /// <summary>이 지표 값을 표시할 때 쓰는 소수 자릿수 — 채널별로 ChannelCatalog에서 가져온다
    /// (YK-900A 규격 해상도: 궤도/차상 주파수 0, 지상 주파수 2, 송신 전압 1, 수신 전압 2).</summary>
    public int Decimals => TLDSDashBoard.Services.ChannelCatalog.ChannelDecimals(Id);

    /// <summary>"F{Decimals}" — ToString에 바로 넘길 수 있는 형식 문자열.</summary>
    public string Format => "F" + Decimals;
}
