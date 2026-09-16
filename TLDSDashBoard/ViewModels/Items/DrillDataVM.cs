namespace TLDSDashBoard.ViewModels.Items;

public sealed class DrillDataVM
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }
    public required string BigPath { get; init; }
    public required string Min { get; init; }
    public required string Max { get; init; }
    public required string Avg { get; init; }

    /// <summary>Real start~end timestamps (from the DB) of the full history shown in this drill-in.</summary>
    public required string RangeLabel { get; init; }
}
