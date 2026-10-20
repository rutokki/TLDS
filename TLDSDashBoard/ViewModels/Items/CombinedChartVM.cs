using System.Collections.ObjectModel;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>
/// TX 또는 RX 그룹 하나의 겹친 차트: 하나의 공유 캔버스에 그려진 최대 6개 채널(각각 독립적으로
/// 정규화됨 — <see cref="ChannelSeriesVM"/> 참고), 범례, 표시 중인 창의 실제(DB에서 온) 시작/중간/끝 시각,
/// 그리고 MetricGroupView의 마우스 이동 핸들러가 구동하는 호버 상태(범례 값과 헤더의
/// <see cref="HoverTimeLabel"/>이 차트 위 마우스 위치를 따라간다).
/// </summary>
public sealed class CombinedChartVM : ViewModelBase
{
    public required string Header { get; init; }
    public required ObservableCollection<ChannelSeriesVM> Series { get; init; }

    /// <summary>표시 중인 창의 실제 샘플 시각. 인덱스는 각 계열의 WindowValues와 같으며, 호버 시 마우스 아래 시각을 알아내는 데 쓴다.</summary>
    public required DateTime[] WindowTimestamps { get; init; }

    public required string TimeStartLabel { get; init; }
    public required string TimeMidLabel { get; init; }
    public required string TimeEndLabel { get; init; }

    private string _hoverTimeLabel = string.Empty;
    /// <summary>차트 위에 호버 중일 때 마우스 아래의 정확한 시각. 호버 중이 아니면 비어 있다.</summary>
    public string HoverTimeLabel { get => _hoverTimeLabel; set => SetProperty(ref _hoverTimeLabel, value); }
}
