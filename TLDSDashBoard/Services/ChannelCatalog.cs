namespace TLDSDashBoard.Services;

/// <summary>
/// 6개 텔레메트리 채널의 표시 메타데이터(id/이름/단위/색/그룹), _file_log의 Value0..Value5 순서
/// (주파수 3 → 전압 3). 채널을 나열하는 모든 페이지의 단일 기준 — 여기서의 채널 인덱스는
/// SetValue.db 임계값 목록(Base_N/Caution_N/Warning_N)의 인덱스이기도 하다.
/// 데이터는 들고 있지 않다 — 앱에 표시되는 모든 값은 실제 _file_log/_file_db 파일에서 온다.
/// </summary>
public static class ChannelCatalog
{
    public const string FrequencyGroup = "주파수";
    public const string VoltageGroup = "전압";

    /// <summary>
    /// Scale / Decimals는 YK-900A 통신전문 규격을 따른다: 장치가 보내고(TLDS가 Value0..5에 저장하는 값은)
    /// 원시 big-endian 정수이며, 원시값 × Scale로 표시한다 —
    ///   Track_freq 정수(1590 → 1590 Hz), Cab_freq 정수, Code_freq ×100(320 → 3.2 Hz),
    ///   Tx_voltage ×10(1234 → 123.4 V), Rx_A/Rx_B_voltage ×100(123 → 1.23 V).
    /// RealTelemetryRepository가 로드 시점에 Scale을 한 번만 적용하므로, 모든 페이지가 실제 단위로 동작한다.
    /// </summary>
    private static readonly (string Id, string Name, string Unit, string ColorHex, string Group, double Scale, int Decimals)[] Specs =
    {
        ("TRK_FREQ",     "궤도 주파수", "Hz", "#4C9AFF", FrequencyGroup, 1,    0),
        ("CAB_FREQ",     "차상 주파수", "Hz", "#FF9F40", FrequencyGroup, 1,    0),
        ("CODE_FREQ",    "지상 주파수", "Hz", "#36D399", FrequencyGroup, 0.01, 2),
        ("TX_VOLTAGE",   "송신 전압",   "V",  "#FF6B6B", VoltageGroup,   0.1,  1),
        ("RX_A_VOLTAGE", "수신 A 전압", "V",  "#B983FF", VoltageGroup,   0.01, 2),
        ("RX_B_VOLTAGE", "수신 B 전압", "V",  "#FFD166", VoltageGroup,   0.01, 2),
    };

    public static IReadOnlyList<string> ChannelIds { get; } = Specs.Select(s => s.Id).ToList();

    public static int ChannelCount => Specs.Length;

    /// <summary>원시 DB 정수 → 실제 값 배율(위 규격 표 참고).</summary>
    public static double ChannelScale(string id)
    {
        return Specs.First(s => s.Id == id).Scale;
    }

    /// <summary>원시 DB 정수 → 실제 값 배율, Value0..5 위치별.</summary>
    public static double ChannelScaleAt(int index)
    {
        return Specs[index].Scale;
    }

    /// <summary>이 채널을 표시할 때의 소수 자릿수("F{n}").</summary>
    public static int ChannelDecimals(string id)
    {
        return Specs.First(s => s.Id == id).Decimals;
    }

    public static string ChannelFormat(string id)
    {
        return "F" + ChannelDecimals(id);
    }

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

    /// <summary>Value0..Value5 / 임계값 목록에서 채널의 위치.</summary>
    public static int IndexOf(string id)
    {
        return Array.FindIndex(Specs, s => s.Id == id);
    }
}
