namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyLightColors2 : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SkyLightColors2()
    {
        // v0.8.5 - v0.9.20 default sky colors

        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - (7 + (1.0 / 3.0));
        var sunsetTime = noonTime + (7 + (1.0 / 3.0));

        var nightColor = new Vector3(0f, 0f, 0.06f);
        var nightColor1 = new Vector3(0.07f, 0.05f, 0.13f);
        var sunriseSunsetColor = new Vector3(0.2f, 0.13f, 0.19f);
        var dayColor1 = new Vector3(0.91f, 0.35f, 0.3f);
        var dayColor2 = new Vector3(0.98f, 0.77f, 0.55f);
        var dayColor = new Vector3(1f, 1f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 2.001, nightColor),
            (sunriseTime - 2.0, nightColor),
            (sunriseTime - 0.75, nightColor1),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 1.0, dayColor1),
            (sunriseTime + 2.0, dayColor2),
            (sunriseTime + 3.0, dayColor),
            (sunriseTime + 3.001, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 3.001, dayColor),
            (sunsetTime - 3.0, dayColor),
            (sunsetTime - 2.0, dayColor2),
            (sunsetTime - 1.0, dayColor1),
            (sunsetTime, sunriseSunsetColor),
            (sunsetTime + 0.75, nightColor1),
            (sunsetTime + 2.0, nightColor),
            (sunsetTime + 2.001, nightColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
