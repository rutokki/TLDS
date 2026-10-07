using System.IO;
using Microsoft.Data.Sqlite;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>One channel's fixed threshold settings — Base(기준)/Caution(주의)/Warning(경고). User-set,
/// not measured; comes from SetValue.db, distinct from the per-second measured values in _file_log.</summary>
public sealed record ChannelThreshold(double Base, double Caution, double Warning);

/// <summary>
/// Reads per-device channel thresholds from TLDSDashBoard/_file_db/SetValue.db (table Level01:
/// Time, ObjectIndex, Type, then (Base_N, Caution_N, Warning_N) for N=0..11 — one row per device,
/// ObjectIndex matching TrackCircuit.Idx). These are fixed settings the user configured, separate from
/// the actual measured values RealTelemetryRepository reads from _file_log.
///
/// Mapped onto the app's existing 표준/측정/점검/최소 (Standard/Measured/Checked/Minimum) 4-value
/// ChannelMeasurement shape as: Standard=Base, Checked=Caution, Minimum=Warning (Measured always comes
/// from elsewhere — the live telemetry, not this table). This keeps every existing 궤도장애/레벨기록
/// column header and layout unchanged rather than renaming everything to Base/Caution/Warning.
/// </summary>
public static class ThresholdRepository
{
    /// <summary>Returns this device's thresholds for the ChannelCatalog channels (same order as Value0..Value5
    /// in _file_log; SetValue.db has Base/Caution/Warning_0..11 but only the first ChannelCount are used), or null if SetValue.db doesn't exist or has no row for this device.</summary>
    public static IReadOnlyList<ChannelThreshold>? LoadForDevice(int objectIndex)
    {
        string path = TldsDataPaths.SetValueDbFile;
        if (!File.Exists(path)) return null;

        var connStr = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Level01 WHERE ObjectIndex = $idx ORDER BY Time DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$idx", objectIndex);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var result = new List<ChannelThreshold>(ChannelCatalog.ChannelCount);
        for (int i = 0; i < ChannelCatalog.ChannelCount; i++)
        {
            double baseVal = Convert.ToDouble(reader[$"Base_{i}"]);
            double caution = Convert.ToDouble(reader[$"Caution_{i}"]);
            double warning = Convert.ToDouble(reader[$"Warning_{i}"]);
            result.Add(new ChannelThreshold(baseVal, caution, warning));
        }
        return result;
    }

    /// <summary>Builds the existing 표준/측정/점검/최소 ChannelMeasurement shape from a real threshold —
    /// Standard/Checked/Minimum come straight from the setting, Measured is supplied by the caller (the
    /// real telemetry sample for that moment, or null when _file_log has none — shown blank, never guessed).</summary>
    public static ChannelMeasurement ToMeasurement(ChannelThreshold t, double? measured) => new()
    {
        Standard = t.Base,
        Measured = measured,
        Checked = t.Caution,
        Minimum = t.Warning,
    };
}
