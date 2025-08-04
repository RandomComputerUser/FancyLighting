namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyLightColors1 : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SkyLightColors1()
    {
        // v1.0.0 - current default sky colors

        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - 6.75;
        var sunsetTime = noonTime + 6.75;

        var nightColor = new Vector3(0.03f, 0.03f, 0.08f);
        var nightColor2 = new Vector3(0.14f, 0.12f, 0.17f);
        var nightColor1 = new Vector3(0.29f, 0.2f, 0.25f);
        var sunriseSunsetColor = new Vector3(0.6f, 0.4f, 0.32f);
        var dayColor1 = new Vector3(0.76f, 0.58f, 0.46f);
        var dayColor2 = new Vector3(0.98f, 0.8f, 0.66f);
        var dayColor = new Vector3(1f, 1f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 2.251, nightColor),
            (sunriseTime - 2.25, nightColor),
            (sunriseTime - 1.0, nightColor2),
            (sunriseTime - 0.5, nightColor1),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.6, dayColor1),
            (sunriseTime + 1.2, dayColor2),
            (sunriseTime + 2.0, dayColor),
            (sunriseTime + 2.001, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 2.001, dayColor),
            (sunsetTime - 2.0, dayColor),
            (sunsetTime - 1.2, dayColor2),
            (sunsetTime - 0.6, dayColor1),
            (sunsetTime, sunriseSunsetColor),
            (sunsetTime + 0.5, nightColor1),
            (sunsetTime + 1.0, nightColor2),
            (sunsetTime + 2.25, nightColor),
            (sunsetTime + 2.251, nightColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
