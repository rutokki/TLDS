using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// 궤도전압리스트: 선택한 역의 궤도(index_{역}.xml)마다 6개 채널의 현재 측정값(다른 exe가 공유메모리에 써 둔
/// 궤도 인덱스별 값, <see cref="TrackVoltageSharedMemory"/> 참고)과 SetValue.db의 기준값을 보여주고, 점검·최소값을
/// **현재 측정값의 %(정수)**로 설정해 SetValue.db에 저장하는 화면 — 저장하면 기준(Base)도 그 측정값으로 재설정된다.
/// % 는 화면 맨 위의 점검/최소 % 한 쌍으로만 정하며, 항목 칩이 켜져 있는 항목에만 [전체 적용]으로 모든 궤도에 적용된다(항목별·궤도별
/// % 입력은 없다). SetValue.db에 행이 없는 궤도도 측정값이 있으면 저장할 수 있다(새 행 추가).
/// 버튼은 [전체 적용]/[저장]/[불러오기]이며 Debug 빌드에만 테스트용 "공유메모리 저장"이 더 있다.
/// 화면이 열릴 때 한 번 불러오고(<see cref="SetActive"/>), 열려 있는 동안에만 측정값을
/// 주기적으로 갱신하며 화면을 떠나면 타이머를 멈춘다(OverView 외 화면은 백그라운드에서 데이터를 읽지 않는다).
/// 측정값 갱신은 적용해 둔(저장 전) 미리보기를 건드리지 않는다.
/// </summary>
public sealed class TrackVoltageListViewModel : ViewModelBase
{
    private readonly DispatcherTimer _measureTimer;

    public ObservableCollection<Station> Stations { get; } = new(ReferenceData.Stations);
    public ObservableCollection<TrackVoltageRowViewModel> Rows { get; } = new();

    private Station _selectedStation = ReferenceData.Stations[0];
    public Station SelectedStation
    {
        get => _selectedStation;
        set { if (SetProperty(ref _selectedStation, value) && _isActive) Reload(); }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private string _measureStatus = string.Empty;
    /// <summary>측정값 수신 현황(공유메모리에서 몇 개 궤도의 값을 읽었는지, 또는 읽지 못한 이유).</summary>
    public string MeasureStatus { get => _measureStatus; private set => SetProperty(ref _measureStatus, value); }

    /// <summary>항목(채널) 칩 6개 — 켜져 있는 항목만 [전체 적용]으로 수정된다.</summary>
    public ObservableCollection<ChannelChip> ChannelChips { get; } = new(
        ChannelCatalog.ChannelIds.Select(id => new ChannelChip(ChannelCatalog.ChannelName(id))));

    /// <summary>"전체" 칩 — 6개 항목 칩이 모두 켜져 있으면 true. 켜거나 끄면 6개 항목 칩이 모두 따라 바뀐다. 항목 칩이 하나라도
    /// 꺼지면 자동으로 꺼진 상태로 보인다.</summary>
    public bool AllChannelsOn
    {
        get => ChannelChips.All(c => c.IsOn);
        set { foreach (var c in ChannelChips) c.IsOn = value; }
    }

    private int _bulkCautionPct = 70;
    /// <summary>[전체 적용]이 켜진 항목에 넣을 점검 %(정수) — 화면에서 % 를 정하는 유일한 입력.</summary>
    public int BulkCautionPct { get => _bulkCautionPct; set => SetProperty(ref _bulkCautionPct, value); }

    private int _bulkWarningPct = 50;
    /// <summary>[전체 적용]이 켜진 항목에 넣을 최소 %(정수).</summary>
    public int BulkWarningPct { get => _bulkWarningPct; set => SetProperty(ref _bulkWarningPct, value); }

    public RelayCommand ApplyAllCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ReloadCommand { get; }
    /// <summary>[테스트 전용] Debug 빌드에서만 동작 — <see cref="IsSharedMemoryTestVisible"/> 참고.</summary>
    public RelayCommand SaveSharedMemoryCommand { get; }

    private bool _isActive;

    public TrackVoltageListViewModel()
    {
        // 항목 칩이 바뀌면 "전체" 칩의 표시(모두 켜짐 여부)를 다시 계산한다.
        foreach (var channel in ChannelChips)
            channel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ChannelChip.IsOn)) OnPropertyChanged(nameof(AllChannelsOn)); };

        ApplyAllCommand = new RelayCommand(ApplyAll);
        SaveCommand = new RelayCommand(Save);
        ReloadCommand = new RelayCommand(Reload);
#if DEBUG
        SaveSharedMemoryCommand = new RelayCommand(SaveToSharedMemory);
#else
        SaveSharedMemoryCommand = new RelayCommand(() => { });
