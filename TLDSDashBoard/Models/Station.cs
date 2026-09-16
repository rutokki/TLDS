namespace TLDSDashBoard.Models;

/// <summary>A station/depot — the top-level filter dimension for track circuits below it. Placeholder reference data (see Services/ReferenceData.cs) until the real station table is known.</summary>
public sealed class Station
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
