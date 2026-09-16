namespace TLDSDashBoard.ViewModels.Items;

public sealed class MetricCardVM
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string Value { get; init; }
    public required string ColorHex { get; init; }
    public required string SparkPath { get; init; }
}
