# TLDSDashBoard — 철도신호 궤도회로 텔레메트리 대시보드 (WPF, .NET 10)

궤도회로 6채널 텔레메트리 — 주파수 3(`TRK_FREQ` 궤도/`CAB_FREQ` 차상/`CODE_FREQ` 지상) + 전압 3(`TX_VOLTAGE` 송신/`RX_A_VOLTAGE` 수신A/`RX_B_VOLTAGE` 수신B), `_file_log`의 Value0~5 순서 — 와,
열차점유·궤도장애·레벨기록·시간별상태출력 등 유지보수 도메인 화면을 함께 보여주는 데스크톱 대시보드입니다.
차트는 외부 라이브러리 없이 순수 XAML `Path`(좌표 문자열 직접 계산)로 그립니다.

## 열기 / 실행

1. Visual Studio(.NET 데스크톱 개발 워크로드)에서 `TLDSDashBoard.sln` 열고 시작 프로젝트 `TLDSDashBoard` 확인 후 F5
2. 또는 CLI: `cd TLDSDashBoard && dotnet run` (WPF는 Windows 전용)

`TargetFramework`는 `net10.0-windows`입니다.

### 배포용(릴리즈) 빌드

이 프로젝트는 `Microsoft.Data.Sqlite`(네이티브 종속성 포함) 패키지를 쓰기 때문에, Visual Studio의 "게시(Publish)" GUI로
자체 포함+단일 파일 옵션을 켜도 네이티브 dll이 exe 밖에 남는 문제가 있습니다. **아래 CLI 명령을 표준으로 씁니다**:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

결과물(`publish/TLDSDashBoard.exe`, 단일 파일)은 .NET 런타임이 없는 PC에도 그대로 복사해서 실행할 수 있습니다.
`publish/`는 빌드 산출물이라 `.gitignore`에서 제외되어 있습니다.

## 아키텍처

- MVVM(프레임워크 없이 직접 구현) — `ViewModelBase`(`INotifyPropertyChanged`) + `RelayCommand`(`ICommand`).
- 차트는 `Viewbox Stretch="Fill"` 위에 고정 가상 좌표(예: 900×90)로 그려서, 화면 크기가 달라져도 좌표 계산은 그대로 두고
  화면에 맞게 자동으로 늘어나게 합니다(`Services/ChartMetrics.cs`가 이 가상 좌표들을 정의).
- **실데이터 전용** — 모든 화면은 TLDS 설치 폴더의 `_file_log`/`_file_db` SQLite 파일만 표시합니다. 데이터가 없으면 샘플로 대체하지 않고 빈 상태와 사유를 보여줍니다.
- 배포 위치는 `TLDS.exe`와 같은 폴더(exe 옆의 `_file_*`를 읽음). 개발 시에는 `--data-root "C:경로	lds"` 인자 또는 `TLDS_DATA_ROOT` 환경변수로 실제 설치본을 가리킬 수 있습니다(`Services/TldsDataPaths.cs`).
- 사이드바 6개 화면 중 "시간별 상태 출력"만 오버레이 모달, 나머지는 메인 콘텐츠 영역 전환 방식입니다.

## 파일 구조

### 루트

| 파일 | 역할 |
|---|---|
| `App.xaml` / `App.xaml.cs` | 앱 진입점, 전역 리소스(색상/스타일/컨버터) 등록 |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | 사이드바 + 메인 콘텐츠(페이지별 전환) + 모달/토스트 오버레이 조립. 코드비하인드는 `DataContext = new MainViewModel()` 한 줄뿐 |

### Themes/

| 파일 | 역할 |
|---|---|
| `Colors.xaml` | 디자인 토큰(색상 브러시) — `CheckedLineBrush`/`MinimumLineBrush` 등 |
| `Styles.xaml` | 카드/버튼/텍스트/스크롤바/ComboBox/CheckBox 등 다크테마 공통 스타일 |

### Models/ — 데이터 형태(로직 없음)

| 파일 | 역할 |
|---|---|
| `ChannelDataSet.cs` | 6채널 시계열 원본(타임스탬프 1개당 채널 6개 값), `Get(id)`로 채널 id 조회 |
| `MetricDef.cs` | 채널 하나의 표시 메타데이터(Id/Name/Unit/색상/전체 데이터) |
| `AlarmEventModel.cs` | OverView 페이지 "최근 10분 알람" 로그 한 행 |
| `ChannelMeasurement.cs` | 표준/측정/점검/최소 4값 묶음(궤도장애·레벨기록 화면에서 채널마다 반복되는 단위) |
| `Station.cs` | 역(스테이션) — 필터용 참조 데이터(placeholder) |
| `TrackCircuit.cs` | 궤도회로(예: "010T") — `Station`에 속함, 필터용 참조 데이터(placeholder) |
| `TrainOccupancyRecord.cs` | 열차점유리스트 한 행(진입/통과/점유시간) |
| `TrackFaultRecord.cs` | 궤도 장애·경보 리스트 한 행 + 6채널 전체 `ChannelMeasurement` 스냅샷 |
| `LevelRecord.cs` | 레벨 기록 리스트 한 행(6채널 전체 `ChannelMeasurement` 스냅샷 + 기록일자/시간대) |
| `HourlyStatusEventRecord.cs` | 시간별 상태 출력 결과 한 행 + 좌측 카테고리 목록 정의 |

