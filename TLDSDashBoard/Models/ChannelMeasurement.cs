namespace TLDSDashBoard.Models;

/// <summary>The 표준(standard)/측정(measured)/점검(checked)/최소(minimum) readings recorded for one TX/RX channel at one point in time — the recurring 4-value group seen throughout the 궤도 장애·경보 and 레벨 기록 screens.</summary>
public sealed class ChannelMeasurement
{
    public required double Standard { get; init; }
    public required double Measured { get; init; }
    public required double Checked { get; init; }
    public required double Minimum { get; init; }
}
