# TX/RX 신호 모니터링 대시보드 — WPF (.NET 10)

원래는 3상 전압(R/S/T) HTML 디자인 레퍼런스를 재현한 프로젝트였으나, 실제 도메인 데이터인 송신(TX) 6채널 + 수신(RX) 6채널 텔레메트리(`ChannelDataSet`: TAC/TV/TFP/TSP/THZ/TA/RFP/RSP/RHZ/RA/RV1/RV2)에 맞춰 구조를 다시 짰습니다.

## 열기 / 실행

1. Visual Studio(.NET 데스크톱 개발 워크로드 설치)에서 `TLDSDashBoard.sln` 열기
2. 시작 프로젝트가 `TLDSDashBoard`인지 확인 후 F5 실행
3. 또는 CLI: `cd TLDSDashBoard && dotnet run` (Windows에서만 빌드/실행 가능 — WPF는 Windows 전용)

> `TargetFramework`는 `net10.0-windows`로 설정되어 있습니다. 다른 .NET 버전만 설치되어 있다면 `.csproj`의 `<TargetFramework>` 값을 설치된 SDK 버전에 맞게 바꿔주세요.

별도의 NuGet 패키지 설치가 필요 없습니다. 차트는 외부 라이브러리 없이 순수 XAML `Path`(Data 문자열을 직접 계산)로 그렸습니다.

## 화면 구성이 바뀐 이유

12개 채널은 V(전압)/A(전류)/Hz(주파수)/ms(펄스)로 단위가 제각각이라, 기존처럼 하나의 큰 라인차트에 겹쳐 그리면 눈금이 맞지 않습니다. 그래서 상단의 "메인 차트 + 브러시(줌/팬)"는 없애고, 채널마다 화면 전체 너비를 쓰는 가로 줄(레인)로 표시해서 다중 채널 오실로스코프/EKG 모니터처럼 12줄을 위아래로 쌓았습니다(송신 6줄 + 수신 6줄, 섹션 헤더로 구분). 각 줄은 왼쪽에 채널명+Id, 가운데에 스파크라인, 오른쪽에 최신값을 보여주고, 줄을 클릭하면 드릴인 모달에서 그 채널만 크게(+ MIN/MAX/AVG) 봅니다. 상단바의 시간범위(1H~30D) 세그먼트 컨트롤도 메인 차트 전용 기능이었어서 함께 제거했습니다.

## 구조

```
TLDSDashBoard/
  App.xaml(.cs)              앱 진입점, 전역 리소스(색상/스타일/컨버터) 등록
  MainWindow.xaml(.cs)        사이드바 + 메인 콘텐츠(KPI/TX카드/RX카드/알람로그) + 모달/토스트 오버레이 조립
  Themes/
    Colors.xaml               디자인 토큰(색상 브러시)
    Styles.xaml                카드/버튼/텍스트/스크롤바 등 공통 스타일
  Models/
    ChannelDataSet.cs           12개 TX/RX 채널 데이터 배열
    MetricDef.cs                 채널 하나의 표시 메타데이터(Id/Name/Unit/색상/데이터)
    AlarmEventModel.cs           알람 로그 한 행
  Services/
    Mulberry32.cs               시드 기반 PRNG(mulberry32 포팅) — 동일 시드(1337)로 재현 가능한 파형 생성
    DataGenerator.cs            12개 채널 + 이벤트 로그 목데이터 생성 (base/amp/noise 값은 전부 자리표시자 — 실제 범위를 알려주시면 교체)
    PathBuilder.cs               데이터 배열 → XAML Path 좌표 문자열 변환
    ChartMetrics.cs               스파크라인/드릴인 차트의 고정 좌표 공간(뷰박스) 상수
  ViewModels/
    MainViewModel.cs             KPI/TX/RX 카드/알람/드릴인/토스트/시계 상태와 파생 데이터 계산
    Items/                        카드 등 바인딩용 소형 뷰모델(KpiItemVM, MetricCardVM, NavItemVM, DrillDataVM)
  Views/
    SidebarView, TopbarView(범위 컨트롤 제거), KpiRowView,
    MetricGroupView(제목 + 카드 그리드 — TX/RX 두 번 재사용), AlarmLogView,
    DrillModalView, ToastView — 각각 UserControl
  Converters/                   Path 문자열→Geometry, Hex→Brush, Null→Visibility, bool→FontWeight, 심각도→배지 브러시
```

## 색상 / 채널 매핑

`MainViewModel.cs` 상단에 12개 채널 각각의 색상 상수(`ColorTAC`, `ColorTV`, ...)가 있고, 채널 메타데이터(한글 라벨/단위)는 `BuildStaticData()`의 `_allChannels` 목록에 한 줄씩 정의되어 있습니다. 색상·라벨·단위를 바꾸려면 이 두 곳만 수정하면 됩니다. 지금은 12색을 특별한 규칙 없이(자유 배정) 서로 잘 구분되도록만 골랐습니다.

## KPI 카드 관련 가정

상단 KPI 4개는 12개 채널 중 대표값으로 **송신 출력전압(TV) / 수신 전압1(RV1) / 송신 주파수(THZ) / Active Alarms**를 임의로 선정했습니다. 다른 채널로 바꾸고 싶으시면 말씀해주세요.

## 알려진 제약

- 빌드 환경(Linux 샌드박스)에 WPF/.NET SDK가 없어 실제 컴파일 검증은 하지 못했습니다. 코드는 바인딩 경로·리소스 키·네임스페이스를 전수 대조했지만, Visual Studio에서 열었을 때 사소한 오타가 있다면 알려주시면 바로 수정하겠습니다.
- `DataGenerator`의 채널별 base/amplitude/noise 값과 단위(ms 등)는 실제 장비 스펙을 몰라 임의로 정한 자리표시자입니다. 실측 범위·단위를 알려주시면 그대로 반영하겠습니다.
- TX/RX 카드 그리드는 3열 고정(UniformGrid)입니다. 반응형(auto-fit)이 필요하시면 추가해드릴 수 있습니다.
