using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// 모든 필터 드롭다운(OverView, 그래프 상세검색, 열차점유, 궤도장애·경보, 레벨기록, 시간별상태출력)이 쓰는
/// 역/궤도회로 기준 데이터. 처음 쓸 때 한 번만 TLDS 설치의 _file_system 설정에서 읽는다 — system.xml이
/// 역을 정하고, index_{역}.xml / rack_{역}.xml이 궤도와 랙 배선을 채운다(<see cref="StationConfigLoader"/> 참고).
/// 하드코딩된 것이 없으므로, 그 역의 TLDS 폴더에 놓기만 하면 같은 exe가 어느 역에서나 동작한다.
///
/// App.OnStartup이 (--data-root 재정의를 적용한 뒤) 어떤 창이 만들어지기 전에 이것을 로드하고, 설정을
/// 읽을 수 없으면 오류를 보여주고 종료한다 — 그래서 모든 페이지가 역과 궤도가 최소 하나씩 있다고
/// 가정할 수 있다.
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

    /// <summary>신호기 이름 — index_{역}.xml의 &lt;signal&gt; 항목 (시간별 상태 출력 신호설비 이벤트 장치 선택용).</summary>
    public static IReadOnlyList<string> Signals => Config.Value.Signals;

    /// <summary>선로전환기 이름 — index_{역}.xml의 &lt;point&gt; 항목.</summary>
    public static IReadOnlyList<string> Switches => Config.Value.Switches;

    public static IEnumerable<TrackCircuit> TracksForStation(string stationId) =>
        Tracks.Where(t => t.StationId == stationId);
}
