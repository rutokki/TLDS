namespace TLDSDashBoard.ViewModels.Items;

public sealed class KpiItemVM
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }
    public required string SparkPath { get; init; }
    public required string DeltaLabel { get; init; }
    public required string DeltaColorHex { get; init; }
    public bool DeltaBold { get; init; }
}
