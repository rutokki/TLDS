using System.Text;
using System.Windows;
using TLDSDashBoard.Services;

namespace TLDSDashBoard;

public partial class App : Application
{
    public App()
    {
        // The real TLDS event/alarm log DBs (_file_db) store DEVICE_NAME/CONTENTS as raw CP949 (Korean
        // codepage 949) bytes, not UTF-8 — .NET doesn't know that codepage unless this provider is
        // registered. See Services/EventLogRepository.cs.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Data root override (see TldsDataPaths): "--data-root <path>" wins over TLDS_DATA_ROOT; with
        // neither, the exe's own folder is used (the normal deployment: dashboard next to TLDS.exe).
        // Must run before base.OnStartup, which creates MainWindow (and so MainViewModel's first DB read).
        TldsDataPaths.SetRoot(Environment.GetEnvironmentVariable(TldsDataPaths.RootEnvVar));
        int i = Array.IndexOf(e.Args, "--data-root");
        if (i >= 0 && i + 1 < e.Args.Length) TldsDataPaths.SetRoot(e.Args[i + 1]);

        base.OnStartup(e);

        // Station/track list comes from _file_system (system.xml → index_/rack_{역}.xml). Without it no
        // device can be mapped to its DB Idx, so say exactly what's missing and exit instead of opening a
        // dashboard with empty filters. (No StartupUri in App.xaml — the window is only created on success.)
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
