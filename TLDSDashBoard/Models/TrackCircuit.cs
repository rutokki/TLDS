namespace TLDSDashBoard.Models;

/// <summary>A track circuit (e.g. "3T", "21AT") belonging to a <see cref="Station"/>. Read at startup from
/// the TLDS install's _file_system/index_{역}.xml (+ rack_{역}.xml for wiring) — see Services/StationConfigLoader.cs.</summary>
public sealed class TrackCircuit
{
    /// <summary>"{station number}:{name}" — unique even if two stations reuse a track name.</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string StationId { get; init; }

    /// <summary>Numeric device index — matches the real DB's `Idx` (telemetry log) / `ObjectIndex`
    /// (SetValue.db thresholds) columns, and index_{역}.xml's own `index` attribute. This is what the
    /// real repositories actually key on; Id/Name are for display only.</summary>
    public required int Idx { get; init; }

    /// <summary>index xml `level_type`: "IMP_I" (임펄스 방식) or "AF_BS" (AF/가청주파수 방식). Informational
    /// (shown in the rack hint) — every track is read through the same 6 ChannelCatalog channels.</summary>
    public required string LevelType { get; init; }

    /// <summary>index xml `type`: NT(일반)/PT(선로전환기 구간)/BT(폐색). Empty for the "전체 궤도" sentinel.</summary>
    public string TrackType { get; init; } = "";

    /// <summary>Impulse rack this track is wired to, from rack_{역}.xml — null when it isn't listed there
    /// (e.g. AF tracks, or tracks the rack file doesn't cover).</summary>
    public string? RackName { get; init; }
    public int? RackNo { get; init; }
    public int? RackSlot { get; init; }

    /// <summary>"임펄스랙3번 · 슬롯 1 · IMP_I" style one-liner for the device filter hint.</summary>
    public string RackLabel =>
        (RackName is null ? "랙 정보 없음" : RackSlot is { } s ? $"{RackName} · 슬롯 {s}" : RackName)
        + (LevelType.Length > 0 ? $" · {LevelType}" : "");
}
