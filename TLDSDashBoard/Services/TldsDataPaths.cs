using System.IO;
namespace TLDSDashBoard.Services;

/// <summary>
/// 현장 TLDS 프로그램(TLDS.exe)이 쓰는 실제 TLDS 데이터 파일 위치를 찾는다. 대시보드는 TLDS 설치 폴더
/// 안(TLDS.exe 옆)에 배포되므로, 기본적으로 자기 exe 옆의 _file_* 폴더를 읽는다 — TLDS.exe가
/// 실시간으로 쓰고 있는 바로 그 파일들이다.
///
/// 개발 중(실시간 TLDS 설치가 없는 bin\에서 실행)에는 명령줄 <c>--data-root "C:\path\to\tlds"</c> 또는
/// 환경 변수 <c>TLDS_DATA_ROOT</c>로 루트를 실제 설치 폴더로 지정할 수 있다(App.OnStartup 참고).
///
/// 구조(실제 쌍룡역 TLDS 설치와 동일):
///   _file_log/{yyyy}/{MM}/{yyyy-MM-dd-HH}.db   — 시간당 파일 하나, 테이블 Level12(구버전: Level01)(Time,Idx,Type,Value0..11)
///                                                  = 실제 측정 텔레메트리(Type=0 행).
///   _file_db/SetValue.db                        — 테이블 Level01(Time,ObjectIndex,Type,Base_N/Caution_N/Warning_N×12)
///                                                  = 고정 임계값 설정, 장치당 한 행.
///   _file_db/Event-{yyyy-MM-dd}.db              — 하루당 파일 하나, 테이블 Event(...)와 Alarm(...).
///   _file_system/system.xml, index_{역}.xml, rack_{역}.xml — 역/궤도/랙 설정(StationConfigLoader).
///   _file_pics/                                 — 궤도 배선도 벡터 파일(아직 사용 안 함).
/// </summary>
public static class TldsDataPaths
{
    public const string RootEnvVar = "TLDS_DATA_ROOT";

    private static string? _rootOverride;

    /// <summary>_file_log/_file_db가 들어 있는 폴더 — 재정의하지 않으면 exe 폴더(클래스 요약 참고).</summary>
    public static string Root => _rootOverride ?? AppContext.BaseDirectory;

    /// <summary>데이터 루트를 다른 폴더(실제 TLDS 설치)로 지정한다. null/공백이면 무시한다.</summary>
    public static void SetRoot(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path)) _rootOverride = Path.GetFullPath(path.Trim().Trim('"'));
    }

    public static string FileLogRoot => Path.Combine(Root, "_file_log");
    public static string FileDbRoot => Path.Combine(Root, "_file_db");
    public static string FileSystemRoot => Path.Combine(Root, "_file_system");

    public static string HourLogFile(DateTime hour) =>
        Path.Combine(FileLogRoot, hour.ToString("yyyy"), hour.ToString("MM"), hour.ToString("yyyy-MM-dd-HH") + ".db");

    public static string EventDbFile(DateTime day) =>
        Path.Combine(FileDbRoot, $"Event-{day:yyyy-MM-dd}.db");

    public static string SetValueDbFile => Path.Combine(FileDbRoot, "SetValue.db");
}
