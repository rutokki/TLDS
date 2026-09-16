using System.Linq;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Fallback sample dataset, used only when <see cref="SqliteChannelDataRepository"/> can't reach
/// the real DB (file/table/columns not matching yet) so the UI still has something to render.
/// Seeded and held in memory (never re-randomized). The per-channel base/amplitude/noise values
/// below are placeholders picked only to produce plausible-looking waveforms for each signal type
/// (AC/voltage/pulse/frequency/current); swap them for real ranges once known. Timestamps are
/// synthesized on a 1-second spacing ending "now" to match the DB's real sampling interval.
/// </summary>
public sealed class DataGenerator
{
    public const int SampleCount = 600; // 10 minutes at the 1-second sampling interval

    /// <summary>Single source of truth for each channel's placeholder waveform shape (base/amplitude/
    /// frequency/noise/phase) plus its display name/unit — used both by <see cref="GenerateChannels"/>
    /// (all 12 at once) and <see cref="GenerateSingleChannelSeries"/> (one channel, any date range, for
    /// the 그래프 상세검색 page), so the two never drift out of sync with each other.</summary>
    private static readonly (string Id, string Name, string Unit, string ColorHex, double Base, double Amp, double Freq, double Noise, double Phase)[] ChannelSpecs =
    {
        ("TAC", "송신 AC",     "V",  "#4C9AFF", 110,   2.5,  0.09, 1.2,  0),
        ("TV",  "송신 출력전압", "V",  "#FF9F40", 24,    0.8,  0.11, 0.4,  0.7),
        ("TFP", "송신 정펄스",   "ms", "#36D399", 48,    3.0,  0.14, 1.5,  1.4),
        ("TSP", "송신 부펄스",   "ms", "#FF6B6B", 46,    3.0,  0.13, 1.5,  2.1),
        ("THZ", "송신 주파수",   "Hz", "#B983FF", 91.7,  0.15, 0.08, 0.06, 0.3),
        ("TA",  "송신 전류",     "A",  "#FFD166", 1.8,   0.15, 0.12, 0.05, 1.0),
        ("RFP", "수신 정펄스",   "ms", "#4EE1D6", 47,    3.2,  0.14, 1.6,  1.8),
        ("RSP", "수신 부펄스",   "ms", "#FF7AC6", 45,    3.2,  0.13, 1.6,  2.5),
        ("RHZ", "수신 주파수",   "Hz", "#A3E635", 91.6,  0.18, 0.08, 0.08, 0.5),
        ("RA",  "수신 전류",     "A",  "#8B93FF", 0.9,   0.10, 0.12, 0.04, 1.6),
        ("RV1", "수신 전압1",    "V",  "#2DD4BF", 12,    0.6,  0.10, 0.35, 2.0),
        ("RV2", "수신 전압2",    "V",  "#FB7185", 11.5,  0.6,  0.10, 0.35, 2.6),
    };

    public static IReadOnlyList<string> ChannelIds { get; } = ChannelSpecs.Select(s => s.Id).ToList();
    public static string ChannelName(string id) => ChannelSpecs.First(s => s.Id == id).Name;
    public static string ChannelUnit(string id) => ChannelSpecs.First(s => s.Id == id).Unit;
    public static string ChannelColor(string id) => ChannelSpecs.First(s => s.Id == id).ColorHex;

    private readonly Mulberry32 _rand;

    public DataGenerator(uint seed = 1337)
    {
        _rand = new Mulberry32(seed);
    }

    public ChannelDataSet GenerateChannels()
    {
        var now = DateTime.Now;
        var timestamps = new DateTime[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            timestamps[i] = now.AddSeconds(-(SampleCount - 1 - i));
        }

        double[] Gen(string id)
        {
            var s = ChannelSpecs.First(c => c.Id == id);
            return GenSeries(s.Base, s.Amp, s.Freq, s.Noise, s.Phase);
        }

        return new ChannelDataSet
        {
            Timestamps = timestamps,
            TAC = Gen("TAC"), TV = Gen("TV"), TFP = Gen("TFP"), TSP = Gen("TSP"), THZ = Gen("THZ"), TA = Gen("TA"),
            RFP = Gen("RFP"), RSP = Gen("RSP"), RHZ = Gen("RHZ"), RA = Gen("RA"), RV1 = Gen("RV1"), RV2 = Gen("RV2"),
        };
    }

