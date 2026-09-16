using System.Globalization;
using System.Text;

namespace TLDSDashBoard.Services;

/// <summary>
/// Builds a WPF Path "M x,y L x,y ..." mini-language string from a data array, the same way the
/// original HTML prototype turns a sample array into an SVG path `d` attribute. Because WPF's path
/// markup syntax is a superset compatible with SVG's M/L commands, the resulting string can be
/// assigned directly to a &lt;Path Data="..."/&gt; (via a StreamGeometry/Geometry converter).
/// </summary>
public static class PathBuilder
{
    public static string PathFromArray(IReadOnlyList<double> arr, double min, double max, double w, double h, double padTop, double padBottom)
    {
        int n = arr.Count;
        double usableH = h - padTop - padBottom;
        double range = max - min;
        if (range == 0) range = 1;

        var sb = new StringBuilder(n * 12);
        for (int i = 0; i < n; i++)
        {
            double x = n == 1 ? 0 : (i / (double)(n - 1)) * w;
            double y = padTop + (1 - (arr[i] - min) / range) * usableH;
            sb.Append(i == 0 ? 'M' : 'L');
            sb.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append(' ');
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Convenience overload that derives min/max (with the same +/-2 padding used for the main chart).</summary>
    public static string PathFromArrayAutoRange(IReadOnlyList<double> arr, double w, double h, double padTop, double padBottom, double pad = 0)
    {
        double min = arr.Count == 0 ? 0 : arr[0];
        double max = min;
        foreach (var v in arr)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return PathFromArray(arr, min - pad, max + pad, w, h, padTop, padBottom);
    }
}
