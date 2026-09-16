namespace TLDSDashBoard.Models;

/// <summary>
/// Static definition of one displayed channel/metric series (id, label, unit, color, and its full
/// sample array).
/// </summary>
public sealed class MetricDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }
    public required double[] Data { get; init; }

    /// <summary>Number of decimal places used when formatting this metric's value — frequencies get one extra digit.</summary>
    public int Decimals => Unit == "Hz" ? 2 : 1;
}
