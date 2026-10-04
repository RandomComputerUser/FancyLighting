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
        var sunriseSunsetColors = new AtmosphereColorSet(
            new(1f, 1f, 0.01f),
            new(0.01f, 1f, 1f),
            new(0.01f, 0.01f, 1f)
        );
        var dayColors = new AtmosphereColorSet(
            new(0.01f, 1f, 1f),
            new(0.01f, 0.5f, 1f),
            new(0.01f, 0.01f, 1f)
        );

        (double hour, AtmosphereColorSet color)[] colorSets =
        [
            (0.00, nightColors),
            (sunriseTime, sunriseSunsetColors),
            (noonTime, dayColors),
            (sunsetTime, sunriseSunsetColors),
        ];

        foreach (var (hour, colorSet) in colorSets)
        {
            lowGradient.AddColor(hour, colorSet.LowColor);
            middleGradient.AddColor(hour, colorSet.MiddleColor);
            highGradient.AddColor(hour, colorSet.HighColor);
        }
    }
}
