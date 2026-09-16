namespace TLDSDashBoard.ViewModels.Items;

public sealed class NavItemVM
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public bool IsActive { get; init; }
    public required string DotColorHex { get; init; }
    public string? Badge { get; init; }
}
