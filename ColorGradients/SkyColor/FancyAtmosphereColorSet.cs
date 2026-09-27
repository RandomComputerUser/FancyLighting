namespace FancyLighting.ColorGradients.SkyColor;

public struct FancyAtmosphereColorSet(
    Vector3 lowColor,
    Vector3 middleColor,
    Vector3 highColor
)
{
    public Vector3 LowColor = lowColor;
    public Vector3 MiddleColor = middleColor;
    public Vector3 HighColor = highColor;

    public readonly FancyAtmosphereCoefficients CalculateCoefficients()
    {
        var coefficients = default(FancyAtmosphereCoefficients);
        (
            coefficients.Coefficients0.X,
            coefficients.Coefficients1.X,
            coefficients.Coefficients2.X
        ) = CalculateCoefficients(LowColor.X, MiddleColor.X, HighColor.X);
        (
            coefficients.Coefficients0.Y,
            coefficients.Coefficients1.Y,
            coefficients.Coefficients2.Y
        ) = CalculateCoefficients(LowColor.Y, MiddleColor.Y, HighColor.Y);
        (
            coefficients.Coefficients0.Z,
            coefficients.Coefficients1.Z,
            coefficients.Coefficients2.Z
        ) = CalculateCoefficients(LowColor.Z, MiddleColor.Z, HighColor.Z);
        return coefficients;
    }

    private static (float, float, float) CalculateCoefficients(
        double x0,
        double x1,
        double x2
    ) =>
        (
            (float)x0,
            (float)((-3 * x0) + (4 * x1) - x2),
            (float)((2 * x0) - (4 * x1) + (2 * x2))
        );
}

public struct FancyAtmosphereCoefficients
{
    public Vector3 Coefficients0;
    public Vector3 Coefficients1;
    public Vector3 Coefficients2;
}
