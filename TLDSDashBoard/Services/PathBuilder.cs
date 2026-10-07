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

    /// <summary>Reduces a window to at most <paramref name="maxPoints"/> points for rendering, using
    /// min/max-per-bucket decimation so spikes survive (unlike naive stride sampling). Used by pages that
    /// keep large windows (e.g. GraphDetailSearchViewModel's up-to-3600-sample full range) so that
    /// PathFromArray — and the WPF Path element parsing/rendering the result — isn't redone at full
    /// resolution on every mouse-move tick during pan/zoom, which is what caused visible lag on that page.
    /// The returned array's own min/max equal the source's, since every bucket's extremes are kept, so
    /// callers can safely run ComputeReferenceLines against the downsampled array too.</summary>
    public static double[] Downsample(IReadOnlyList<double> arr, int maxPoints)
    {
        int n = arr.Count;
        if (n <= maxPoints || maxPoints <= 1) return arr as double[] ?? arr.ToArray();

        int buckets = Math.Max(1, maxPoints / 2);
        var result = new double[buckets * 2];
        double bucketSize = n / (double)buckets;
        int outIdx = 0;
        for (int b = 0; b < buckets; b++)
        {
            int start = (int)(b * bucketSize);
            int end = b == buckets - 1 ? n : (int)((b + 1) * bucketSize);
            if (end <= start) end = start + 1;

            double min = arr[start], max = arr[start];
            for (int i = start + 1; i < end; i++)
            {
                double v = arr[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }
            result[outIdx++] = min;
            result[outIdx++] = max;
        }
        return result;
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

    /// <summary>Same shape as <see cref="ComputeReferenceLines(IReadOnlyList{double})"/> but for REAL
    /// threshold values (from ThresholdRepository/SetValue.db) instead of the data-derived placeholder
    /// guess above — the axis is expanded to fit whichever is wider, the data's own spread or the two
    /// threshold lines, so both are always visible together.</summary>
    public static (double CheckedValue, double MinimumValue, double AxisMin, double AxisMax) ComputeReferenceLines(
        IReadOnlyList<double> window, double checkedValue, double minimumValue)
    {
        double dataMin = window.Count > 0 ? window.Min() : 0;
        double dataMax = window.Count > 0 ? window.Max() : 0;
        double axisMin = Math.Min(dataMin, minimumValue);
        double axisMax = Math.Max(dataMax, checkedValue);
        if (axisMax <= axisMin) axisMax = axisMin + 1; // degenerate case: flat data exactly at a flat threshold
        return (checkedValue, minimumValue, axisMin, axisMax);
    }
}
