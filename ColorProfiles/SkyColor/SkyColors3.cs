﻿namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyColors3 : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SkyColors3()
    {
        // v0.6.0 default sky colors

        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - 7.0;
        var sunsetTime = noonTime + 7.0;

        var midnightColor = new Vector3(8, 10, 15) / 255f;
        var twilightColor1 = new Vector3(10, 13, 18) / 255f;
        var twilightColor2 = new Vector3(60, 35, 23) / 255f;
        var sunrisesetColor = new Vector3(150, 70, 32) / 255f;
        var goldenHourColor1 = new Vector3(250, 130, 55) / 255f;
        var goldenHourColor2 = new Vector3(300, 230, 170) / 255f;
        var noonColor = new Vector3(360, 460, 560) / 255f;

        (double hour, Vector3 color)[] colors =
        [
            (0.0, midnightColor),
            (sunriseTime - 1.5, twilightColor1),
            (sunriseTime - 0.5, twilightColor2),
            (sunriseTime, sunrisesetColor),
            (sunriseTime + 0.75, goldenHourColor1),
            (sunriseTime + 1.5, goldenHourColor2),
            (noonTime, noonColor),
            (sunsetTime - 1.5, goldenHourColor2),
            (sunsetTime - 0.75, goldenHourColor1),
            (sunsetTime, sunrisesetColor),
            (sunsetTime + 0.5, twilightColor2),
            (sunsetTime + 1.5, twilightColor1),
        ];

        foreach (var (hour, color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
