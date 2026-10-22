namespace TLDSDashBoard.ViewModels.Items;

/// <summary>궤도전압리스트 상단의 항목(채널) 칩 하나 — 켜져 있는 항목만 [전체 적용]으로 수정된다.</summary>
public sealed class ChannelChip : ViewModelBase
{
    public ChannelChip(string name) { Name = name; }

    /// <summary>항목 이름(예: "송신 전압").</summary>
    public string Name { get; }

    private bool _isOn = true;
    /// <summary>칩이 켜져 있으면 true — 이 항목만 수정 대상이 된다.</summary>
    public bool IsOn { get => _isOn; set => SetProperty(ref _isOn, value); }
}
