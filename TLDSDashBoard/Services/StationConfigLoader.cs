using System.IO;
using System.Text;
using System.Xml.Linq;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>설정된 역(들)에 대해 _file_system에서 읽은 모든 것.</summary>
public sealed record StationConfig(
    IReadOnlyList<Station> Stations,
    IReadOnlyList<TrackCircuit> Tracks,
    IReadOnlyList<string> LeuDevices,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Signals,
    IReadOnlyList<string> Switches);

/// <summary>
/// TLDS.exe와 같은 방식으로 TLDS 설치 자체의 역 설정을 읽는다:
///   1. _file_system/system.xml — &lt;section&gt;&lt;station_no&gt;&lt;match number=".." name=".."/&gt;가 이 설치가
///      담당하는 역(들)을 나열한다(없으면 &lt;menu&gt;&lt;section name=".."/&gt;로 폴백).
///   2. _file_system/index_{역이름}.xml — &lt;track&gt;&lt;contents index name type level_type/&gt; = 궤도회로
///      목록; `index`는 DB 장치 번호(_file_log Idx / SetValue.db ObjectIndex)다. &lt;input&gt;
///      DICARD 항목 = LEU 입력카드.
///   3. _file_system/rack_{역이름}.xml — &lt;Rack&gt;&lt;sub type="impulse" name rack_no&gt;&lt;card slot&gt;&lt;track name/&gt;
///      = 각 궤도가 어느 임펄스 랙/슬롯에 배선되어 있는지(선택 사항; 랙 항목이 없는 궤도는 랙 정보만
///      없을 뿐이다).
/// 따라서 system.xml이 지정한 역이 대시보드에 표시되는 역이다 — 폴더에 있는 다른 index_*.xml 파일은
/// TLDS.exe와 똑같이 무시된다.
///
/// 파일에는 인코딩 선언이 없고 UTF-8이 아니라 CP949(한국어 ANSI)로 저장되어 있다 — 바이트로 읽어
/// 명시적으로 디코딩한다(BOM이 있거나 엄격히 유효한 UTF-8일 때만 UTF-8로 본다).
/// </summary>
public static class StationConfigLoader
{
    public static StationConfig Load()
    {
        var warnings = new List<string>();
        string systemPath = Path.Combine(TldsDataPaths.FileSystemRoot, "system.xml");
        if (!File.Exists(systemPath))
            throw new FileNotFoundException($"system.xml을 찾을 수 없습니다: {systemPath}");

        var system = LoadXml(systemPath);
        var stations = system.Descendants("station_no").Elements("match")
            .Select(m => new Station { Id = Attr(m, "number"), Name = Attr(m, "name") })
            .Where(s => s.Name.Length > 0)
            .ToList();
        if (stations.Count == 0)
        {
            // Older configs: only the 화면 선택 menu names the station.
            stations = system.Descendants("menu").Elements("section")
                .Select((s, i) => new Station { Id = (i + 1).ToString(), Name = Attr(s, "name") })
                .Where(s => s.Name.Length > 0)
                .ToList();
        }
        if (stations.Count == 0)
            throw new InvalidDataException($"system.xml에 역 정보(<station_no><match>)가 없습니다: {systemPath}");

        var tracks = new List<TrackCircuit>();
        var leus = new List<string>();
        var signals = new List<string>();
        var switches = new List<string>();
        foreach (var station in stations)
        {
            string indexPath = Path.Combine(TldsDataPaths.FileSystemRoot, $"index_{station.Name}.xml");
            if (!File.Exists(indexPath))
            {
                warnings.Add($"index_{station.Name}.xml 없음");
                continue;
            }
            var index = LoadXml(indexPath);
            var racks = LoadRackMap(station.Name, warnings);
            int before = tracks.Count;

            foreach (var c in index.Root!.Elements("track").Elements("contents"))
            {
                if (!int.TryParse(Attr(c, "index"), out int idx)) continue;
                string name = Attr(c, "name");
                racks.TryGetValue(name, out var rack);
                tracks.Add(new TrackCircuit
                {
                    Id = $"{station.Id}:{name}",
                    Name = name,
                    StationId = station.Id,
                    Idx = idx,
                    LevelType = Attr(c, "level_type"),
                    TrackType = Attr(c, "type"),
                    RackName = rack?.RackName,
                    RackNo = rack?.RackNo,
                    RackSlot = rack?.Slot,
                });
            }

            leus.AddRange(index.Root!.Elements("input").Elements("contents")
                .Where(c => Attr(c, "type") == "DICARD")
                .Select(c => Attr(c, "name")));

            // 신호기 = <signal><contents name>, 선로전환기 = <point><contents name> (names only — no DB index
            // is needed, they're just labels for the 시간별 상태 출력 신호설비 이벤트 device pickers).
            signals.AddRange(index.Root!.Elements("signal").Elements("contents").Select(c => Attr(c, "name")).Where(n => n.Length > 0));
            switches.AddRange(index.Root!.Elements("point").Elements("contents").Select(c => Attr(c, "name")).Where(n => n.Length > 0));

            var stationTrackNames = tracks.Skip(before).Select(t => t.Name).ToHashSet();
            var rackOnly = racks.Keys.Where(n => !stationTrackNames.Contains(n)).ToList();
            if (rackOnly.Count > 0)
                warnings.Add($"rack_{station.Name}.xml에만 있는 궤도(무시됨): {string.Join(", ", rackOnly)}");
        }

        if (tracks.Count == 0)
            throw new InvalidDataException(
                $"궤도 정보를 읽지 못했습니다 ({string.Join(" / ", warnings)}). 경로: {TldsDataPaths.FileSystemRoot}");

        return new StationConfig(stations, tracks.OrderBy(t => t.StationId).ThenBy(t => t.Idx).ToList(), leus, warnings,
            signals.Distinct().ToList(), switches.Distinct().ToList());
    }

