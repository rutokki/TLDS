using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TLDSDashBoard.Services;

public sealed record EventLogRow(DateTime Time, int DeviceType, string DeviceName, string Content);

public sealed record AlarmLogRow(DateTime GeneratedAt, DateTime? RestoredAt, string DeviceName, string Content, int Major, int Minor);

/// <summary>
/// 실제 이벤트/알람 로그 TLDSDashBoard/_file_db/Event-{yyyy-MM-dd}.db(하루 파일 하나)를 읽는다:
///   Event (TIME, SUB_SECTION, EVENT_NUM, DEVICE_TYPE, DEVICE_NAME char20, CONTENTS char20)
///   Alarm (GENERATE_TIME, SUB_SECTION, GENERATE, EVENT_NUM, DEVICE_TYPE, MAJOR, MINOR, WC,
///          DEVICE_NAME char20, CONTENTS char20, SUB_TEXT char4, RESTORE_TIME)
///
/// DEVICE_NAME/CONTENTS는 UTF-8이 아니라 원시 CP949(한국어 코드페이지 949) 바이트로 저장되어 있다 —
/// SQLite TEXT로 그대로 읽으면(Microsoft.Data.Sqlite의 기본 동작) .NET이 TEXT 컬럼을 UTF-8로 가정하기
/// 때문에 글자가 깨진다. 그래서 두 컬럼은 BLOB으로 다시 읽어 CP949로 명시적으로 디코딩한다.
/// </summary>
public static class EventLogRepository
{
    private static readonly Encoding Cp949 = Encoding.GetEncoding(949);

    /// <summary>주어진 날짜의 Event 행을 전부 읽는다. 그 날 파일이 없으면 예외 대신 빈 리스트를 반환한다 —
    /// 여러 날에 걸친 기간 검색을 하는 호출자에게 "그 날 이벤트 없음"과 "그 날 로그 파일 없음"은
    /// 똑같이 보이기 때문이다.</summary>
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

    /// <summary>주어진 날짜의 Alarm 행을 전부 읽는다. 파일이 없으면 빈 리스트를 반환하는 동작은 <see cref="LoadEvents"/>와 같다.</summary>
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
