using TLDSDashBoard.Models;
using TLDSDashBoard.Services;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>
/// 궤도전압리스트 한 행 안의 항목(채널) 하나 — 궤도/차상/지상 주파수, 송신/수신A/수신B 전압 중 하나의 기준/측정/점검/최소.
/// 값이 null이면 화면에서 빈칸이다.
///
/// 점검/최소는 기준값이 아니라 **현재 측정값**에 대한 %다. 상단의 [전체 적용]이 % 를 넣는 순간(<see cref="ApplyPct"/>)의
/// 측정값을 스냅샷으로 잡아 기준 칸을 그 값으로 바꾸고, 점검 = 측정값 × 점검 %, 최소 = 측정값 × 최소 % 로 미리보기를 보여준다
/// (저장 전에는 <see cref="IsDirty"/>). 저장하면 SetValue.db의 이 항목의 기준·점검·최소가 이 값으로 재설정된다. [불러오기]는
/// 스냅샷과 미리보기를 버리고 DB 값으로 돌아간다. 스냅샷을 잡으려면 이 항목의 측정값이 있어야 하므로 측정값이 없으면
/// 적용할 수 없다(<see cref="CanEdit"/>).
/// </summary>
public sealed class ChannelCellViewModel : ViewModelBase
{
    private readonly int _index;

    /// <summary>값이 바뀌어 행(<see cref="TrackVoltageRowViewModel"/>)에 알려야 할 때 호출된다.</summary>
    internal Action? Changed { get; set; }

    public ChannelCellViewModel(int index) { _index = index; }

    /// <summary>항목 이름(예: "송신 전압") — 오류 메시지에 쓴다.</summary>
    public string ChannelName => ChannelCatalog.ChannelName(ChannelCatalog.ChannelIds[_index]);

    private double? _base;
    /// <summary>기준(Base) 값 — SetValue.db의 값. 적용하면 그 순간의 측정값으로 바뀐다(저장하면 이 값이 새 기준이 된다).</summary>
    public double? Base { get => _base; private set => SetProperty(ref _base, value); }

