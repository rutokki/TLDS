using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Placeholder station/track-circuit reference data for the filter dropdowns on the new list
/// screens (열차점유/궤도장애·경보/레벨기록/시간별상태출력). The real app has no station/track
/// dimension yet — swap this for a real lookup once the station/track schema is known.
/// </summary>
public static class ReferenceData
{
    public static readonly IReadOnlyList<Station> Stations = new[]
    {
        new Station { Id = "SHC", Name = "시흥차량기지" },
    };

    public static readonly IReadOnlyList<TrackCircuit> Tracks = new[]
    {
        new TrackCircuit { Id = "010T", Name = "010T", StationId = "SHC" },
        new TrackCircuit { Id = "020T", Name = "020T", StationId = "SHC" },
        new TrackCircuit { Id = "030T", Name = "030T", StationId = "SHC" },
        new TrackCircuit { Id = "040T", Name = "040T", StationId = "SHC" },
    };

    public static IEnumerable<TrackCircuit> TracksForStation(string stationId) =>
        Tracks.Where(t => t.StationId == stationId);
}
