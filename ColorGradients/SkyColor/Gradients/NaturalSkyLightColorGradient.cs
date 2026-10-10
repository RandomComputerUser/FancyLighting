namespace FancyLighting.ColorGradients.SkyColor.Gradients;

public sealed class NaturalSkyLightColorGradient : LoadableTimeBasedColorGradientBase
{
    private readonly BasicTimeBasedColorGradient _gradient;

    private NaturalSkyLightColorGradient()
    {
        _gradient = new(InterpolationMode.Cubic, useLinearColorSpace: true);

        var noonTime = 12.0;
        // Sunrise and sunset times are affected by resolution
        var sunriseTime = noonTime - 7.0;
        var sunsetTime = noonTime + 7.0;

        var nightColor = new Vector3(0.04f, 0.04f, 0.05f);
        var nightColor2 = new Vector3(0.11f, 0.10f, 0.16f);
        var nightColor1 = new Vector3(0.18f, 0.16f, 0.28f);
        var sunriseSunsetColor = new Vector3(0.49f, 0.41f, 0.32f);
        var dayColor1 = new Vector3(0.95f, 0.75f, 0.40f);
        var dayColor2 = new Vector3(0.98f, 0.89f, 0.71f);
        var dayColor = new Vector3(1.00f, 1.00f, 1.00f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 2.0, nightColor),
            (sunriseTime - 1.0, nightColor2),
            (sunriseTime - 0.5, nightColor1),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.75, dayColor1),
            (sunriseTime + 1.5, dayColor2),
            (sunriseTime + 3.0, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 3.0, dayColor),
            (sunsetTime - 1.5, dayColor2),
            (sunsetTime - 0.75, dayColor1),
            (sunsetTime, sunriseSunsetColor),
            (sunsetTime + 0.5, nightColor1),
            (sunsetTime + 1.0, nightColor2),
            (sunsetTime + 2.0, nightColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _gradient.AddColor(hour, color);
        }
    }

    public override Vector3 GetColor(double hour) => _gradient.GetColor(hour);
}