    private double? _measured;
    /// <summary>공유메모리에서 읽은 현재 측정값.</summary>
    public double? Measured
    {
        get => _measured;
        private set { if (SetProperty(ref _measured, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    private double? _caution;
    /// <summary>점검값 — 저장된 값, 또는 적용한 뒤에는 저장하면 DB에 들어갈 미리보기 값(측정값 × %).</summary>
    public double? Caution { get => _caution; private set => SetProperty(ref _caution, value); }

    private double? _minimum;
    /// <summary>최소값 — 위 점검값과 같은 방식.</summary>
    public double? Minimum { get => _minimum; private set => SetProperty(ref _minimum, value); }

    /// <summary>적용된 점검 %(정수) — 적용하지 않았으면 null.</summary>
    private int? _cautionPct;
    /// <summary>적용된 최소 %(정수) — 적용하지 않았으면 null.</summary>
    private int? _warningPct;

    private bool _isDirty;
    /// <summary>저장 대기 중인 변경(적용했지만 아직 저장하지 않음)이 있는지 — 화면에서 황색으로 표시된다.</summary>
    public bool IsDirty { get => _isDirty; private set => SetProperty(ref _isDirty, value); }

    /// <summary>적용한 순간의 측정값 — 점검/최소 계산과 저장될 새 기준값의 원천. 적용하지 않았으면 null.</summary>
    private double? _snapshot;

    /// <summary>DB에 저장돼 있는 이 항목의 값. 행이 없으면 null.</summary>
    private ChannelThreshold? _db;

    /// <summary>적용 가능한지 — 이 항목의 현재 측정값이 있을 때(이미 적용해 스냅샷을 잡은 뒤에는 측정값이 사라져도 된다).</summary>
    public bool CanEdit => _snapshot.HasValue || _measured.HasValue;

    /// <summary>적용된 %가 저장 가능한 범위인지 — 0 &lt; 최소 ≤ 점검 ≤ 100. 아니면 사유를 돌려준다.</summary>
    public string? Validate()
    {
        if (_cautionPct is not { } c || _warningPct is not { } w) return "점검/최소 %가 적용되지 않았습니다.";
        if (!_snapshot.HasValue) return "현재 측정값이 없어(공유메모리) 계산할 수 없습니다.";
        if (c <= 0 || c > 100 || w <= 0 || w > 100) return "점검/최소 %는 1 이상 100 이하여야 합니다.";
        if (w > c) return "최소 %는 점검 %보다 클 수 없습니다.";
        return null;
    }

    /// <summary>저장할 내용 — 저장 대기 중이 아니거나 저장할 수 없는 상태면 null.</summary>
    public ChannelSave? ToSave() =>
        IsDirty && Validate() is null && _snapshot is { } s
            ? new ChannelSave(_cautionPct!.Value, _warningPct!.Value, (int)Math.Round(s / ChannelCatalog.ChannelScaleAt(_index)))
            : null;

    /// <summary>[전체 적용]: 이 순간의 측정값을 기준으로 잡고 점검/최소 %를 적용한다(저장 대기로 표시). 현재 측정값이 없으면
    /// 적용하지 않고 false를 돌려준다.</summary>
    public bool ApplyPct(int caution, int warning)
    {
        if (!CanEdit) return false;
        _cautionPct = caution;
        _warningPct = warning;
        // 다시 적용하면 새 순간의 측정값으로 기준을 다시 잡는다.
        _snapshot = _measured ?? _snapshot;
        if (_snapshot is { } s)
        {
            Base = s;
            Caution = Preview(s, caution);
            Minimum = Preview(s, warning);
        }
        IsDirty = true;
        OnPropertyChanged(nameof(CanEdit));
        Changed?.Invoke();
        return true;
    }

    /// <summary>SetValue.db의 이 항목 값을 채운다(적용했던 미리보기와 스냅샷은 버려진다). t가 null이면 이 궤도는 DB에 행이 없는 것이다.</summary>
    public void LoadDb(ChannelThreshold? t)
    {
        _db = t;
        _snapshot = null;
        _cautionPct = null;
        _warningPct = null;
        IsDirty = false;
        ShowDb();
        OnPropertyChanged(nameof(CanEdit));
        Changed?.Invoke();
    }

    /// <summary>공유메모리에서 읽은 측정값을 넣는다. null이면 측정 칸을 비운다. DB 행이 없고 아직 적용하지 않은 항목은 기준 칸이
    /// 측정값을 따라간다(저장하면 그 값이 기준이 되므로).</summary>
    public void SetMeasured(double? value)
    {
        Measured = value;
        if (_db is null && !_snapshot.HasValue) Base = value;
        Changed?.Invoke();
    }

    /// <summary>적용하지 않은 상태의 칸 — DB 값, 행이 없으면 기준은 현재 측정값이고 점검/최소는 비어 있다.</summary>
    private void ShowDb()
    {
        Base = _db?.Base ?? Measured;
        Caution = _db?.Caution;
        Minimum = _db?.Warning;
    }

    /// <summary>저장 시 SQLite가 계산하는 값과 같게 미리 계산한다: 원시 정수 값 × % ÷ 100의 소수점 이하 버림
    /// (ThresholdRepository.SavePercent와 같은 식).</summary>
    private double? Preview(double? value, int? pct)
    {
        if (value is not { } v || pct is not { } p) return null;
        double scale = ChannelCatalog.ChannelScaleAt(_index);
        int decimals = ChannelCatalog.ChannelDecimals(ChannelCatalog.ChannelIds[_index]);
        double rawValue = Math.Round(v / scale);
        double raw = Math.Floor(rawValue * p / 100.0 + 1e-9);
        return Math.Round(raw * scale, decimals);
    }
}

/// <summary>
/// 궤도전압리스트의 궤도 하나(index_{역}.xml의 궤도) — 6개 항목(<see cref="Cells"/>)을 가진다. 상단의 [전체 적용]이 **항목 칩이 켜져
/// 있는 항목에만** 점검/최소 %를 적용한다(<see cref="ApplyPct"/>). 저장은 적용한 항목만 SetValue.db에 반영하며(행이 없는 궤도는
/// 6개 항목을 모두 적용해야 새 행으로 추가된다), 저장하면 그 항목의 기준도 적용한 순간의 측정값으로 재설정된다.
/// </summary>
public sealed class TrackVoltageRowViewModel : ViewModelBase
{
    private readonly Func<int, bool> _isChannelOn;

    /// <param name="isChannelOn">항목 번호(ChannelCatalog 순서) → 상단 항목 칩이 켜져 있는지. 생략하면 6개 항목이 모두 켜진 것으로 본다.</param>
    public TrackVoltageRowViewModel(TrackCircuit track, Func<int, bool>? isChannelOn = null)
    {
        Track = track;
        _isChannelOn = isChannelOn ?? (_ => true);
        Cells = Enumerable.Range(0, ChannelCatalog.ChannelCount).Select(i => new ChannelCellViewModel(i) { Changed = CellChanged }).ToArray();
    }

    public TrackCircuit Track { get; }
    public string TrackName => Track.Name;

    /// <summary>ChannelCatalog 순서(궤도/차상/지상 주파수, 송신/수신A/수신B 전압)의 6개 항목.</summary>
    public ChannelCellViewModel[] Cells { get; }

    private bool _hasThreshold;
    /// <summary>SetValue.db에 이 궤도의 행이 있는지 — 없으면 저장할 때 새 행이 추가된다.</summary>
    public bool HasThreshold { get => _hasThreshold; private set => SetProperty(ref _hasThreshold, value); }

    /// <summary>저장 대기 중인 변경이 있는 항목이 하나라도 있는지.</summary>
    public bool IsDirty => Cells.Any(c => c.IsDirty);

    private void CellChanged() => OnPropertyChanged(nameof(IsDirty));

    /// <summary>[전체 적용]: **켜진 항목에만** 점검/최소 %를 적용한다(꺼진 항목과 현재 측정값이 없는 항목은 건드리지 않는다).
    /// 적용된 항목이 하나라도 있으면 true.</summary>
    public bool ApplyPct(int caution, int warning)
    {
        bool any = false;
        for (int i = 0; i < Cells.Length; i++)
        {
            if (_isChannelOn(i) && Cells[i].ApplyPct(caution, warning)) any = true;
        }
        return any;
    }

    /// <summary>저장 가능한 상태인지 — 문제가 있으면 "항목: 사유"를 돌려준다. 이미 DB 행이 있는 궤도는 적용한 항목만, 행이 없는
    /// 궤도는 새 행에 6개 항목이 모두 필요하므로 전체 항목을 검사한다.</summary>
    public string? Validate()
    {
        foreach (var cell in Cells)
        {
            if (!HasThreshold || cell.IsDirty)
                if (cell.Validate() is { } problem) return $"{cell.ChannelName}: {problem}";
        }
        return null;
    }

    /// <summary>항목별 저장 내용(적용하지 않은 항목은 null). 행이 없는 궤도는 6개 항목이 모두 채워져야 한다(<see cref="Validate"/>).</summary>
    public ChannelSave?[] ToSaves() => Cells.Select(c => c.ToSave()).ToArray();

    /// <summary>SetValue.db의 임계값을 채운다(적용했던 미리보기는 버려진다). thresholds가 null이면 이 궤도는 DB에 행이 없는 것이다.</summary>
    public void LoadThreshold(IReadOnlyList<ChannelThreshold>? thresholds)
    {
        HasThreshold = thresholds is not null;
        for (int i = 0; i < Cells.Length; i++) Cells[i].LoadDb(thresholds?[i]);
        CellChanged();
    }

    /// <summary>공유메모리에서 읽은 측정값(ChannelCatalog 순서)을 넣는다. null이면 측정 칸을 비운다.</summary>
    public void SetMeasured(double[]? values)
    {
        for (int i = 0; i < Cells.Length; i++) Cells[i].SetMeasured(values?[i]);
    }
}
