using System.Globalization;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>
/// <see cref="CombinedChartVM"/> 안의 채널 하나의 행("레인") — 자신의 실제 값 축을 가진 쌓인 미니 차트
/// (<see cref="AxisMaxLabel"/>/<see cref="AxisMinLabel"/>, 그려진 창 자체의 최소/최대 — 채널을 겹치지 않고 쌓는
/// 이유는 단위(V/A/Hz/ms)가 하나의 스케일을 공유하지 않기 때문이다). <see cref="Value"/>는 처음에는
/// <see cref="LatestValue"/>와 같지만, 마우스가 공유 차트 영역 위에 있는 동안 실시간으로 바뀌어(MetricGroupView의
/// 마우스 핸들러 참고) 라벨에 가장 최근 샘플 대신 커서 아래의 값이 표시된다.
/// <see cref="WindowValues"/>가 그 조회를 가능하게 한다. 라벨을 클릭하면 이 채널의 드릴인 모달이 열린다.
///
/// 차트 모양 프로퍼티(Path/축 라벨/기준선/WindowValues)는 가변(init 전용 아님)이라서, 줌/팬 동안 채널마다
/// ChannelSeriesVM 하나를 유지하는 페이지 — GraphDetailSearchViewModel 참고 — 가 매 프레임 컬렉션 전체를 다시
/// 만들지 않고 제자리에서 다시 계산할 수 있다. 호버 때 Value를 제자리에서 바꾸는 것과 같은 방식이다.
/// </summary>
public sealed class ChannelSeriesVM : ViewModelBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }

    private string _path = "";
    public required string Path { get => _path; set => SetProperty(ref _path, value); }

    /// <summary>이 레인 차트의 값 축 라벨(그려진 창의 위쪽/최대와 아래쪽/최소이며, 아래 점검/최소 기준선까지 포함하도록
    /// 확장됨) — 정규화하지 않은 실제 숫자.</summary>
    private string _axisMaxLabel = "";
    public required string AxisMaxLabel { get => _axisMaxLabel; set => SetProperty(ref _axisMaxLabel, value); }
    private string _axisMinLabel = "";
    public required string AxisMinLabel { get => _axisMinLabel; set => SetProperty(ref _axisMinLabel, value); }

    /// <summary>점검(check)/최소(minimum) 기준 임계값. 실제 데이터와 같은 축 스케일에서 평평한 점선으로
    /// 그린다 — PathBuilder.HorizontalLinePath 참고.</summary>
    private string _checkedLinePath = "";
    public required string CheckedLinePath { get => _checkedLinePath; set => SetProperty(ref _checkedLinePath, value); }
    private string _minimumLinePath = "";
    public required string MinimumLinePath { get => _minimumLinePath; set => SetProperty(ref _minimumLinePath, value); }
    private string _checkedLabel = "";
    public required string CheckedLabel { get => _checkedLabel; set => SetProperty(ref _checkedLabel, value); }
    private string _minimumLabel = "";
    public required string MinimumLabel { get => _minimumLabel; set => SetProperty(ref _minimumLabel, value); }

    /// <summary>표시 중인 창의 이 채널 실제 값. 인덱스는 <see cref="CombinedChartVM.WindowTimestamps"/>와 같으며, 호버 시 마우스 아래 값을 찾는 데 쓴다.</summary>
    private double[] _windowValues = Array.Empty<double>();
    public required double[] WindowValues { get => _windowValues; set => SetProperty(ref _windowValues, value); }

    /// <summary><see cref="WindowValues"/>의 값을 그릴 때 적용하는 숫자 형식("F1"/"F2").</summary>
    private string _valueFormat = "F1";
    public required string ValueFormat { get => _valueFormat; set => SetProperty(ref _valueFormat, value); }

    /// <summary>가장 최근 샘플의 서식화된 값 — 마우스가 차트를 벗어나면 <see cref="Value"/>가 이 값으로 돌아간다.</summary>
    private string _latestValue = "";
    public required string LatestValue { get => _latestValue; set => SetProperty(ref _latestValue, value); }

    private string _value = string.Empty;
    /// <summary>현재 표시되는 값: 평소에는 최근 샘플, 호버 중에는 마우스 아래 샘플.</summary>
    public string Value { get => _value; set => SetProperty(ref _value, value); }

    public void ShowValueAt(int index)
    {
        Value = index >= 0 && index < WindowValues.Length
            ? WindowValues[index].ToString(ValueFormat, CultureInfo.InvariantCulture)
            : LatestValue;
    }

    public void ResetToLatest() => Value = LatestValue;
}
