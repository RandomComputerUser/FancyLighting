namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyColorProfile : ISimpleColorProfile
{
    protected List<(double hour, Vector3 color)> _colors;
    protected InterpolationMode _interpolationMode;

    public SkyColorProfile(InterpolationMode interpolationMode)
    {
        _colors = new();
        _interpolationMode = interpolationMode;
    }

    public virtual void AddColor(double hour, Vector3 color) =>
        _colors.Add((hour, color));

    protected (double hour, Vector3 color) HourColorAtIndex(int index)
    {
        if (index < 0)
        {
            var (hour, color) = _colors[index + _colors.Count];
            return (hour - 24.0, color);
        }

        if (index >= _colors.Count)
        {
            var (hour, color) = _colors[index - _colors.Count];
            return (hour + 24.0, color);
        }

        return _colors[index];
    }

    private static Vector3 ChooseDerivative(
        double x1,
        double x2,
        double x3,
        Vector3 y1,
        Vector3 y2,
        Vector3 y3
    )
    {
        var baseSlope = (y3 - y1) / (float)(x3 - x1);
        var slope1 = 2 * (y2 - y1) / (float)(x2 - x1);
        var slope2 = 2 * (y3 - y2) / (float)(x3 - x2);

        return new(
            Derivative(baseSlope.X, slope1.X, slope2.X),
            Derivative(baseSlope.Y, slope1.Y, slope2.Y),
            Derivative(baseSlope.Z, slope1.Z, slope2.Z)
        );

        static float Derivative(float baseSlope, float slope1, float slope2)
        {
            var minSlope = 0f;
            var maxSlope = 0f;

            if (slope1 > 0f)
            {
                if (slope2 > 0f)
                {
                    maxSlope = MathF.Min(slope1, slope2);
                }
                else if (slope2 < 0f)
                {
                    minSlope = slope2;
                    maxSlope = slope1;
                }
            }
            else if (slope1 < 0f)
            {
                if (slope2 > 0f)
                {
                    minSlope = slope1;
                    maxSlope = slope2;
                }
                else if (slope2 < 0f)
                {
                    minSlope = MathF.Max(slope1, slope2);
                }
            }

            return MathHelper.Clamp(baseSlope, minSlope, maxSlope);
        }
    }

    public virtual Vector3 GetColor(double hour)
    {
        hour %= 24.0;

        var color = _colors[^1].color;
        for (var i = 1; i <= _colors.Count; ++i)
        {
            if (hour > HourColorAtIndex(i).hour)
            {
                continue;
            }

            var (hour0, color0) = HourColorAtIndex(i - 2);
            var (hour1, color1) = HourColorAtIndex(i - 1);
            var (hour2, color2) = HourColorAtIndex(i);
            var (hour3, color3) = HourColorAtIndex(i + 1);

            var diff = (float)(hour2 - hour1);
            var t = (float)(hour - hour1) / diff;

            switch (_interpolationMode)
            {
                case InterpolationMode.Linear:
                {
                    // Linear interpolation

                    color = Vector3.Lerp(color1, color2, t);
                    break;
                }

                case InterpolationMode.Cubic:
                default:
                {
                    // Cubic Hermite spline interpolation

                    var m1 =
                        ChooseDerivative(hour0, hour1, hour2, color0, color1, color2)
                        * diff;
                    var m2 =
                        ChooseDerivative(hour1, hour2, hour3, color1, color2, color3)
                        * diff;

                    var t2 = t * t;
                    var t3 = t2 * t;

                    color =
                        (((2 * t3) - (3 * t2) + 1) * color1)
                        + ((t3 - (2 * t2) + t) * m1)
                        + (((-2 * t3) + (3 * t2)) * color2)
                        + ((t3 - t2) * m2);
                    break;
                }
            }

            break;
        }

        return Vector3.Clamp(color, Vector3.Zero, Vector3.One);
    }
}