### Services/ — 데이터 소스 · 순수 계산 로직

| 파일 | 역할 |
|---|---|
| `IChannelDataRepository.cs` | 6채널 텔레메트리 이력의 소스 인터페이스(실제 DB 스키마 확정 시 교체할 지점) |
| `RealTelemetryRepository.cs` | `_file_log/{yyyy}/{MM}/{yyyy-MM-dd-HH}.db`(시간당 1파일)에서 장치별 6채널 실측값(Value0~5)을 읽음(OverView는 [현재−10분, 현재] 구간, 폴백 없음) |
| `ChannelCatalog.cs` | 6채널 id/이름/단위/색상/그룹(주파수·전압)의 단일 소스(데이터 없음) — 채널 추가·변경은 여기와 `ChannelDataSet`/`TrackFaultRecord`/`LevelRecord` 프로퍼티, 표 XAML 컬럼만 맞추면 됨 |
| `EventLogRepository.cs` | `_file_db/Event-{yyyy-MM-dd}.db`의 Event/Alarm 테이블(CP949 문자열) |
| `ThresholdRepository.cs` | `_file_db/SetValue.db`의 장치별 기준/주의(점검)/경고(최소) 설정값 |
| `TldsDataPaths.cs` | 데이터 루트 경로(기본: exe 폴더, `--data-root`/`TLDS_DATA_ROOT`로 변경) |
| `ReferenceData.cs` | 역/궤도회로 목록(필터 콤보용) — 시작 시 `StationConfigLoader` 결과를 1회 로드 |
| `StationConfigLoader.cs` | `_file_system/system.xml`의 역(`<station_no><match>`) → `index_{역}.xml`(궤도·LEU) + `rack_{역}.xml`(랙/슬롯) 로드. 파일은 CP949, 비정상 XML 주석은 제거 후 파싱 |
| `PathBuilder.cs` | 숫자 배열 → XAML `Path` 좌표 문자열 변환(`PathFromArray`/`PathFromArrayAutoRange`/`HorizontalLinePath`/`ComputeReferenceLines`) |
| `ChartMetrics.cs` | 차트별 고정 가상 캔버스 크기 상수(레인/드릴인 차트 등, XAML의 `Canvas Width/Height`와 반드시 일치해야 함) |

### ViewModels/

| 파일 | 역할 |
|---|---|
| `ViewModelBase.cs` | `INotifyPropertyChanged` 베이스 + `SetProperty` 헬퍼 |
| `RelayCommand.cs` | MVVM 커맨드 바인딩용 `ICommand` 구현체 |
| `MainViewModel.cs` | 내비게이션 상태(활성 페이지) + OverView 페이지의 주파수·전압 레인차트(`FrequencyChart`/`VoltageChart`)/알람/드릴인 모달/토스트/60초 갱신 타이머 소유. 5개 자식 페이지 VM을 프로퍼티로 노출 |
| `GraphDetailSearchViewModel.cs` | "그래프 상세검색" 페이지 VM — 6채널 각각 자기 실제값 축을 가진 스택 레인, 공유 줌/팬/호버 윈도우 |
| `HourlyStatusViewModel.cs` | "시간별 상태 출력" 모달 VM — 한 번의 검색으로 7개 카테고리 결과를 동시에 채움 |
| `TrainOccupancyViewModel.cs` | "열차점유리스트" 페이지 필터+결과 |
| `TrackFaultListViewModel.cs` | "궤도 장애·경보" 페이지 필터+결과 |
| `LevelRecordListViewModel.cs` | "레벨 기록 리스트" 페이지 필터+결과 |
| `DateRangeFilter.cs` | 시작~종료(시/분 단위) 기간 필터 — 대부분의 조회 화면에서 공용으로 씀 |

### ViewModels/Items/ — 화면 바인딩용 소형(레코드성) 뷰모델

| 파일 | 역할 |
|---|---|
| `NavItemVM.cs` | 사이드바 항목 하나(Id/Label/활성여부/점 색상) |
| `ChannelSeriesVM.cs` | 스택 레인 차트 한 줄(색상/이름/차트경로/축라벨/점검·최소 기준선/현재값) — OverView와 그래프 상세검색 양쪽에서 공용으로 씀. 값들은 mutable이라 줌/팬/호버 때 in-place로 갱신됨 |
| `CombinedChartVM.cs` | 주파수 또는 전압 그룹 하나(`ChannelSeriesVM` 목록 + 헤더 + 시간축 라벨 + 호버 상태) — `MetricGroupView`가 이걸 바인딩 |
| `DrillDataVM.cs` | 드릴인 모달의 채널 1개 확대 차트 상태(줌/팬/호버 중 값이 실시간으로 바뀜) |
| `HourlyStatusCategoryGroup.cs` | 시간별 상태 출력 모달의 카테고리 1개 결과 섹션 |
| `CalendarDayCell.cs` | `SimpleDatePicker` 달력 그리드의 날짜 셀 하나 |
| `MetricCardVM.cs` | ⚠️ **미사용(죽은 코드)** — 초기 KPI 카드 그리드 디자인의 잔재. 어디서도 참조되지 않음, 삭제 후보 |

