using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TLDSDashBoard.Services;

public sealed record EventLogRow(DateTime Time, int DeviceType, string DeviceName, string Content);

public sealed record AlarmLogRow(DateTime GeneratedAt, DateTime? RestoredAt, string DeviceName, string Content, int Major, int Minor);

/// <summary>
/// Reads the real event/alarm log at TLDSDashBoard/_file_db/Event-{yyyy-MM-dd}.db (one file per day):
///   Event (TIME, SUB_SECTION, EVENT_NUM, DEVICE_TYPE, DEVICE_NAME char20, CONTENTS char20)
///   Alarm (GENERATE_TIME, SUB_SECTION, GENERATE, EVENT_NUM, DEVICE_TYPE, MAJOR, MINOR, WC,
///          DEVICE_NAME char20, CONTENTS char20, SUB_TEXT char4, RESTORE_TIME)
///
/// DEVICE_NAME/CONTENTS are stored as raw CP949 (Korean codepage 949) bytes, not UTF-8 — reading them
/// as SQLite TEXT directly (as Microsoft.Data.Sqlite would by default) mangles them, since .NET assumes
/// UTF-8 for TEXT columns. Both are re-read as BLOB and decoded with CP949 explicitly instead.
/// </summary>
public static class EventLogRepository
{
    private static readonly Encoding Cp949 = Encoding.GetEncoding(949);

    /// <summary>Loads every Event row on the given day. Returns an empty list (not an exception) if that
    /// day's file doesn't exist, since "no events that day" and "no log file for that day" look the same
    /// to a caller doing a date-range scan across several days.</summary>
    public static List<EventLogRow> LoadEvents(DateTime day)
    {
        string path = TldsDataPaths.EventDbFile(day);
        if (!File.Exists(path)) return new List<EventLogRow>();

        var connStr = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        var rows = new List<EventLogRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TIME, DEVICE_TYPE, CAST(DEVICE_NAME AS BLOB), CAST(CONTENTS AS BLOB) FROM Event ORDER BY TIME";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)).LocalDateTime;
            int deviceType = reader.GetInt32(1);
            string deviceName = DecodeCp949((byte[])reader.GetValue(2));
            string content = DecodeCp949((byte[])reader.GetValue(3));
            rows.Add(new EventLogRow(time, deviceType, deviceName, content));
        }
        return rows;
    }

    /// <summary>Loads every Alarm row on the given day. Same empty-list-on-missing-file behavior as <see cref="LoadEvents"/>.</summary>
    public static List<AlarmLogRow> LoadAlarms(DateTime day)
    {
        string path = TldsDataPaths.EventDbFile(day);
        if (!File.Exists(path)) return new List<AlarmLogRow>();

        var connStr = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        var rows = new List<AlarmLogRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT GENERATE_TIME, RESTORE_TIME, CAST(DEVICE_NAME AS BLOB), CAST(CONTENTS AS BLOB), MAJOR, MINOR
            FROM Alarm ORDER BY GENERATE_TIME";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var generatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)).LocalDateTime;
            long restoreRaw = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            DateTime? restoredAt = restoreRaw > 0 ? DateTimeOffset.FromUnixTimeSeconds(restoreRaw).LocalDateTime : null;
            string deviceName = DecodeCp949((byte[])reader.GetValue(2));
            string content = DecodeCp949((byte[])reader.GetValue(3));
            int major = reader.GetInt32(4);
            int minor = reader.GetInt32(5);
            rows.Add(new AlarmLogRow(generatedAt, restoredAt, deviceName, content, major, minor));
        }
        return rows;
    }

    private static string DecodeCp949(byte[] bytes)
    {
        int len = Array.IndexOf(bytes, (byte)0);
        if (len < 0) len = bytes.Length;
        return Cp949.GetString(bytes, 0, len).Trim();
    }
}
