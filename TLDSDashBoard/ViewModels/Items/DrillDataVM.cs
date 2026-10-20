using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>
/// 채널 하나의 차트 상태. 두 가지로 쓰인다: 드릴인 모달에서는 가변으로 쓰여(모달이 열려 있는 동안 줌/팬/호버가
/// 모두 실시간으로 갱신함 — 같은 이유는 ChannelSeriesVM 참고), 그래프 상세검색의 채널 격자에서는 한 번 계산한
/// 단순 스냅샷으로 쓰인다(거기에는 줌/팬이 없어서 Hover*는 설정되지 않은 채로 남음). 어느 쪽이든
/// 모양이 같아서, 비슷한 클래스 두 개 대신 하나로 둘 다 처리한다.
/// </summary>
public sealed class DrillDataVM : ViewModelBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }

    private string _bigPath = "";
    public string BigPath { get => _bigPath; set => SetProperty(ref _bigPath, value); }

    private string _min = "";
    public string Min { get => _min; set => SetProperty(ref _min, value); }

    private string _max = "";
    public string Max { get => _max; set => SetProperty(ref _max, value); }

    private string _avg = "";
    public string Avg { get => _avg; set => SetProperty(ref _avg, value); }

    /// <summary>점검(check)/최소(minimum) reference thresholds, same as the overview lane charts — see PathBuilder.HorizontalLinePath.</summary>
    private string _checkedLinePath = "";
    public string CheckedLinePath { get => _checkedLinePath; set => SetProperty(ref _checkedLinePath, value); }

    private string _minimumLinePath = "";
    public string MinimumLinePath { get => _minimumLinePath; set => SetProperty(ref _minimumLinePath, value); }

    /// <summary>현재 보이는(줌/팬된) 창의 시작~끝 시각.</summary>
    private string _rangeLabel = "";
    public string RangeLabel { get => _rangeLabel; set => SetProperty(ref _rangeLabel, value); }

    /// <summary>e.g. "42/140 샘플 표시 중" — zoom-level feedback (drill-in modal only).</summary>
    private string _windowLabel = "";
    public string WindowLabel { get => _windowLabel; set => SetProperty(ref _windowLabel, value); }

    /// <summary>커서 아래의 실시간 값. 마우스가 차트 위에 없으면 비어 있다(드릴인 모달 전용).</summary>
    private string? _hoverValue;
    public string? HoverValue { get => _hoverValue; set => SetProperty(ref _hoverValue, value); }

    private string? _hoverTimeLabel;
    public string? HoverTimeLabel { get => _hoverTimeLabel; set => SetProperty(ref _hoverTimeLabel, value); }
}
