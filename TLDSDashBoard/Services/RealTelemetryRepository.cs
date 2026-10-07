using Microsoft.Data.Sqlite;
using System.IO;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Reads real track-circuit telemetry from the per-hour log files at TLDSDashBoard/_file_log/{yyyy}/{MM}/
/// {yyyy-MM-dd-HH}.db (table Level12 — Level01 in older files: Time INT, Idx WORD, Type WORD, Value0..Value11 WORD).
/// Idx is the device index (matches TrackCircuit.Idx / ReferenceData.Tracks); Type distinguishes
/// measured-value rows (0, what this reads) from per-channel status/flag rows (1, not used here).
///
/// Value0..Value5 map to the 6 channels in ChannelCatalog order (궤도/차상/지상 주파수, 송신/수신A/수신B 전압);
/// Value6..Value11 are read but not used. The schema itself carries no channel labels.
/// </summary>
public sealed class RealTelemetryRepository : IChannelDataRepository
{
    public ChannelDataSet LoadRange(int deviceIdx, DateTime start, DateTime end)
    {
        var rows = new List<(DateTime Time, int[] Values)>();
        long startUnix = new DateTimeOffset(DateTime.SpecifyKind(start, DateTimeKind.Local)).ToUnixTimeSeconds();
        long endUnix = new DateTimeOffset(DateTime.SpecifyKind(end, DateTimeKind.Local)).ToUnixTimeSeconds();

        var hourCursor = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
        while (hourCursor <= end)
        {
            string path = TldsDataPaths.HourLogFile(hourCursor);
            if (File.Exists(path))
            {
                rows.AddRange(ReadHourFileRange(path, deviceIdx, startUnix, endUnix));
            }
            hourCursor = hourCursor.AddHours(1);
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"장치 Idx={deviceIdx}의 {start:yyyy-MM-dd HH:mm}~{end:yyyy-MM-dd HH:mm} 구간 실측 데이터를 _file_log에서 찾지 못했습니다.");
        }

        var timestamps = rows.Select(r => r.Time).ToArray();
        double[] Col(int i)
        {
            return rows.Select(r => (double)r.Values[i]).ToArray();
        }

        return new ChannelDataSet
        {
            Timestamps = timestamps,
            TRK_FREQ = Col(0),
            CAB_FREQ = Col(1),
            CODE_FREQ = Col(2),
            TX_VOLTAGE = Col(3),
            RX_A_VOLTAGE = Col(4),
            RX_B_VOLTAGE = Col(5)

        };
    }

    private static List<(DateTime Time, int[] Values)> ReadHourFileRange(string path, int deviceIdx, long startUnix, long endUnix)
    {
        var result = new List<(DateTime, int[])>();
        var connStr = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        string? table = FindLevelTable(conn);
        if (table is null) return result; // no Level table in this hour file — treat as no data

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT Time, Value0, Value1, Value2, Value3, Value4, Value5, Value6, Value7, Value8, Value9, Value10, Value11
            FROM [{table}]
            WHERE Idx = $idx AND Type = 0 AND Time >= $start AND Time <= $end
            ORDER BY Time ASC";
        cmd.Parameters.AddWithValue("$idx", deviceIdx);
        cmd.Parameters.AddWithValue("$start", startUnix);
        cmd.Parameters.AddWithValue("$end", endUnix);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)).LocalDateTime;
            var values = new int[12];
            for (int i = 0; i < 12; i++) values[i] = reader.GetInt32(i + 1);
            result.Add((time, values));
        }
        return result;
    }

    /// <summary>Current TLDS writes the measured-value table as "Level12"; hour files from older installs
    /// use "Level01". Prefer Level12, fall back to Level01, so both generations of log files are read.
    /// (SetValue.db's threshold table is still Level01 — see ThresholdRepository — and is unaffected.)</summary>
    private static readonly string[] LevelTableNames = { "Level12", "Level01" };

    private static string? FindLevelTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) tables.Add(reader.GetString(0));
        return LevelTableNames.FirstOrDefault(tables.Contains);
    }
}