    /// <summary>One channel's series ending at <paramref name="end"/> — used by the 그래프 상세검색 page,
    /// which queries a single channel/track/period instead of all 12 channels "live" like the Overview page.</summary>
    public (DateTime[] Timestamps, double[] Values) GenerateSingleChannelSeries(string channelId, DateTime end, int sampleCount)
    {
        var s = ChannelSpecs.First(c => c.Id == channelId);
        var timestamps = new DateTime[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            timestamps[i] = end.AddSeconds(-(sampleCount - 1 - i));
        }
        var values = GenSeries(s.Base, s.Amp, s.Freq, s.Noise, s.Phase, sampleCount);
        return (timestamps, values);
    }

    private static readonly (string Channel, string Message, AlarmSeverity Severity)[] AlarmTemplates =
    {
        ("TV", "송신 출력전압 스파이크 감지", AlarmSeverity.Critical),
        ("RHZ", "수신 주파수 이탈", AlarmSeverity.Warning),
        ("TA", "송신 전류 정상 복귀", AlarmSeverity.Info),
        ("RFP", "수신 정펄스 폭 변동 경고", AlarmSeverity.Warning),
        ("RV1", "수신 전압1 정상", AlarmSeverity.Info),
        ("THZ", "송신 주파수 이탈", AlarmSeverity.Critical),
    };

    /// <summary>Overview page's "최근 10분 알람" list — real timestamps within [start, end] (the same
    /// window the TX/RX charts are showing), not the fixed/unrelated ones this used to have.</summary>
    public List<AlarmEventModel> GenerateRecentAlarms(DateTime start, DateTime end)
    {
        var span = end - start;
        if (span <= TimeSpan.Zero) span = TimeSpan.FromMinutes(10);

        var rows = new List<AlarmEventModel>();
        int count = 3 + (int)(_rand.NextDouble() * 4);
        for (int i = 0; i < count; i++)
        {
            var at = start + span * _rand.NextDouble();
            var t = AlarmTemplates[(int)(_rand.NextDouble() * AlarmTemplates.Length)];
            rows.Add(new AlarmEventModel
            {
                Time = at.ToString("HH:mm:ss"),
                Channel = t.Channel,
                Message = t.Message,
                Severity = t.Severity,
            });
        }
        return rows.OrderByDescending(r => r.Time).ToList();
    }

    private double[] GenSeries(double baseValue, double amp, double freq, double noise, double phase, int? count = null)
    {
        int n = count ?? SampleCount;
        var arr = new double[n];
        for (int i = 0; i < n; i++)
        {
            arr[i] = baseValue + amp * Math.Sin(i * freq + phase) + (_rand.NextDouble() - 0.5) * noise;
        }
        return arr;
    }

    // ==================== 역/궤도 도메인 화면용 mock 데이터 ====================
    // 아래 4개 메서드는 전부 트랙 Id(또는 카테고리) 기반으로 시드를 고정해서, 같은 조회 조건이면
    // 항상 같은 결과가 나오도록 만든 placeholder 데이터입니다. 실제 DB 스키마가 정해지면 교체 대상.