### Views/

| 파일 | 역할 |
|---|---|
| `SidebarView.xaml(.cs)` | 좌측 내비게이션 목록(6개 항목) |
| `TopbarView.xaml(.cs)` | 상단바 |
| `ToastView.xaml(.cs)` | 우하단 알림 토스트 오버레이 |
| `GraphPageView.xaml(.cs)` | OverView 페이지 — 기간+장치 필터 + `MetricGroupView`(주파수/전압) + 최근 10분 알람 카드. (구 `KpiRowView`에서 이름 변경) |
| `MetricGroupView.xaml(.cs)` | TX 또는 RX 그룹의 스택 레인 차트 블록(채널마다 1줄, 공유 크로스헤어 + 마우스 추적 Popup 툴팁). OverView 페이지에서 두 번 재사용 |
| `GraphDetailSearchView.xaml(.cs)` | "그래프 상세검색" 페이지 — `MetricGroupView`와 동일한 레인 구조, 줌/팬/호버 공유 + Popup 툴팁 |
| `DrillModalView.xaml(.cs)` | 채널 하나를 크게 보는 확대 모달(줌/팬/호버 크로스헤어) |
| `HourlyStatusModalView.xaml(.cs)` | "시간별 상태 출력" 전체화면 모달(카테고리 목록 + 필터 + 결과 그리드) |
| `TrainOccupancyView.xaml(.cs)` | "열차점유리스트" 페이지(필터 + DataGrid) |
| `TrackFaultListView.xaml(.cs)` | "궤도 장애·경보" 페이지(필터 + DataGrid, 6채널 24개 측정컬럼) |
| `LevelRecordListView.xaml(.cs)` | "레벨 기록 리스트" 페이지(필터 + DataGrid, 6채널 24개 측정컬럼) |
| `DateRangeFilterView.xaml(.cs)` | `SimpleDatePicker` 2개(시작/종료)를 묶은 재사용 필터 컨트롤 — `Filter` DependencyProperty로 `DateRangeFilter` 바인딩 |
| `SimpleDatePicker.xaml(.cs)` | 완전 커스텀 날짜 선택 컨트롤 — 네이티브 `DatePicker`/`Calendar`가 다크테마 스타일링에 3차례 실패해서 직접 구현 |

### Converters/

| 파일 | 역할 |
|---|---|
| `PathDataConverter.cs` | `PathBuilder`가 만든 "M x,y L x,y..." 문자열 → `Geometry` (Path.Data 바인딩용) |
| `HexToBrushConverter.cs` | `"#RRGGBB"`/`"#AARRGGBB"` 문자열 → `SolidColorBrush` |
| `NullToVisibilityConverter.cs` | 값이 null이 아니면(문자열은 빈 값도 아니면) Visible, 아니면 Collapsed |
| `ZeroToVisibilityConverter.cs` | 개수가 0(또는 null)이면 Visible — 조회 전/결과 없음 안내문구(빈 상태 표시)용 |
| `BoldWhenTrueConverter.cs` | bool → FontWeight(true면 SemiBold, false면 Normal) |
| `SeverityToBrushConverter.cs` | 알람 심각도 → Critical/Warning/Info 텍스트·배경 브러시(`ConverterParameter="Bg"`로 배경 선택) |

## 알려진 제약 / 참고사항

- 역/궤도 목록은 하드코딩하지 않고 설치 폴더의 `system.xml`이 지정한 역의 `index_`/`rack_` 파일에서 읽습니다. 다른 역 파일이 폴더에 있어도 무시되며, `system.xml`이나 해당 `index_` 파일이 없으면 오류 메시지를 띄우고 종료합니다.
- `RealTelemetryRepository`는 Value0~5를 `ChannelCatalog` 순서(주파수 3 → 전압 3)로 매핑합니다(Value6~11은 미사용, 스키마에 채널 라벨 없음). OverView는 [현재−10분, 현재] 구간을 60초마다 다시 읽으며, 없으면 빈 화면+사유(상단바 배지 툴팁에 경로 표시)를 보여줍니다.
- 열차점유리스트는 아직 실데이터 소스(점유 기록 테이블)가 확인되지 않아 항상 비어 있습니다.
- `ViewModels/Items/MetricCardVM.cs`는 참조되지 않는 죽은 코드입니다(위 표 참고).
- 궤도장애·레벨기록 리스트의 "표준/측정/점검/최소" 4단 헤더는 2단 그리드헤더 대신 컬럼명에 평탄화해서 표시합니다.
