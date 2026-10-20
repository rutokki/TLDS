using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>모든 페이지의 "CSV 저장" 버튼이 공유하는 구현 — 기본 저장 대화상자를 띄우고,
/// BOM이 있는 UTF-8로 쓰며(없으면 Excel이 한글을 깨진 글자로 연다), 쉼표/따옴표/줄바꿈이 들어간
/// 필드는 따옴표로 감싸 이스케이프한다.</summary>
public static class CsvExporter
{
    public static readonly string[] MeasurementSuffixes = { "표준", "측정", "점검", "최소" };

    /// <summary>저장 대화상자를 띄우고 헤더 + 행을 CSV로 쓴다. 사용자가 대화상자를 취소했거나 쓰기가
    /// 실패했으면(예: Excel에서 이미 파일을 열어 둠) null을 반환한다 — 어느 쪽이든 호출한 쪽은 성공했다고
    /// 알리지 말고 상태 메시지를 그대로 둬야 한다. 쓰기 실패는 예외를 던지는 대신 여기서(저장 대화상자와
    /// 같은 곳) 자체 메시지 상자를 띄운다. 잠긴 파일의 IOException을 처리하지 않으면 앱이 죽기 때문이다.</summary>
    public static string? Export(string suggestedFileName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "CSV 파일 (*.csv)|*.csv",
            DefaultExt = ".csv",
        };
        if (dialog.ShowDialog() != true) return null;

        try
        {
            using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            writer.WriteLine(string.Join(",", headers.Select(Escape)));
            foreach (var row in rows)
            {
                writer.WriteLine(string.Join(",", row.Select(Escape)));
            }
            return dialog.FileName;
        }
        catch (IOException)
        {
            MessageBox.Show(
                $"'{Path.GetFileName(dialog.FileName)}' 파일이 다른 프로그램에서 열려 있어 저장할 수 없습니다.\n해당 파일을 닫은 후 다시 시도해주세요.",
                "CSV 저장 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"'{Path.GetFileName(dialog.FileName)}' 위치에 쓸 권한이 없습니다. 다른 폴더에 저장해보세요.",
                "CSV 저장 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    /// <summary>표준/측정/점검/최소를 CSV 필드 4개로, <see cref="MeasurementSuffixes"/>와 같은 순서로 —
    /// 궤도 장애·경보와 레벨 기록 그리드 전반에 반복되는 표준/측정/점검/최소 묶음.</summary>
    public static IEnumerable<string> FlattenChannel(ChannelMeasurement c)
    {
        return new[]
    {
        c.Standard.ToString("F2", CultureInfo.InvariantCulture),
        c.Measured?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
        c.Checked.ToString("F2", CultureInfo.InvariantCulture),
        c.Minimum.ToString("F2", CultureInfo.InvariantCulture),
    };
    }

    private static string Escape(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
