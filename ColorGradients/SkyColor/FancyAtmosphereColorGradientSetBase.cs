namespace FancyLighting.ColorGradients.SkyColor;

public abstract class FancyAtmosphereColorGradientSetBase
{
    protected internal ITimedBasedColorGradient _lowGradient;
    protected internal ITimedBasedColorGradient _middleGradient;
    protected internal ITimedBasedColorGradient _highGradient;

    public FancyAtmosphereColorSet GetColors(double hour) =>
        new(
            _lowGradient.GetColor(hour),
            _middleGradient.GetColor(hour),
            _highGradient.GetColor(hour)
        );
}
