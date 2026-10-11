namespace FancyLighting.ColorGradients.SkyColor.Gradients;

public sealed class VividSunColorGradient : LoadableTimeBasedColorGradientBase
{
    private readonly BasicTimeBasedColorGradient _gradient;

    private VividSunColorGradient()
    {
        _gradient = new(InterpolationMode.Cubic, useLinearColorSpace: true);

        var noonTime = 12.0;
        // Sunrise and sunset times are affected by resolution
        var sunriseTime = noonTime - 6.75;
        var sunsetTime = noonTime + 6.75;

        var nightColor = new Vector3(1f, 0.75f, 0.45f);
        var sunriseSunsetColor = new Vector3(1f, 0.80f, 0.55f);
        var dayColor1 = new Vector3(1f, 0.85f, 0.70f);
        var dayColor2 = new Vector3(1f, 0.90f, 0.80f);
        var dayColor = new Vector3(1f, 1f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 0.5, nightColor),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.5, dayColor1),
            (sunriseTime + 1.25, dayColor2),
            (sunriseTime + 3.0, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 3.0, dayColor),
            (sunsetTime - 1.25, dayColor2),
            (sunsetTime - 0.5, dayColor1),
            (sunsetTime, sunriseSunsetColor),
            (sunsetTime + 0.5, nightColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _gradient.AddColor(hour, color);
        }
    }

    public override Vector3 GetColor(double hour) => _gradient.GetColor(hour);
}
