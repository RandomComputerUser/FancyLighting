namespace FancyLighting.ColorGradients.SkyColor.Gradients;

public sealed class RedGoldenHourSunColorGradient : LoadableTimeBasedColorGradientBase
{
    private readonly BasicTimeBasedColorGradient _gradient;

    private RedGoldenHourSunColorGradient()
    {
        _gradient = new(InterpolationMode.Cubic, useLinearColorSpace: true);

        var noonTime = 12.0;
        // Sunrise and sunset times are affected by resolution
        var sunriseTime = noonTime - 7.0;
        var sunsetTime = noonTime + 7.0;

        var nightColor = new Vector3(1f, 0.77f, 0.45f);
        var sunriseSunsetColor = new Vector3(1f, 0.84f, 0.62f);
        var dayColor1 = new Vector3(1f, 0.91f, 0.76f);
        var dayColor2 = new Vector3(1f, 0.97f, 0.93f);
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
            _gradient.AddColor(hour, color);
        }
    }

    public override Vector3 GetColor(double hour) => _gradient.GetColor(hour);
}
