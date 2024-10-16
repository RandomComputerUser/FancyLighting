﻿using Microsoft.Xna.Framework;

namespace FancyLighting.Profiles.SkyColor;

public class SkyColors3 : ISimpleColorProfile
{
    private SkyColorProfile _profile;

    public SkyColors3()
    {
        // 0.4.0 - 0.5.5 sky colors

        _profile = new(InterpolationMode.Linear);

        var midnightTime = 0.0;
        var noonTime = 12.0;
        var dayEndTime = 24.0;
        var sunriseTime = 5.0;
        var preSunriseTime = sunriseTime - 0.5;
        var postSunriseTimeEarly = sunriseTime + 0.5;
        var postSunriseTimeLate = sunriseTime + 1.0;
        var sunsetTime = 12.0 + 7.0;
        var preSunsetTimeEarly = sunsetTime - 1.0;
        var preSunsetTimeLate = sunsetTime - 0.5;
        var postSunsetTime = sunsetTime + 0.5;

        Vector3 midnightColor = new Vector3(10, 10, 20) / 255f;
        Vector3 nightTransitionColor = new Vector3(45, 30, 40) / 255f;
        Vector3 dayNightTransitionColor = new Vector3(150, 60, 70) / 255f;
        Vector3 dayTransitionColor = new Vector3(200, 120, 90) / 255f;
        Vector3 dayTransitionColor2 = new Vector3(230, 180, 140) / 255f;
        Vector3 noonColor = new Vector3(360, 360, 360) / 255f;

        (double hour, Vector3 color)[] colors = new[]
        {
            (midnightTime, midnightColor),
            (preSunriseTime, nightTransitionColor),
            (sunriseTime, dayNightTransitionColor),
            (postSunriseTimeEarly, dayTransitionColor),
            (postSunriseTimeLate, dayTransitionColor2),
            (noonTime, noonColor),
            (preSunsetTimeEarly, dayTransitionColor2),
            (preSunsetTimeLate, dayTransitionColor),
            (sunsetTime, dayNightTransitionColor),
            (postSunsetTime, nightTransitionColor),
            (dayEndTime, midnightColor),
        };

        foreach ((var hour, var color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
