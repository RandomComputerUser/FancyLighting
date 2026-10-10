namespace FancyLighting.ColorGradients.SkyColor.Gradients;

public class NaturalAtmosphereColorGradientSet : LoadableAtmosphereColorGradientSetBase
{
    private NaturalAtmosphereColorGradientSet()
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
        var sunriseTime = noonTime - 7.0;
        var sunsetTime = noonTime + 7.0;

        var nightColors = new AtmosphereColorSet(
            new(0.01f, 0.01f, 0.01f),
            new(0.01f, 0.01f, 0.01f),
            new(0.01f, 0.01f, 0.01f)
        );
        var nightColors2 = new AtmosphereColorSet(
            new(0.01f, 0.01f, 0.01f),
            new(0.01f, 0.01f, 0.01f),
            new(0.01f, 0.01f, 0.01f)
        );
        var nightColors1 = new AtmosphereColorSet(
            new(0.01f, 0.01f, 0.01f),
            new(0.01f, 0.01f, 0.01f),
            new(0.01f, 0.01f, 0.01f)
        );
        var sunriseSunsetColors = new AtmosphereColorSet(
            new(1.00f, 0.70f, 0.30f),
            new(0.50f, 0.60f, 0.70f),
            new(0.15f, 0.25f, 0.50f)
        );
        var dayColors1 = new AtmosphereColorSet(
            new(1.00f, 0.90f, 0.60f),
            new(0.40f, 0.70f, 0.85f),
            new(0.20f, 0.30f, 0.65f)
        );
        var dayColors2 = new AtmosphereColorSet(
            new(1.00f, 0.97f, 0.80f),
            new(0.40f, 0.65f, 0.85f),
            new(0.20f, 0.30f, 0.75f)
        );
        var dayColors = new AtmosphereColorSet(
            new(0.60f, 0.85f, 1.00f),
            new(0.40f, 0.60f, 0.90f),
            new(0.20f, 0.30f, 0.80f)
        );

        (double hour, AtmosphereColorSet color)[] colorSets =
        [
            (0.00, nightColors),
            (sunriseTime - 2.0, nightColors),
            (sunriseTime - 1.0, nightColors2),
            (sunriseTime - 0.5, nightColors1),
            (sunriseTime, sunriseSunsetColors),
            (sunriseTime + 0.75, dayColors1),
            (sunriseTime + 1.5, dayColors2),
            (sunriseTime + 3.0, dayColors),
            (noonTime, dayColors),
            (sunsetTime - 3.0, dayColors),
            (sunsetTime - 1.5, dayColors2),
            (sunsetTime - 0.75, dayColors1),
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
