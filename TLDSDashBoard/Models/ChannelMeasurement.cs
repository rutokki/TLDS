namespace TLDSDashBoard.Models;

/// <summary>The 표준(standard)/측정(measured)/점검(checked)/최소(minimum) readings recorded for one channel at one point in time — the recurring 4-value group seen throughout the 궤도 장애·경보 and 레벨 기록 screens.</summary>
public sealed class ChannelMeasurement
{
    public required double Standard { get; init; } //기준
    public required double? Measured { get; init; } // 측정 — null when no real sample exists for that moment (shown blank, never guessed)
    public required double Checked { get; init; } //점검
    public required double Minimum { get; init; } // 최소
}
