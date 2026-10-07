using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Station/track-circuit reference data for every filter dropdown (OverView, 그래프 상세검색, 열차점유,
/// 궤도장애·경보, 레벨기록, 시간별상태출력). Loaded once, on first use, from the TLDS install's own
/// _file_system config — system.xml picks the station(s), then index_{역}.xml / rack_{역}.xml supply the
/// tracks and their rack wiring (see <see cref="StationConfigLoader"/>). Nothing is hardcoded, so the same
/// exe works at any station as long as it sits in that station's TLDS folder.
///
/// App.OnStartup loads this (after applying any --data-root override) before any window is created, and
/// exits with the error shown if the config can't be read — so every page can assume at least one
/// station and one track exist.
/// </summary>
public static class ReferenceData
{
    private static readonly Lazy<StationConfig> Config = new(StationConfigLoader.Load);

    public static StationConfig Current => Config.Value;

    public static IReadOnlyList<Station> Stations => Config.Value.Stations;

    public static IReadOnlyList<TrackCircuit> Tracks => Config.Value.Tracks;

    /// <summary>LEU 입력카드 이름 — index_{역}.xml의 &lt;input&gt; DICARD 항목. "LEU고장" 카테고리(시간별 상태
    /// 출력)에서 참조할 장치 목록으로 쓸 수 있음(아직 미배선).</summary>
    public static IReadOnlyList<string> LeuDevices => Config.Value.LeuDevices;

    public static IEnumerable<TrackCircuit> TracksForStation(string stationId) =>
        Tracks.Where(t => t.StationId == stationId);
}
