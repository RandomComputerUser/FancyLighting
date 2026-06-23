namespace FancyLighting;

public static class FancySkyLighting
{
    internal static Rectangle _lightMapArea;
    internal static float[] _skyLightLuma;

    internal static void Unload()
    {
        _skyLightLuma = null;
    }

    internal static void SetLightMapArea(Rectangle area)
    {
        _lightMapArea = area;
        var length = area.Width * area.Height;
        ArrayUtils.MakeAtLeastSize(ref _skyLightLuma, length);
    }

    public static void SetSkyLightLuma(int x, int y, float luma)
    {
        var row = y - _lightMapArea.Y;
        var col = x - _lightMapArea.X;

        if (
            row < 0
            || row >= _lightMapArea.Height
            || col < 0
            || col >= _lightMapArea.Width
        )
        {
            return;
        }

        var index = (_lightMapArea.Height * col) + row;
        _skyLightLuma[index] = luma;
    }

    public static (double angle, double mult) CalculateSkyLightAngleAndMultiplier(
        double hour
    )
    {
        hour = MathUtils.EuclideanRemainder(hour, 24.0);

        var diffFromNoon = hour - 12.0;
        var diffFromMidnight = hour < 12.0 ? hour : hour - 24.0;

        const double SunsetOffset = 6.0 + (50.0 / 60.0);
        const double MoonsetOffset = 4.0 + (21.0 / 60.0);

        double progress;
        double amountVisible;
        double baseMult;
        if (Math.Abs(diffFromNoon) < SunsetOffset)
        {
            // sun is out
            progress = (diffFromNoon + SunsetOffset) / (2.0 * SunsetOffset);
            amountVisible = (SunsetOffset - Math.Abs(diffFromNoon)) / (16.0 / 60.0);
            baseMult = 2.0;
        }
        else if (Math.Abs(diffFromMidnight) < MoonsetOffset)
        {
            // moon is out
            progress = (diffFromMidnight + MoonsetOffset) / (2.0 * MoonsetOffset);
            amountVisible = (MoonsetOffset - Math.Abs(diffFromMidnight)) / (9.0 / 60.0);
            baseMult = 1.0;
        }
        else
        {
            return (angle: 0.0, mult: 0.0);
        }

        amountVisible *= 0.25; // ease the transition

        var angle = Math.PI * progress;
        var mult = baseMult * MathUtils.Smoothstep(0.0, 1.0, amountVisible);
        return (angle, mult);
    }
}
