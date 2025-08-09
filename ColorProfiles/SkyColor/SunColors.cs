namespace FancyLighting.ColorProfiles.SkyColor;

public class SunColors : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SunColors()
    {
        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - (6.0 + (50.0 / 60.0));
        var sunsetTime = noonTime + (6.0 + (50.0 / 60.0));

        var nightColor = new Vector3(1f, 0.7f, 0.35f);
        var sunriseSunsetColor = new Vector3(1f, 0.77f, 0.5f);
        var dayColor1 = new Vector3(1f, 0.85f, 0.67f);
        var dayColor2 = new Vector3(1f, 0.97f, 0.9f);
        var dayColor = new Vector3(1f, 1f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 0.501, nightColor),
            (sunriseTime - 0.5, nightColor),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.75, dayColor1),
            (sunriseTime + 1.5, dayColor2),
            (sunriseTime + 3.0, dayColor),
            (sunriseTime + 3.001, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 3.001, dayColor),
            (sunsetTime - 3.0, dayColor),
            (sunsetTime - 1.5, dayColor2),
            (sunsetTime - 0.75, dayColor1),
            (sunsetTime, sunriseSunsetColor),
            (sunsetTime + 0.5, nightColor),
            (sunsetTime + 0.501, nightColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
