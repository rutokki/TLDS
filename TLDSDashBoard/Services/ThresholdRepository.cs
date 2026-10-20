using System.IO;
using Microsoft.Data.Sqlite;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>채널 하나의 고정 임계값 설정 — Base(기준)/Caution(주의)/Warning(경고). 사용자가 설정한 값이지
/// 측정값이 아니다. SetValue.db에서 오며, _file_log의 초당 측정값과는 별개다.</summary>
public sealed record ChannelThreshold(double Base, double Caution, double Warning);

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
