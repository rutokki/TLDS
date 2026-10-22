using System.IO;
using Microsoft.Data.Sqlite;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>채널 하나의 고정 임계값 설정 — Base(기준)/Caution(주의)/Warning(경고). 사용자가 설정한 값이지
/// 측정값이 아니다. SetValue.db에서 오며, _file_log의 초당 측정값과는 별개다.</summary>
public sealed record ChannelThreshold(double Base, double Caution, double Warning);

/// <summary>저장할 항목(채널) 하나 — 점검 %·최소 %(정수)와 새 기준값(그 항목의 측정값을 원시 정수로 바꾼 값).</summary>
public readonly record struct ChannelSave(int CautionPct, int WarningPct, int BaseRaw);

/// <summary>
/// TLDSDashBoard/_file_db/SetValue.db(테이블 Level01: Time, ObjectIndex, Type, 이어서 N=0..11에 대해
/// (Base_N, Caution_N, Warning_N) — 장치당 한 행, ObjectIndex는 TrackCircuit.Idx와 일치)에서 장치별 채널
/// 임계값을 읽는다. 사용자가 설정한 고정값이며, RealTelemetryRepository가 _file_log에서 읽는 실제
/// 측정값과는 별개다.
///
/// 앱에 이미 있던 표준/측정/점검/최소(Standard/Measured/Checked/Minimum) 4값 ChannelMeasurement 형태에
/// 다음처럼 대응시킨다: Standard=Base, Checked=Caution, Minimum=Warning (Measured는 항상 다른 곳 — 이 테이블이
/// 아니라 실시간 텔레메트리 — 에서 온다). 이렇게 하면 모든 이름을 Base/Caution/Warning으로 바꾸지 않고도
/// 기존 궤도장애/레벨기록의 컬럼 헤더와 레이아웃을 그대로 유지할 수 있다.
/// </summary>
public static class ThresholdRepository
{
    /// <summary>이 장치의 ChannelCatalog 채널별 임계값을 반환한다(_file_log의 Value0..Value5와 같은 순서; SetValue.db에는
    /// Base/Caution/Warning_0..11이 있지만 앞쪽 ChannelCount개만 쓴다). SetValue.db가 없거나 이 장치의 행이 없으면 null.</summary>
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
            // SetValue.db도 _file_log의 Value와 같은 원시 장치 정수 단위로 저장되어 있다(예: Base_2=501은 5.01 Hz).
            // 측정값과 같은 Scale/Decimals를 적용해야 점검/최소선이 측정값과 같은 단위로 그려진다 —
            // 적용하지 않으면 주파수/전압 4개 채널에서 선이 10~100배 큰 위치에 그려졌다.
            double scale = ChannelCatalog.ChannelScaleAt(i);
            int decimals = ChannelCatalog.ChannelDecimals(ChannelCatalog.ChannelIds[i]);
            double ToReal(string column) => Math.Round(Convert.ToDouble(reader[$"{column}_{i}"]) * scale, decimals);
            result.Add(new ChannelThreshold(ToReal("Base"), ToReal("Caution"), ToReal("Warning")));
        }
        return result;
    }

    private static bool _backedUp;

    /// <summary>
    /// 장치별로 항목(채널)마다 기준(Base)을 현재 측정값으로 재설정하고, 점검(Caution)·최소(Warning)를 그 측정값의 %로 계산해
    /// SetValue.db에 저장한다. <c>channels</c>는 ChannelCatalog 순서의 6개 항목이며 null인 항목은 건드리지 않는다. 항목마다
    /// 점검 %·최소 %가 다를 수 있고, 계산은 원시 정수로 SQLite가 직접 한다(원시값 × % / 100의 소수점 이하 버림 — 기존
    /// SetValue.db의 값이 그렇게 만들어져 있다: 105×70% = 73.5 → 73, 289×50% = 144.5 → 144). 실제 단위로 바꿨다 되돌리며
    /// 생기는 오차가 없다.
    /// 이미 행이 있는 장치는 최신 행(Time 최대) 하나에서 지정된 항목의 Base·점검·최소 컬럼만 수정하고 나머지 항목·Time·다른
    /// 컬럼은 건드리지 않는다. 행이 없는 장치는 새 행을 추가하므로 6개 항목이 모두 있어야 한다(Type은 기존 행에서 가장 흔한
    /// 값, Time은 지금; 6번 이후 채널은 0). 전체가 한 트랜잭션이라 중간에 실패하면 아무것도 바뀌지 않는다. SetValue.db는
    /// 현장 TLDS가 쓰는 설정 파일이므로 이 프로세스에서 처음 저장하기 전에 일관된 사본(SetValue.db.bak-날짜시각)을 한 번 만든다.
    /// </summary>
    /// <returns>(수정한 장치 수, 새로 추가한 장치 수).</returns>
    public static (int Updated, int Inserted) SavePercent(IEnumerable<(int ObjectIndex, ChannelSave?[] Channels)> items)
    {
        string path = TldsDataPaths.SetValueDbFile;
        if (!File.Exists(path)) throw new FileNotFoundException("SetValue.db를 찾을 수 없습니다.", path);

        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWrite, DefaultTimeout = 5, // TLDS가 잠시 잡고 있어도 5초는 기다린다
        }.ToString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        if (!_backedUp)
        {
            string backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            using var vacuum = conn.CreateCommand();
            vacuum.CommandText = $"VACUUM INTO '{backup.Replace("'", "''")}'";
            vacuum.ExecuteNonQuery();
            _backedUp = true;
        }

        int updated = 0, inserted = 0;
        using var tx = conn.BeginTransaction();

        long type;
        using (var typeCmd = conn.CreateCommand())
        {
            typeCmd.Transaction = tx;
            typeCmd.CommandText = "SELECT Type FROM Level01 GROUP BY Type ORDER BY COUNT(*) DESC LIMIT 1";
            type = typeCmd.ExecuteScalar() is long t ? t : 10;
        }
        long now = DateTimeOffset.Now.ToUnixTimeSeconds();

        // 항목별 계산식: 같은 파라미터($b/$c/$w + 항목 번호)를 UPDATE와 INSERT가 함께 쓴다.
        static string CautionExpr(int i) => $"CAST($b{i} * $c{i} / 100.0 + 1e-9 AS INTEGER)";
        static string WarningExpr(int i) => $"CAST($b{i} * $w{i} / 100.0 + 1e-9 AS INTEGER)";
        void AddParams(SqliteCommand cmd, ChannelSave?[] channels, int objectIndex)
        {
            cmd.Parameters.AddWithValue("$idx", objectIndex);
            for (int i = 0; i < channels.Length; i++)
            {
                if (channels[i] is not { } s) continue;
                cmd.Parameters.AddWithValue($"$b{i}", s.BaseRaw);
                cmd.Parameters.AddWithValue($"$c{i}", s.CautionPct);
                cmd.Parameters.AddWithValue($"$w{i}", s.WarningPct);
            }
        }

        foreach (var (objectIndex, channels) in items)
        {
            var targets = Enumerable.Range(0, channels.Length).Where(i => channels[i] is not null).ToList();
            if (targets.Count == 0) continue;

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE Level01 SET " + string.Join(", ", targets.Select(i =>
                $"Base_{i} = $b{i}, Caution_{i} = {CautionExpr(i)}, Warning_{i} = {WarningExpr(i)}")) +
                " WHERE rowid = (SELECT rowid FROM Level01 WHERE ObjectIndex = $idx ORDER BY Time DESC LIMIT 1)";
            AddParams(cmd, channels, objectIndex);
            if (cmd.ExecuteNonQuery() > 0) { updated++; continue; }

            // 행이 없음: 새 행에는 6개 항목이 모두 필요하다.
            if (targets.Count != ChannelCatalog.ChannelCount)
                throw new InvalidOperationException($"ObjectIndex {objectIndex}는 SetValue.db에 행이 없어 6개 항목을 모두 입력해야 새로 저장할 수 있습니다.");
            var all = Enumerable.Range(0, ChannelCatalog.ChannelCount).ToList();
            using var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = "INSERT INTO Level01 (Time, ObjectIndex, Type, " +
                string.Join(", ", all.Select(i => $"Base_{i}, Caution_{i}, Warning_{i}")) + ") VALUES ($time, $idx, $type, " +
                string.Join(", ", all.Select(i => $"$b{i}, {CautionExpr(i)}, {WarningExpr(i)}")) + ")";
            AddParams(insert, channels, objectIndex);
            insert.Parameters.AddWithValue("$time", now);
            insert.Parameters.AddWithValue("$type", type);
            insert.ExecuteNonQuery();
            inserted++;
        }
        tx.Commit();
        return (updated, inserted);
    }

    /// <summary>실제 임계값으로 기존 표준/측정/점검/최소 ChannelMeasurement 형태를 만든다 —
    /// 표준/점검/최소는 설정값 그대로이고, 측정값은 호출한 쪽이 넘긴다(그 시점의 실제 텔레메트리 샘플이며,
    /// _file_log에 없으면 null — 빈칸으로 표시하고 절대 추측하지 않는다).</summary>
    public static ChannelMeasurement ToMeasurement(ChannelThreshold t, double? measured) => new()
    {
        Standard = t.Base,
        Measured = measured,
        Checked = t.Caution,
        Minimum = t.Warning,
    };
}
