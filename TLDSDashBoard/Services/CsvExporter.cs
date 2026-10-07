using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>Shared "CSV 저장" implementation for every page's export button — shows a native Save dialog,
/// writes UTF-8 with a BOM (without it, Excel opens Korean text as garbled), and quotes/escapes any
/// field containing a comma, quote, or newline.</summary>
public static class CsvExporter
{
    public static readonly string[] MeasurementSuffixes = { "표준", "측정", "점검", "최소" };

    /// <summary>Shows a Save dialog and writes the header + rows as CSV. Returns null if the user
    /// canceled the dialog, or if the write failed (e.g. the file is already open in Excel) — either way
    /// callers should leave their status message alone rather than claim success. A failed write shows
    /// its own message box here (same place the Save dialog itself lives) instead of throwing, since an
    /// unhandled IOException from a locked file would otherwise crash the app.</summary>
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

    /// <summary>Standard/Measured/Checked/Minimum as 4 CSV fields, in the same order as
    /// <see cref="MeasurementSuffixes"/> — the recurring 표준/측정/점검/최소 group used throughout the
    /// 궤도 장애·경보 and 레벨 기록 grids.</summary>
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
