using System.Linq;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Fallback sample dataset, used only when <see cref="SqliteChannelDataRepository"/> can't reach
/// the real DB (file/table/columns not matching yet) so the UI still has something to render.
/// Seeded and held in memory (never re-randomized). The per-channel base/amplitude/noise values
/// below are placeholders picked only to produce plausible-looking waveforms for each signal type
/// (AC/voltage/pulse/frequency/current); swap them for real ranges once known. Timestamps are
/// synthesized on a 1-minute spacing ending "now" to match the DB's real sampling interval.
/// </summary>
public sealed class DataGenerator
{
    public const int SampleCount = 140;

    private readonly Mulberry32 _rand = new(1337);

    public ChannelDataSet GenerateChannels()
    {
        var now = DateTime.Now;
        var timestamps = new DateTime[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            timestamps[i] = now.AddMinutes(-(SampleCount - 1 - i));
        }

        return new ChannelDataSet
        {
            Timestamps = timestamps,
            TAC = GenSeries(110, 2.5, 0.09, 1.2, 0),      // 송신 AC (V)
            TV = GenSeries(24, 0.8, 0.11, 0.4, 0.7),      // 송신 출력전압 (V)
            TFP = GenSeries(48, 3.0, 0.14, 1.5, 1.4),     // 송신 정펄스 (ms)
            TSP = GenSeries(46, 3.0, 0.13, 1.5, 2.1),     // 송신 부펄스 (ms)
            THZ = GenSeries(91.7, 0.15, 0.08, 0.06, 0.3), // 송신 주파수 (Hz)
            TA = GenSeries(1.8, 0.15, 0.12, 0.05, 1.0),   // 송신 전류 (A)
            RFP = GenSeries(47, 3.2, 0.14, 1.6, 1.8),     // 수신 정펄스 (ms)
            RSP = GenSeries(45, 3.2, 0.13, 1.6, 2.5),     // 수신 부펄스 (ms)
            RHZ = GenSeries(91.6, 0.18, 0.08, 0.08, 0.5), // 수신 주파수 (Hz)
            RA = GenSeries(0.9, 0.10, 0.12, 0.04, 1.6),   // 수신 전류 (A)
            RV1 = GenSeries(12, 0.6, 0.10, 0.35, 2.0),    // 수신 전압1 (V)
            RV2 = GenSeries(11.5, 0.6, 0.10, 0.35, 2.6),  // 수신 전압2 (V)
        };
    }

    public List<AlarmEventModel> GenerateEvents()
    {
        (string time, string channel, string message, AlarmSeverity sev)[] rows =
        {
            ("09-10 14:12", "TV",  "송신 출력전압 스파이크 감지 (TV spike detected)", AlarmSeverity.Critical),
            ("09-10 13:47", "RHZ", "수신 주파수 이탈 (RHZ deviation)",                AlarmSeverity.Warning),
            ("09-10 12:03", "TA",  "송신 전류 정상 복귀 (TA normalized)",             AlarmSeverity.Info),
            ("09-10 10:55", "RFP", "수신 정펄스 폭 변동 경고 (RFP drift)",            AlarmSeverity.Warning),
            ("09-10 09:20", "RV1", "수신 전압1 정상 (RV1 stable)",                   AlarmSeverity.Info),
            ("09-09 22:41", "THZ", "송신 주파수 이탈 (THZ deviation)",                AlarmSeverity.Critical),
        };

        return rows.Select(r => new AlarmEventModel
        {
            Time = r.time,
            Channel = r.channel,
            Message = r.message,
            Severity = r.sev
        }).ToList();
    }

    private double[] GenSeries(double baseValue, double amp, double freq, double noise, double phase)
    {
        var arr = new double[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            arr[i] = baseValue + amp * Math.Sin(i * freq + phase) + (_rand.NextDouble() - 0.5) * noise;
        }
        return arr;
    }
}
