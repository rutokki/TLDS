namespace TLDSDashBoard.Models;

/// <summary>A station — the top-level filter dimension for track circuits below it. Read from system.xml's &lt;station_no&gt;&lt;match number name/&gt; (Id = number) — see Services/StationConfigLoader.cs.</summary>
public sealed class Station
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
