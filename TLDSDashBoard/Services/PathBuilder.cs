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

    /// <summary>A flat reference line at one value (점검/최소 threshold overlays) — uses the SAME min/max
    /// scale as the data path it's drawn alongside, so it lines up with the real data instead of floating
    /// at an unrelated position.</summary>
    public static string HorizontalLinePath(double value, double min, double max, double w, double h, double padTop, double padBottom)
    {
        double usableH = h - padTop - padBottom;
        double range = max - min;
        if (range == 0) range = 1;
        double y = padTop + (1 - (value - min) / range) * usableH;
        string yStr = y.ToString("0.##", CultureInfo.InvariantCulture);
        return $"M0,{yStr} L{w.ToString("0.##", CultureInfo.InvariantCulture)},{yStr}";
    }

    /// <summary>점검(check)/최소(minimum) reference thresholds for one window of samples — no real spec
    /// table exists yet, so these are placeholder margins derived from the window's own spread (flat-line
    /// fallback so the lines don't collapse onto the data when it isn't moving at all). Shared by every
    /// chart that overlays these lines (Overview lanes, drill-in detail view, 그래프 상세검색) so they all
    /// agree on the same thresholds for the same data.</summary>
    public static (double CheckedValue, double MinimumValue, double AxisMin, double AxisMax) ComputeReferenceLines(IReadOnlyList<double> window)
    {
        double dataMin = window.Count > 0 ? window.Min() : 0;
        double dataMax = window.Count > 0 ? window.Max() : 0;
        double margin = (dataMax - dataMin) * 0.15;
        if (margin <= 0) margin = Math.Abs(dataMax) * 0.05 + 0.5;
        double checkedValue = dataMax + margin;
        double minimumValue = dataMin - margin;
        double axisMin = Math.Min(dataMin, minimumValue);
        double axisMax = Math.Max(dataMax, checkedValue);
        return (checkedValue, minimumValue, axisMin, axisMax);
    }
}
