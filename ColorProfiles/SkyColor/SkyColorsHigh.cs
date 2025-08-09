namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyColorsHigh : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SkyColorsHigh()
    {
        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - (6.0 + (50.0 / 60.0));
        var sunsetTime = noonTime + (6.0 + (50.0 / 60.0));

        var nightColor = new Vector3(0.02f, 0.02f, 0.04f);
        var nightColor2 = new Vector3(0.04f, 0.05f, 0.08f);
        var nightColor1 = new Vector3(0.06f, 0.09f, 0.23f);
        var sunriseSunsetColor = new Vector3(0.07f, 0.14f, 0.36f);
        var dayColor1 = new Vector3(0.08f, 0.25f, 0.65f);
        var dayColor2 = new Vector3(0.09f, 0.32f, 0.9f);
        var dayColor = new Vector3(0.1f, 0.35f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 2.501, nightColor),
            (sunriseTime - 2.5, nightColor),
            (sunriseTime - 1.25, nightColor2),
            (sunriseTime - 0.5, nightColor1),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.75, dayColor1),
            (sunriseTime + 2.0, dayColor2),
            (sunriseTime + 3.0, dayColor),
            (sunriseTime + 3.001, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 3.001, dayColor),
            (sunsetTime - 3.0, dayColor),
            (sunsetTime - 2.0, dayColor2),
            (sunsetTime - 0.75, dayColor1),
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
