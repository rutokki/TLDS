using System.Globalization;
using System.Text;

namespace TLDSDashBoard.Services;

/// <summary>
/// 데이터 배열로 WPF Path의 "M x,y L x,y ..." 미니 언어 문자열을 만든다. 원래 HTML 프로토타입이 샘플
/// 배열을 SVG path의 `d` 속성으로 바꾸는 방식과 같다. WPF의 경로 마크업 구문이 SVG의 M/L 명령과
/// 호환되는 상위 집합이므로, 결과 문자열을 &lt;Path Data="..."/&gt;에 (StreamGeometry/Geometry 변환기를
/// 거쳐) 바로 넣을 수 있다.
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

    /// <summary>렌더링용으로 창(window)을 최대 <paramref name="maxPoints"/>개 점으로 줄인다. 구간별
    /// 최소/최대 샘플링을 써서 스파이크가 사라지지 않는다(단순 간격 추출과 다름). 큰 창을 쓰는 페이지(예:
    /// GraphDetailSearchViewModel의 최대 3600샘플 전체 범위)에서, PathFromArray와 그 결과를 파싱/렌더링하는
    /// WPF Path 요소가 팬/줌 중 마우스를 움직일 때마다 전체 해상도로 다시 계산되지 않게 하려는 것이다 —
    /// 그 페이지에서 눈에 띄는 지연이 생긴 원인이 이것이었다. 반환 배열의 최소/최대는 원본과 같다
    /// (모든 구간의 극값을 유지하므로) — 그래서 호출자는 ComputeReferenceLines도 다운샘플링된 배열에
    /// 안전하게 쓸 수 있다.</summary>
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

    /// <summary>최소/최대를 직접 구하는 편의 오버로드(메인 차트에 쓰던 것과 같은 ±2 패딩 적용).</summary>
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

    /// <summary>한 값에 그리는 평평한 기준선(점검/최소 임계값 오버레이) — 함께 그려지는 데이터 경로와
    /// 같은 최소/최대 스케일을 쓰므로, 엉뚱한 위치에 떠 있지 않고 실제 데이터와 정렬된다.</summary>
    public static string HorizontalLinePath(double value, double min, double max, double w, double h, double padTop, double padBottom)
    {
        double usableH = h - padTop - padBottom;
        double range = max - min;
        if (range == 0) range = 1;
        double y = padTop + (1 - (value - min) / range) * usableH;
        string yStr = y.ToString("0.##", CultureInfo.InvariantCulture);
        return $"M0,{yStr} L{w.ToString("0.##", CultureInfo.InvariantCulture)},{yStr}";
    }

    /// <summary>샘플 한 창에 대한 점검(check)/최소(minimum) 기준 임계값 — 실제 규격 표가 아직 없어서 창
    /// 자체의 편차에서 뽑은 임시 마진이다(데이터가 전혀 움직이지 않을 때 선이 데이터 위에 겹치지 않도록
    /// 평평한 선 폴백 포함). 이 선을 덧그리는 모든 차트(Overview 레인, 드릴인 상세 보기, 그래프 상세검색)가
    /// 공유하므로 같은 데이터에 대해 모두 같은 임계값을 쓴다.</summary>
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

    /// <summary><see cref="ComputeReferenceLines(IReadOnlyList{double})"/>와 같은 형태이지만 데이터에서 뽑은
    /// 위의 임시 추정값 대신 실제 임계값(ThresholdRepository/SetValue.db)을 쓴다 — 축은 데이터 자체의 편차와
    /// 두 임계선 중 더 넓은 쪽에 맞게 확장되어, 둘 다 항상 함께 보인다.</summary>
    public static (double CheckedValue, double MinimumValue, double AxisMin, double AxisMax) ComputeReferenceLines(
        IReadOnlyList<double> window, double checkedValue, double minimumValue)
    {
        double dataMin = window.Count > 0 ? window.Min() : 0;
        double dataMax = window.Count > 0 ? window.Max() : 0;
        double axisMin = Math.Min(dataMin, minimumValue);
        double axisMax = Math.Max(dataMax, checkedValue);
        if (axisMax <= axisMin) axisMax = axisMin + 1; // 퇴화 경우: 평평한 데이터가 평평한 임계값과 정확히 같음
        return (checkedValue, minimumValue, axisMin, axisMax);
    }
}
