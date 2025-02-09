using System;

namespace FancyLighting.Utils;

internal static class MathUtils
{
    public static double Hypot(double x, double y)
    {
        x = Math.Abs(x);
        y = Math.Abs(y);

        if (x == 0.0)
        {
            return y;
        }

        if (y == 0.0)
        {
            return x;
        }

        var big = Math.Max(x, y);
        var small = Math.Min(x, y);

        var ratio = small / big;

        return big * Math.Sqrt(1.0 + (ratio * ratio));
    }

    public static float Lerp(float x, float y, float t) => ((1 - t) * x) + (t * y);
}
