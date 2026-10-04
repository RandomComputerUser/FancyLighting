namespace FancyLighting.ColorGradients.SkyColor;

public abstract class AtmosphereColorGradientSetBase
{
    protected internal ITimedBasedColorGradient _lowGradient;
    protected internal ITimedBasedColorGradient _middleGradient;
    protected internal ITimedBasedColorGradient _highGradient;

    public AtmosphereColorSet GetColors(double hour) =>
        new(
            _lowGradient.GetColor(hour),
            _middleGradient.GetColor(hour),
            _highGradient.GetColor(hour)
        );
}
