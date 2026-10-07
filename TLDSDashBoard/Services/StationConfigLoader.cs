using System.IO;
using System.Text;
using System.Xml.Linq;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>Everything read from _file_system for the configured station(s).</summary>
public sealed record StationConfig(
    IReadOnlyList<Station> Stations,
    IReadOnlyList<TrackCircuit> Tracks,
    IReadOnlyList<string> LeuDevices,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Reads the TLDS install's own station configuration the same way TLDS.exe does:
///   1. _file_system/system.xml — &lt;section&gt;&lt;station_no&gt;&lt;match number=".." name=".."/&gt; lists the
///      station(s) this install serves (falls back to &lt;menu&gt;&lt;section name=".."/&gt; if absent).
///   2. _file_system/index_{역이름}.xml — &lt;track&gt;&lt;contents index name type level_type/&gt; = the 궤도회로
///      list; `index` is the DB device number (_file_log Idx / SetValue.db ObjectIndex). &lt;input&gt;
///      DICARD entries = LEU 입력카드.
///   3. _file_system/rack_{역이름}.xml — &lt;Rack&gt;&lt;sub type="impulse" name rack_no&gt;&lt;card slot&gt;&lt;track name/&gt;
///      = which impulse rack/slot each track is wired to (optional; tracks without a rack entry just
///      have no rack info).
/// So whichever station system.xml names is what the dashboard shows — other index_*.xml files lying in
/// the folder are ignored, exactly like TLDS.exe.
///
/// The files carry no encoding declaration and are saved as CP949 (Korean ANSI), not UTF-8 — they are
/// read as bytes and decoded explicitly (UTF-8 only when a BOM or strictly valid UTF-8 says so).
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

            var stationTrackNames = tracks.Skip(before).Select(t => t.Name).ToHashSet();
            var rackOnly = racks.Keys.Where(n => !stationTrackNames.Contains(n)).ToList();
            if (rackOnly.Count > 0)
                warnings.Add($"rack_{station.Name}.xml에만 있는 궤도(무시됨): {string.Join(", ", rackOnly)}");
        }

        if (tracks.Count == 0)
            throw new InvalidDataException(
                $"궤도 정보를 읽지 못했습니다 ({string.Join(" / ", warnings)}). 경로: {TldsDataPaths.FileSystemRoot}");

        return new StationConfig(stations, tracks.OrderBy(t => t.StationId).ThenBy(t => t.Idx).ToList(), leus, warnings);
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
        // Field config files contain comments that aren't well-formed XML (e.g. rack_쌍룡.xml's
        // <!--sub type="di" sub_type="---" ...--> — "--" inside a comment), which TLDS.exe's own parser
        // tolerates but XDocument rejects. Comments carry nothing we read, so strip them before parsing.
        text = System.Text.RegularExpressions.Regex.Replace(text, "<!--.*?-->", "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        try { return XDocument.Parse(text); }
        catch (Exception ex) { throw new InvalidDataException($"{Path.GetFileName(path)} 파싱 실패: {ex.Message}", ex); }
    }
}
