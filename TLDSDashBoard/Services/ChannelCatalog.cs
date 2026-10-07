namespace TLDSDashBoard.Services;

/// <summary>
/// Display metadata for the 6 telemetry channels (id/name/unit/color/group), in _file_log's
/// Value0..Value5 order (주파수 3 → 전압 3). Single source of truth for every page that lists the channels
/// — the index of a channel here is also its index into the SetValue.db threshold list (Base_N/Caution_N/Warning_N).
/// Holds no data — every value shown in the app comes from the real _file_log/_file_db files.
/// </summary>
public static class ChannelCatalog
{
    public const string FrequencyGroup = "주파수";
    public const string VoltageGroup = "전압";

    private static readonly (string Id, string Name, string Unit, string ColorHex, string Group)[] Specs =
    {
        ("TRK_FREQ",     "궤도 주파수", "Hz", "#4C9AFF", FrequencyGroup),
        ("CAB_FREQ",     "차상 주파수", "Hz", "#FF9F40", FrequencyGroup),
        ("CODE_FREQ",    "지상 주파수", "Hz", "#36D399", FrequencyGroup),
        ("TX_VOLTAGE",   "송신 전압",   "V",  "#FF6B6B", VoltageGroup),
        ("RX_A_VOLTAGE", "수신 A 전압", "V",  "#B983FF", VoltageGroup),
        ("RX_B_VOLTAGE", "수신 B 전압", "V",  "#FFD166", VoltageGroup),
    };

    public static IReadOnlyList<string> ChannelIds { get; } = Specs.Select(s => s.Id).ToList();

    public static int ChannelCount => Specs.Length;

    public static string ChannelName(string id)
    {
        return Specs.First(s => s.Id == id).Name;
    }

    public static string ChannelUnit(string id)
    {
        return Specs.First(s => s.Id == id).Unit;
    }

    public static string ChannelColor(string id)
    {
        return Specs.First(s => s.Id == id).ColorHex;
    }

    /// <summary>"주파수" or "전압" — OverView renders one stacked-lane block per group.</summary>
    public static string ChannelGroup(string id)
    {
        return Specs.First(s => s.Id == id).Group;
    }

    /// <summary>Position of the channel in Value0..Value5 / the threshold list.</summary>
    public static int IndexOf(string id)
    {
        return Array.FindIndex(Specs, s => s.Id == id);
    }
}
