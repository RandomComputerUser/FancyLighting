namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyColorsHigh : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SkyColorsHigh()
    {
        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - 6.5;
        var sunsetTime = noonTime + 6.5;

        var nightColor = new Vector3(0.02f, 0.02f, 0.03f);
        var nightColor1 = new Vector3(0.03f, 0.06f, 0.12f);
        var sunriseSunsetColor = new Vector3(0.05f, 0.15f, 0.3f);
        var dayColor1 = new Vector3(0.06f, 0.25f, 0.55f);
        var dayColor2 = new Vector3(0.08f, 0.3f, 0.85f);
        var dayColor = new Vector3(0.1f, 0.35f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 1.801, nightColor),
            (sunriseTime - 1.8, nightColor),
            (sunriseTime - 0.9, nightColor1),
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
            (sunsetTime + 0.9, nightColor1),
            (sunsetTime + 1.8, nightColor),
            (sunsetTime + 1.801, nightColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
