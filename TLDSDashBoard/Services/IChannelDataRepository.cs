using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Source of the 6-channel (주파수/전압) telemetry history. The dashboard displays data that already
/// exists on disk — it does not generate or receive live readings itself (see
/// <see cref="RealTelemetryRepository"/> for the real _file_log-backed implementation).
/// </summary>
public interface IChannelDataRepository
{
    /// <summary>Loads every row for the given device within [start,end] (inclusive), oldest first. Throws
    /// if no real data covers any part of that window — callers should treat that as "no data", not fall
    /// back to a different window or to generated data.</summary>
    ChannelDataSet LoadRange(int deviceIdx, DateTime start, DateTime end);
}
