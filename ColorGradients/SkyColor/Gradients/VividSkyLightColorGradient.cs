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

        var nightColor = new Vector3(0.04f, 0.04f, 0.05f);
        var nightColor2 = new Vector3(0.12f, 0.10f, 0.16f);
        var nightColor1 = new Vector3(0.22f, 0.14f, 0.29f);
        var sunriseSunsetColor = new Vector3(0.55f, 0.38f, 0.32f);
        var dayColor1 = new Vector3(0.95f, 0.48f, 0.35f);
        var dayColor2 = new Vector3(0.98f, 0.67f, 0.58f);
        var dayColor = new Vector3(1.00f, 1.00f, 1.00f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, nightColor),
            (sunriseTime - 2.0, nightColor),
            (sunriseTime - 1.0, nightColor2),
            (sunriseTime - 0.5, nightColor1),
            (sunriseTime, sunriseSunsetColor),
            (sunriseTime + 0.5, dayColor1),
            (sunriseTime + 1.0, dayColor2),
            (sunriseTime + 3.0, dayColor),
            (noonTime, dayColor),
            (sunsetTime - 3.0, dayColor),
            (sunsetTime - 1.0, dayColor2),
            (sunsetTime - 0.5, dayColor1),
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
