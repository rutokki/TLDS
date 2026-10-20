using System.Text;
using System.Windows;
using TLDSDashBoard.Services;

namespace TLDSDashBoard;

public partial class App : Application
{
    public App()
    {
        // 실제 TLDS 이벤트/알람 로그 DB(_file_db)는 DEVICE_NAME/CONTENTS를 UTF-8이 아니라 원시 CP949(한국어
        // 코드페이지 949) 바이트로 저장한다. 이 provider를 등록하지 않으면 .NET이 해당 코드페이지를 알지
        // 못한다. Services/EventLogRepository.cs 참고.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 데이터 루트 재정의(TldsDataPaths 참고): "--data-root <경로>"가 TLDS_DATA_ROOT보다 우선하며, 둘 다
        // 없으면 exe가 있는 폴더를 쓴다(일반 배포 형태: TLDS.exe 옆에 대시보드를 둠).
        // MainWindow(그리고 MainViewModel의 첫 DB 읽기)를 만드는 base.OnStartup보다 먼저 실행해야 한다.
        TldsDataPaths.SetRoot(Environment.GetEnvironmentVariable(TldsDataPaths.RootEnvVar));
        int i = Array.IndexOf(e.Args, "--data-root");
        if (i >= 0 && i + 1 < e.Args.Length) TldsDataPaths.SetRoot(e.Args[i + 1]);

        base.OnStartup(e);

        // 역/궤도 목록은 _file_system(system.xml → index_/rack_{역}.xml)에서 읽는다. 이게 없으면 장치를 DB Idx에
        // 매핑할 수 없으므로, 빈 필터로 대시보드를 여는 대신 무엇이 빠졌는지 정확히 알려주고 종료한다.
        // (App.xaml에 StartupUri가 없다 — 창은 성공했을 때만 만들어진다.)
        try
        {
            var config = ReferenceData.Current;
            if (config.Warnings.Count > 0)
                System.Diagnostics.Debug.WriteLine("[StationConfig] " + string.Join(" / ", config.Warnings));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"역/궤도 설정을 읽지 못했습니다.\n\n{ex.Message}\n\n" +
                $"TLDSDashBoard.exe를 TLDS 설치 폴더(_file_system 폴더가 있는 곳)에 두고 실행하세요.",
                "TLDS 대시보드", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
