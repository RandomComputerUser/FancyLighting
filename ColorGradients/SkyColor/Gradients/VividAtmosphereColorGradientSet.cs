namespace FancyLighting.ColorGradients.SkyColor.Gradients;

public class VividAtmosphereColorGradientSet : LoadableAtmosphereColorGradientSetBase
{
    private VividAtmosphereColorGradientSet()
    {
        var lowGradient = new BasicTimeBasedColorGradient(
            InterpolationMode.Cubic,
            useLinearColorSpace: true
        );
        var middleGradient = new BasicTimeBasedColorGradient(
            InterpolationMode.Cubic,
            useLinearColorSpace: true
        );
        var highGradient = new BasicTimeBasedColorGradient(
            InterpolationMode.Cubic,
            useLinearColorSpace: true
        );

        _lowGradient = lowGradient;
        _middleGradient = middleGradient;
        _highGradient = highGradient;

        var noonTime = 12.0;
        // Sunrise and sunset times are affected by resolution
        var sunriseTime = noonTime - 6.75;
        var sunsetTime = noonTime + 6.75;

        var nightColors = new AtmosphereColorSet(
            new(0.03f, 0.06f, 0.12f),
            new(0.02f, 0.04f, 0.10f),
            new(0.01f, 0.02f, 0.08f)
        );
        var nightColors2 = new AtmosphereColorSet(
            new(0.06f, 0.18f, 0.35f),
            new(0.05f, 0.10f, 0.30f),
            new(0.04f, 0.04f, 0.24f)
        );
        var nightColors1 = new AtmosphereColorSet(
            new(0.70f, 0.40f, 0.20f),
            new(0.40f, 0.33f, 0.55f),
            new(0.15f, 0.05f, 0.40f)
        );
        var sunriseSunsetColors = new AtmosphereColorSet(
            new(1.00f, 0.55f, 0.30f),
            new(0.70f, 0.60f, 0.80f),
            new(0.40f, 0.18f, 0.60f)
        );
        var dayColors1 = new AtmosphereColorSet(
            new(1.00f, 0.65f, 0.45f),
            new(0.65f, 0.55f, 0.85f),
            new(0.45f, 0.20f, 0.75f)
        );
        var dayColors2 = new AtmosphereColorSet(
            new(1.00f, 0.83f, 0.75f),
            new(0.60f, 0.55f, 0.90f),
            new(0.30f, 0.25f, 0.85f)
        );
        var dayColors = new AtmosphereColorSet(
            new(0.65f, 0.85f, 1.00f),
            new(0.40f, 0.60f, 0.95f),
            new(0.20f, 0.30f, 0.90f)
        );

        (double hour, AtmosphereColorSet color)[] colorSets =
        [
            (0.00, nightColors),
            (sunriseTime - 2.0, nightColors),
            (sunriseTime - 1.0, nightColors2),
            (sunriseTime - 0.5, nightColors1),
            (sunriseTime, sunriseSunsetColors),
            (sunriseTime + 0.5, dayColors1),
            (sunriseTime + 1.25, dayColors2),
            (sunriseTime + 3.0, dayColors),
            (noonTime, dayColors),
            (sunsetTime - 3.0, dayColors),
            (sunsetTime - 1.25, dayColors2),
            (sunsetTime - 0.5, dayColors1),
            (sunsetTime, sunriseSunsetColors),
            (sunsetTime + 0.5, nightColors1),
            (sunsetTime + 1.0, nightColors2),
            (sunsetTime + 2.0, nightColors),
        ];

        foreach (var (hour, colorSet) in colorSets)
        {
            lowGradient.AddColor(hour, colorSet.LowColor);
            middleGradient.AddColor(hour, colorSet.MiddleColor);
            highGradient.AddColor(hour, colorSet.HighColor);
        }
    }
}
