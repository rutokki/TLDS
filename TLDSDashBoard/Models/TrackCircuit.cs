namespace TLDSDashBoard.Models;

/// <summary>A track circuit (e.g. "010T") belonging to a <see cref="Station"/>. Placeholder reference data (see Services/ReferenceData.cs) until the real track table is known.</summary>
public sealed class TrackCircuit
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string StationId { get; init; }
}