    public static uint SeedFrom(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) h = (h ^ c) * 16777619;
            return h == 0 ? 1u : h;
        }
    }

    private static ChannelMeasurement GenMeasurement(Mulberry32 rand, double baseValue, double spread)
    {
        return new ChannelMeasurement
        {
            Standard = baseValue,
            Measured = baseValue + (rand.NextDouble() - 0.5) * spread,
            Checked = baseValue + (rand.NextDouble() - 0.5) * spread * 0.6,
            Minimum = baseValue - spread,
        };
    }

    public List<TrainOccupancyRecord> GenerateTrainOccupancy(TrackCircuit track, DateTime start, DateTime end)
    {
        var rand = new Mulberry32(SeedFrom("occupancy-" + track.Id));
        var span = end - start;
        if (span <= TimeSpan.Zero) span = TimeSpan.FromHours(1);

        var rows = new List<TrainOccupancyRecord>();
        int count = 4 + (int)(rand.NextDouble() * 5);
        for (int i = 0; i < count; i++)
        {
            var entry = start + span * rand.NextDouble();
            var occupancySeconds = 20 + rand.NextDouble() * 90;
            var pass = entry.AddSeconds(occupancySeconds);
            rows.Add(new TrainOccupancyRecord
            {
                TrackName = track.Name,
                EntryTime = entry.ToString("yyyy-MM-dd HH:mm:ss"),
                PassTime = pass.ToString("yyyy-MM-dd HH:mm:ss"),
                OccupancyTime = $"{occupancySeconds:F1} sec",
            });
        }
        return rows.OrderBy(r => r.EntryTime).ToList();
    }

    private static readonly string[] FaultTypes = { "부정 점유", "전압 주의", "전압 장애", "퓨즈 고장", "통신 고장" };

    public List<TrackFaultRecord> GenerateTrackFaults(TrackCircuit track, DateTime start, DateTime end)
    {
        var rand = new Mulberry32(SeedFrom("fault-" + track.Id));
        var span = end - start;
        if (span <= TimeSpan.Zero) span = TimeSpan.FromHours(1);

        ChannelMeasurement M(double baseValue) => GenMeasurement(rand, baseValue, baseValue * 0.05 + 0.5);

        var rows = new List<TrackFaultRecord>();
        int count = 2 + (int)(rand.NextDouble() * 4);
        for (int i = 0; i < count; i++)
        {
            var occurred = start + span * rand.NextDouble();
            var recovered = occurred.AddMinutes(1 + rand.NextDouble() * 20);
            rows.Add(new TrackFaultRecord
            {
                TrackName = track.Name,
                Date = occurred.ToString("yyyy-MM-dd"),
                OccurredAt = occurred.ToString("HH:mm:ss"),
                RecoveredAt = recovered.ToString("HH:mm:ss"),
                FaultType = FaultTypes[(int)(rand.NextDouble() * FaultTypes.Length)],
                Imp = (50 + rand.NextDouble() * 10).ToString("F1"),
                Af = (1700 + rand.NextDouble() * 50).ToString("F0"),
                TrackType = "IMP",
                Tac = M(110), Tv = M(24), Tfp = M(48), Tsp = M(46), Thz = M(91.7), Ta = M(1.8),
                Rfp = M(47), Rsp = M(45), Rhz = M(91.6), Ra = M(0.9), Rv1 = M(12), Rv2 = M(11.5),
            });
        }
        return rows.OrderBy(r => r.Date).ThenBy(r => r.OccurredAt).ToList();
    }

    public List<LevelRecord> GenerateLevelRecords(TrackCircuit track, Station station, DateTime recordDate, string hourRangeLabel)
    {
        var rand = new Mulberry32(SeedFrom($"level-{track.Id}-{recordDate:yyyyMMdd}-{hourRangeLabel}"));

        ChannelMeasurement M(double baseValue) => GenMeasurement(rand, baseValue, baseValue * 0.05 + 0.3);

        var rows = new List<LevelRecord>();
        int count = 3 + (int)(rand.NextDouble() * 4);
        for (int i = 0; i < count; i++)
        {
            rows.Add(new LevelRecord
            {
                StationName = station.Name,
                TrackName = track.Name,
                RecordDate = recordDate.ToString("yyyy-MM-dd"),
                HourRangeLabel = hourRangeLabel,
                Rfp = M(47), Rsp = M(45), Rhz = M(91.6), Ra = M(0.9), Rv1 = M(12), Rv2 = M(11.5),
            });
        }
        return rows;
    }

    private static readonly string[] DeviceNames = { "선로전환기 #1", "신호기 #3", "폐색장치 A", "LEU-02", "궤도회로 010T" };

    public List<HourlyStatusEventRecord> GenerateHourlyStatusEvents(HourlyStatusCategory category, Station station, DateTime start, DateTime end)
    {
        var rand = new Mulberry32(SeedFrom("hourly-" + category));
        var span = end - start;
        if (span <= TimeSpan.Zero) span = TimeSpan.FromHours(1);

        string CategoryLabel() => category switch
        {
            HourlyStatusCategory.TrackFault => "궤도 장애",
            HourlyStatusCategory.SignalFailure => "신호기 고장",
            HourlyStatusCategory.SwitchMachineFailure => "선로전환기 고장",
            HourlyStatusCategory.BlockDeviceFailure => "폐색장치 고장",
            HourlyStatusCategory.LeuFailure => "LEU 고장",
            HourlyStatusCategory.SignalEquipmentEvent => "신호설비 이벤트",
            _ => "시스템 이벤트",
        };

        var rows = new List<HourlyStatusEventRecord>();
        int count = 3 + (int)(rand.NextDouble() * 5);
        for (int i = 0; i < count; i++)
        {
            var at = start + span * rand.NextDouble();
            rows.Add(new HourlyStatusEventRecord
            {
                OccurredAt = at.ToString("yyyy-MM-dd HH:mm:ss"),
                StationName = station.Name,
                DeviceName = DeviceNames[(int)(rand.NextDouble() * DeviceNames.Length)],
                Content = $"{CategoryLabel()} 발생",
            });
        }
        return rows.OrderBy(r => r.OccurredAt).ToList();
    }
}
