namespace FancyLighting.ColorProfiles.SkyColor;

public class SkyColorsLow : ISimpleColorProfile
{
    private readonly SkyColorProfile _profile;

    public SkyColorsLow()
    {
        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - 6.75;
        var sunsetTime = noonTime + 6.75;

        var nightColor = new Vector3(0.05f, 0.05f, 0.08f);
        var nightColor2 = new Vector3(0.08f, 0.07f, 0.12f);
        var nightColor1 = new Vector3(0.11f, 0.1f, 0.17f);
        var sunriseSunsetColor = new Vector3(0.7f, 0.4f, 0.25f);
        var dayColor1 = new Vector3(0.8f, 0.65f, 0.5f);
        var dayColor2 = new Vector3(0.9f, 0.85f, 0.7f);
        var dayColor3 = new Vector3(0.7f, 0.825f, 0.9f);
        var dayColor = new Vector3(0.55f, 0.8f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 2.501, nightColor),
            (sunriseTime - 2.5, nightColor),
            (sunriseTime - 1.5, nightColor2),
            (sunriseTime - 1.0, nightColor1),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.6, dayColor1),
            (sunriseTime + 1.2, dayColor2),
            (sunriseTime + 1.6, dayColor3),
            (sunriseTime + 2.0, dayColor),
            (sunriseTime + 2.001, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 2.001, dayColor),
            (sunsetTime - 2.0, dayColor),
            (sunsetTime - 1.6, dayColor3),
            (sunsetTime - 1.2, dayColor2),
            (sunsetTime - 0.6, dayColor1),
            (sunsetTime, sunriseSunsetColor),
            (sunsetTime + 1.0, nightColor1),
            (sunsetTime + 1.5, nightColor2),
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
