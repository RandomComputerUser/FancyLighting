using System.Runtime.CompilerServices;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float x, float y, float t) => ((1 - t) * x) + (t * y);

    public static double Smoothstep(double edge1, double edge2, double x)
    {
        x = (x - edge1) / (edge2 - edge1);

        if (x <= 0.0)
        {
            return 0.0;
        }

        if (x >= 1.0)
        {
            return 1.0;
        }

        return x * x * (3.0 - (2.0 * x));
    }

    public static double EuclideanRemainder(double x, double y)
    {
        var result = x % y;
        if (result < 0.0)
        {
            result += Math.Abs(y);
        }

        return result;
    }
}