#endif

        _measureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _measureTimer.Tick += (_, _) => RefreshMeasured();
        // 시작 시 자동 조회하지 않는다 — 화면이 활성화될 때 SetActive(true)가 처음 불러온다.
    }

    /// <summary>이 화면이 보이는 동안만 true. 켜질 때 DB에서 새로 불러오고 측정 갱신 타이머를 시작한다.</summary>
    public void SetActive(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;
        if (active)
        {
            Reload();
            _measureTimer.Start();
        }
        else
        {
            _measureTimer.Stop();
        }
    }

    /// <summary>[불러오기] 선택한 역의 궤도와 SetValue.db 임계값을 다시 읽고 공유메모리의 측정값도 다시 읽는다 —
    /// 저장하지 않은 편집은 버려진다.</summary>
    private void Reload()
    {
        Rows.Clear();
        int missing = 0;
        string? error = null;
        foreach (var track in ReferenceData.TracksForStation(SelectedStation.Id))
        {
            var row = new TrackVoltageRowViewModel(track, i => ChannelChips[i].IsOn);
            try
            {
                var thresholds = ThresholdRepository.LoadForDevice(track.Idx);
                row.LoadThreshold(thresholds);
                if (thresholds is null) missing++;
            }
            catch (Exception ex)
            {
                error ??= ex.Message;
                row.LoadThreshold(null);
                missing++;
            }
            Rows.Add(row);
        }

        StatusMessage = error is not null
            ? $"SetValue.db를 읽지 못했습니다: {error}"
            : missing > 0
                ? $"{Rows.Count}개 궤도 중 {missing}개는 SetValue.db에 행이 없습니다 — 현재 측정값이 있으면 % 를 입력해 새로 저장할 수 있습니다."
                : $"{Rows.Count}개 궤도를 불러왔습니다.";
        RefreshMeasured();
    }

    /// <summary>공유메모리에서 궤도 인덱스별 측정값을 읽어 모든 행의 측정 칸을 갱신한다(기준/점검/최소와 편집 중인 %는
    /// 그대로). 공유메모리에 없는 궤도의 측정 칸은 비운다.</summary>
    private void RefreshMeasured()
    {
        IReadOnlyDictionary<int, double[]>? latest;
        try
        {
            latest = TrackVoltageSharedMemory.Read();
        }
        catch (Exception ex)
        {
            foreach (var row in Rows) row.SetMeasured(null);
            MeasureStatus = $"공유메모리를 읽지 못했습니다: {ex.Message}";
            return;
        }

        if (latest is null)
        {
            foreach (var row in Rows) row.SetMeasured(null);
            MeasureStatus = $"공유메모리({TrackVoltageSharedMemory.MapName})가 아직 없습니다.";
            return;
        }

        int received = 0;
        foreach (var row in Rows)
        {
            if (latest.TryGetValue(row.Track.Idx, out var values)) { row.SetMeasured(values); received++; }
            else row.SetMeasured(null);
        }
        MeasureStatus = $"측정값 {received}/{Rows.Count}개 궤도 수신 (공유메모리)";
    }

#if DEBUG
    /// <summary>[테스트 전용 — Debug 빌드에만 있다] 읽기 처리를 시험할 샘플 데이터를 만들어 공유메모리에 쓰고, 바로 읽어
    /// 화면에 반영한다(다른 exe가 쓴 것처럼 <see cref="TrackVoltageSharedMemory"/> 배치대로). 화면에 있는 모든 궤도의
    /// 6개 채널 값을 만든다 — SetValue.db에 기준값이 있는 궤도는 그 기준값 ±4% 안, 없는 궤도는 일반적인 값
    /// (<see cref="SampleTypical"/>) ±4% 안. 누를 때마다 값이 조금씩 달라진다. 실제 배포(Release)에는 없다.</summary>
    private void SaveToSharedMemory()
    {
        var random = new Random();
        var records = new List<(int Idx, double[] Values)>();
        foreach (var row in Rows)
        {
            var values = new double[ChannelCatalog.ChannelCount];
            for (int i = 0; i < values.Length; i++)
            {
                double center = row.HasThreshold && row.Cells[i].Base is { } b && b > 0 ? b : SampleTypical[i];
                int decimals = ChannelCatalog.ChannelDecimals(ChannelCatalog.ChannelIds[i]);
                values[i] = Math.Round(center * (1 + (random.NextDouble() * 2 - 1) * 0.04), decimals);
            }
            records.Add((row.Track.Idx, values));
        }

        try
        {
            TrackVoltageSharedMemory.Write(records);
            RefreshMeasured();
            StatusMessage = $"[테스트] 공유메모리({TrackVoltageSharedMemory.MapName})에 {records.Count}개 궤도의 샘플 데이터를 만들어 읽어 왔습니다.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"공유메모리 쓰기 실패 ({ex.Message})";
        }
    }

    /// <summary>샘플 생성 시 SetValue.db에 기준값이 없는 궤도에 쓰는 일반적인 값(ChannelCatalog 순서) — 현장 SetValue.db 기준값의
    /// 평균 부근.</summary>
    private static readonly double[] SampleTypical = { 225, 580, 5.0, 15.0, 2.9, 1.0 };