    private sealed record RackSlot(string RackName, int? RackNo, int? Slot);

    /// <summary>track name → rack/slot from rack_{역}.xml (impulse racks only); empty when the file is absent.</summary>
    private static Dictionary<string, RackSlot> LoadRackMap(string stationName, List<string> warnings)
    {
        var map = new Dictionary<string, RackSlot>();
        string rackPath = Path.Combine(TldsDataPaths.FileSystemRoot, $"rack_{stationName}.xml");
        if (!File.Exists(rackPath))
        {
            warnings.Add($"rack_{stationName}.xml 없음");
            return map;
        }
        foreach (var sub in LoadXml(rackPath).Descendants("sub").Where(s => Attr(s, "type") == "impulse"))
        {
            string rackName = Attr(sub, "name");
            int? rackNo = int.TryParse(Attr(sub, "rack_no"), out int rn) ? rn : null;
            foreach (var card in sub.Elements("card"))
            {
                int? slot = int.TryParse(Attr(card, "slot"), out int sl) ? sl : null;
                foreach (var t in card.Elements("track"))
                    map[Attr(t, "name")] = new RackSlot(rackName, rackNo, slot);
            }
        }
        return map;
    }

    private static string Attr(XElement e, string name) => (string?)e.Attribute(name) is { } v ? v.Trim() : "";

    private static XDocument LoadXml(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string text;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        else
        {
            try { text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
            catch (DecoderFallbackException) { text = Encoding.GetEncoding(949).GetString(bytes); }
        }
        // 현장 설정 파일에는 올바른 XML이 아닌 주석이 있다(예: rack_쌍룡.xml의
        // <!--sub type="di" sub_type="---" ...--> — 주석 안에 "--"). TLDS.exe 자체 파서는 이를 허용하지만
        // XDocument는 거부한다. 주석에는 우리가 읽는 내용이 없으므로, 파싱 전에 제거한다.
        text = System.Text.RegularExpressions.Regex.Replace(text, "<!--.*?-->", "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        try { return XDocument.Parse(text); }
        catch (Exception ex) { throw new InvalidDataException($"{Path.GetFileName(path)} 파싱 실패: {ex.Message}", ex); }
    }
}
