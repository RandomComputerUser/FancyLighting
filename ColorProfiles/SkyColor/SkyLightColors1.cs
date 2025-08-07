namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyLightColors1 : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SkyLightColors1()
    {
        // v1.0.0 - current default sky colors

        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - 7.0;
        var sunsetTime = noonTime + 7.0;

        var nightColor = new Vector3(0.03f, 0.03f, 0.08f);
        var nightColor2 = new Vector3(0.1f, 0.09f, 0.15f);
        var nightColor1 = new Vector3(0.23f, 0.21f, 0.3f);
        var sunriseSunsetColor = new Vector3(0.38f, 0.36f, 0.42f);
        var dayColor1 = new Vector3(0.72f, 0.6f, 0.48f);
        var dayColor2 = new Vector3(0.98f, 0.9f, 0.84f);
        var dayColor = new Vector3(1f, 1f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 2.501, nightColor),
            (sunriseTime - 2.5, nightColor),
            (sunriseTime - 1.25, nightColor2),
            (sunriseTime - 0.5, nightColor1),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.5, dayColor1),
            (sunriseTime + 1.5, dayColor2),
            (sunriseTime + 3.0, dayColor),
            (sunriseTime + 3.001, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 3.001, dayColor),
            (sunsetTime - 3.0, dayColor),
            (sunsetTime - 1.5, dayColor2),
            (sunsetTime - 0.5, dayColor1),
            (sunsetTime, sunriseSunsetColor),
            (sunsetTime + 0.5, nightColor1),
            (sunsetTime + 1.25, nightColor2),
            (sunsetTime + 2.5, nightColor),
            (sunsetTime + 2.501, nightColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
