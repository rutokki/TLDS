using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Reads the 12-channel TX/RX history from a local SQLite file. The real schema was not known when
/// this was written, so every name below is a guess — a single "wide" table with one row per
/// timestamp and one column per channel (the most common shape for this kind of logger). Update the
/// four constants/ColumnMap below to match the actual database; nothing else needs to change.
///
/// If the file/table/columns don't match (or the file doesn't exist yet), <see cref="LoadRecent"/>
/// throws and <see cref="ViewModels.MainViewModel"/> falls back to generated sample data so the UI
/// still has something to show — watch the topbar's data-source label to see which is active.
/// </summary>
public sealed class SqliteChannelDataRepository : IChannelDataRepository
{
    // ---- TODO: adjust these to match the real database ----
    private const string DbPath = @"data\telemetry.db"; // relative to the app's working directory; use an absolute path if the DB lives elsewhere
    private const string TableName = "Readings";
    private const string TimestampColumn = "Timestamp";

    // TODO: left = channel id used throughout the app, right = actual column name in the DB (only the right side should need editing).
    private static readonly (string Id, string Column)[] ColumnMap =
    {
        ("TAC", "TAC"), ("TV", "TV"), ("TFP", "TFP"), ("TSP", "TSP"), ("THZ", "THZ"), ("TA", "TA"),
        ("RFP", "RFP"), ("RSP", "RSP"), ("RHZ", "RHZ"), ("RA", "RA"), ("RV1", "RV1"), ("RV2", "RV2"),
    };
    // ---------------------------------------------------------

    public ChannelDataSet LoadRecent(int maxSamples)
    {
        var connStr = new SqliteConnectionStringBuilder { DataSource = DbPath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        string cols = string.Join(", ", ColumnMap.Select(c => $"[{c.Column}]"));
        string sql = $"SELECT [{TimestampColumn}], {cols} FROM [{TableName}] ORDER BY [{TimestampColumn}] DESC LIMIT $n";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$n", maxSamples);

        var timestamps = new List<DateTime>();
        var rows = new List<double[]>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                timestamps.Add(ParseTimestamp(reader.GetValue(0)));
                var vals = new double[ColumnMap.Length];
                for (int i = 0; i < ColumnMap.Length; i++)
                {
                    vals[i] = reader.IsDBNull(i + 1) ? 0.0 : Convert.ToDouble(reader.GetValue(i + 1), CultureInfo.InvariantCulture);
                }
                rows.Add(vals);
            }
        }

        if (timestamps.Count == 0)
        {
            throw new InvalidOperationException($"'{TableName}' 테이블에서 읽어온 행이 없습니다 (DB 경로/테이블/컬럼명을 확인하세요).");
        }

        // DB read back newest-first (for LIMIT); charts want oldest-first.
        timestamps.Reverse();
        rows.Reverse();

        double[] Col(int idx) => rows.Select(r => r[idx]).ToArray();

        return new ChannelDataSet
        {
            Timestamps = timestamps.ToArray(),
            TAC = Col(0), TV = Col(1), TFP = Col(2), TSP = Col(3), THZ = Col(4), TA = Col(5),
            RFP = Col(6), RSP = Col(7), RHZ = Col(8), RA = Col(9), RV1 = Col(10), RV2 = Col(11),
        };
    }

    private static DateTime ParseTimestamp(object raw)
    {
        switch (raw)
        {
            case string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt):
                return dt;
            case long unixSeconds:
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
            case double unixSecondsD:
                return DateTimeOffset.FromUnixTimeSeconds((long)unixSecondsD).LocalDateTime;
            default:
                return DateTime.TryParse(raw?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2) ? dt2 : DateTime.Now;
        }
    }
}
