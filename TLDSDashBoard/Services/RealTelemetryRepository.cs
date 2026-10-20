using Microsoft.Data.Sqlite;
using System.IO;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// TLDSDashBoard/_file_log/{yyyy}/{MM}/{yyyy-MM-dd-HH}.db의 시간별 로그 파일에서 실제 궤도회로
/// 텔레메트리를 읽는다(테이블 Level12 — 구버전 파일은 Level01: Time INT, Idx WORD, Type WORD, Value0..Value11 WORD).
/// Idx는 장치 인덱스(TrackCircuit.Idx / ReferenceData.Tracks와 일치)이고, Type은 측정값 행(0, 여기서 읽는 것)과
/// 채널별 상태/플래그 행(1, 여기서는 쓰지 않음)을 구분한다.
///
/// Value0..Value5는 ChannelCatalog 순서의 6개 채널(궤도/차상/지상 주파수, 송신/수신A/수신B 전압)에 대응하고,
/// Value6..Value11은 읽기만 하고 쓰지 않는다. 스키마 자체에는 채널 라벨이 없다.
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
        // Value0..5는 원시 장치 정수(YK-900A 통신전문)다 — 여기서 한 번만 실제 단위로 변환해, 모든 소비자
        // (차트, 표, CSV)가 Hz/V를 바로 보게 한다. 채널별 Scale은 ChannelCatalog에 있다.
        double[] Col(int i)
        {
            double scale = ChannelCatalog.ChannelScaleAt(i);
            int decimals = ChannelCatalog.ChannelDecimals(ChannelCatalog.ChannelIds[i]);
            // 규격 해상도로 반올림해서, 예컨대 70 × 0.01이 0.7000000000000001이 아니라 0.7이 되게 한다(CSV에서 중요).
            return rows.Select(r => Math.Round(r.Values[i] * scale, decimals)).ToArray();
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
        if (table is null) return result; // 이 시간 파일에 Level 테이블이 없음 — 데이터 없음으로 처리

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

    /// <summary>현재 TLDS는 측정값 테이블을 "Level12"로 쓰고, 구버전 설치의 시간 파일은 "Level01"을 쓴다.
    /// Level12를 우선하고 Level01로 폴백해서 두 세대의 로그 파일을 모두 읽는다.
    /// (SetValue.db의 임계값 테이블은 여전히 Level01이고 — ThresholdRepository 참고 — 영향받지 않는다.)</summary>
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
