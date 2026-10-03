namespace FancyLighting.ColorGradients.SkyColor.Gradients;

public sealed class VividSkyLightColorGradient : LoadableTimeBasedColorGradientBase
{
    private readonly BasicTimeBasedColorGradient _gradient;

    private VividSkyLightColorGradient()
    {
        _gradient = new(InterpolationMode.Cubic, useLinearColorSpace: true);

        var noonTime = 12.0;
        // Sunrise and sunset times are affected by resolution
        var sunriseTime = noonTime - 7.0;
        var sunsetTime = noonTime + 7.0;

        var nightColor = new Vector3(0.01f, 0.01f, 0.01f);
        var sunriseSunsetColor = new Vector3(1f, 0.01f, 0.01f);
        var dayColor = new Vector3(1f, 1f, 1f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime, sunriseSunsetColor),
            (noonTime, dayColor),
            (sunsetTime, sunriseSunsetColor),
        ];

        foreach (var (hour, color) in colors)
        {
            _gradient.AddColor(hour, color);
        }
    }

    public override Vector3 GetColor(double hour) => _gradient.GetColor(hour);
}