#endif

    /// <summary>"공유메모리 저장" 테스트 버튼을 보일지 — Debug 빌드에서만 true, 실제 배포(Release)에는 버튼이 나오지 않는다.</summary>
    public bool IsSharedMemoryTestVisible =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>[전체 적용] 상단의 점검/최소 %를 **칩이 켜져 있는 항목에만**, 현재 측정값이 있는 모든 궤도에 넣는다(SetValue.db에 행이
    /// 없어도 포함). 각 항목의 점검/최소는 그 항목 측정값의 %로 계산되고 기준도 그 측정값으로 바뀐다. 꺼진 항목은 건드리지 않는다.
    /// [저장]을 눌러야 SetValue.db에 반영된다.</summary>
    private void ApplyAll()
    {
        var on = ChannelChips.Where(c => c.IsOn).ToList();
        if (on.Count == 0)
        {
            StatusMessage = "적용할 항목이 없습니다 — 위의 항목 칩(전체/궤도/차상/…)을 켜세요.";
            return;
        }
        if (BulkCautionPct is < 1 or > 100 || BulkWarningPct is < 1 or > 100 || BulkWarningPct > BulkCautionPct)
        {
            StatusMessage = "점검/최소 %는 1~100이고 최소 %가 점검 %보다 클 수 없습니다.";
            return;
        }

        int applied = Rows.Count(r => r.ApplyPct(BulkCautionPct, BulkWarningPct));
        int skipped = Rows.Count - applied;
        string target = on.Count == ChannelChips.Count ? "전체 항목" : string.Join(", ", on.Select(c => c.Name));
        StatusMessage = $"{applied}개 궤도의 {target}에 현재 측정값의 점검 {BulkCautionPct}% / 최소 {BulkWarningPct}%를 적용했습니다" +
                        (skipped > 0 ? $" (측정값 없는 {skipped}개 제외). " : ". ") +
                        "[저장]을 눌러야 기준·점검·최소가 SetValue.db에 반영됩니다.";
    }

    private void Save()
    {
        var dirty = Rows.Where(r => r.IsDirty).ToList();
        if (dirty.Count == 0)
        {
            StatusMessage = "변경된 점검/최소 %가 없습니다.";
            return;
        }

        foreach (var row in dirty)
        {
            if (row.Validate() is { } problem)
            {
                StatusMessage = $"{row.TrackName} — {problem}";
                return;
            }
        }

        var saves = dirty.Select(r => (Row: r, Channels: r.ToSaves())).ToList();
        int channelCount = saves.Sum(s => s.Channels.Count(c => c is not null));
        int added = dirty.Count(r => !r.HasThreshold);
        var answer = MessageBox.Show(
            $"{dirty.Count}개 궤도, 항목 {channelCount}개의 기준·점검·최소 값을 SetValue.db에 저장합니다.\n" +
            "기준값은 지금의 측정값으로 바뀌고, 점검/최소는 그 측정값의 %로 계산됩니다.\n" +
            (added > 0 ? $"그중 {added}개 궤도는 SetValue.db에 행이 없어 새 행으로 추가됩니다.\n" : "") +
            "현장 TLDS가 쓰는 설정 파일입니다. 저장 전 사본(SetValue.db.bak-…)을 만들어 둡니다.\n\n계속할까요?",
            "점검/최소 값 저장", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            StatusMessage = "저장을 취소했습니다.";
            return;
        }

        (int Updated, int Inserted) result;
        try
        {
            result = ThresholdRepository.SavePercent(saves.Select(s => (s.Row.Track.Idx, s.Channels)));
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패 — 아무것도 바뀌지 않았습니다 ({ex.Message})";
            return;
        }

        // 저장된 값을 DB에서 다시 읽어 화면과 실제 저장 결과가 같은지 보여준다(원시 정수 변환 포함).
        foreach (var row in dirty)
        {
            try { row.LoadThreshold(ThresholdRepository.LoadForDevice(row.Track.Idx)); }
            catch (Exception) { /* 다시 읽지 못해도 저장은 끝났다 — 다음 [불러오기]에서 확인된다 */ }
        }
        StatusMessage = $"{result.Updated + result.Inserted}개 궤도의 점검/최소 값을 저장했습니다" +
                        (result.Inserted > 0 ? $" (SetValue.db에 새로 추가 {result.Inserted}개)." : ".");
    }
}
