using System.IO;
namespace TLDSDashBoard.Services;

/// <summary>
/// Locates the real TLDS data files the field TLDS program (TLDS.exe) writes. The dashboard is deployed
/// INTO the TLDS install folder (next to TLDS.exe), so by default it reads the _file_* folders beside its
/// own exe — the same files TLDS.exe is writing live.
///
/// For development (running from bin\, where there is no live TLDS install) the root can be pointed at a
/// real install instead, via the command line <c>--data-root "C:\path\to\tlds"</c> or the environment
/// variable <c>TLDS_DATA_ROOT</c> (see App.OnStartup).
///
/// Layout (matching a real 쌍룡역 TLDS install):
///   _file_log/{yyyy}/{MM}/{yyyy-MM-dd-HH}.db   — one file per HOUR, table Level12 (older: Level01)(Time,Idx,Type,Value0..11)
///                                                  = the actual measured telemetry (Type=0 rows).
///   _file_db/SetValue.db                        — table Level01(Time,ObjectIndex,Type,Base_N/Caution_N/Warning_N×12)
///                                                  = fixed threshold settings, one row per device.
///   _file_db/Event-{yyyy-MM-dd}.db              — one file per DAY, tables Event(...) and Alarm(...).
///   _file_system/system.xml, index_{역}.xml, rack_{역}.xml — station/track/rack config (StationConfigLoader).
///   _file_pics/                                 — track-diagram vector file (not yet used).
/// </summary>
public static class TldsDataPaths
{
    public const string RootEnvVar = "TLDS_DATA_ROOT";

    private static string? _rootOverride;

    /// <summary>Folder holding _file_log/_file_db — the exe folder unless overridden (see class summary).</summary>
    public static string Root => _rootOverride ?? AppContext.BaseDirectory;

    /// <summary>Points the data root at another folder (a real TLDS install). Ignored when null/blank.</summary>
    public static void SetRoot(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path)) _rootOverride = Path.GetFullPath(path.Trim().Trim('"'));
    }

    public static string FileLogRoot => Path.Combine(Root, "_file_log");
    public static string FileDbRoot => Path.Combine(Root, "_file_db");
    public static string FileSystemRoot => Path.Combine(Root, "_file_system");

    public static string HourLogFile(DateTime hour) =>
        Path.Combine(FileLogRoot, hour.ToString("yyyy"), hour.ToString("MM"), hour.ToString("yyyy-MM-dd-HH") + ".db");

    public static string EventDbFile(DateTime day) =>
        Path.Combine(FileDbRoot, $"Event-{day:yyyy-MM-dd}.db");

    public static string SetValueDbFile => Path.Combine(FileDbRoot, "SetValue.db");
}
