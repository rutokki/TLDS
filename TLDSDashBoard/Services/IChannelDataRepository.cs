using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// Source of the 12-channel TX/RX telemetry history. The dashboard displays data that already
/// exists in a database — it does not generate or receive live readings itself — so this is the
/// one seam to swap when the real schema is known (see <see cref="SqliteChannelDataRepository"/>).
/// </summary>
public interface IChannelDataRepository
{
    /// <summary>Loads the most recent <paramref name="maxSamples"/> rows, oldest first.</summary>
    ChannelDataSet LoadRecent(int maxSamples);
}
