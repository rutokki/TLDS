namespace TLDSDashBoard.Models;

/// <summary>역 — 그 아래 궤도회로들의 최상위 필터 차원. system.xml의 &lt;station_no&gt;&lt;match number name/&gt;에서 읽는다(Id = number) — Services/StationConfigLoader.cs 참고.</summary>
public sealed class Station
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
