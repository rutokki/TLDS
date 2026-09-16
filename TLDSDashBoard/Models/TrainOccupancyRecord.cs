namespace TLDSDashBoard.Models;

/// <summary>One row of the 열차 점유 리스트 (train occupancy list): one train's entry/pass/occupancy times on one track circuit.</summary>
public sealed class TrainOccupancyRecord
{
    public required string TrackName { get; init; }
    public required string EntryTime { get; init; }
    public required string PassTime { get; init; }
    public required string OccupancyTime { get; init; }
}
