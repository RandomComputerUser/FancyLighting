namespace FancyLighting.ColorGradients.SkyColor;

public struct AtmosphereColorSet(Vector3 lowColor, Vector3 middleColor, Vector3 highColor)
{
    public Vector3 LowColor = lowColor;
    public Vector3 MiddleColor = middleColor;
    public Vector3 HighColor = highColor;

    public readonly AtmosphereCoefficients CalculateCoefficients()
    {
        var coefficients = default(AtmosphereCoefficients);
        Main.NewText("red");
        (
            coefficients.Coefficients0.X,
            coefficients.Coefficients1.X,
            coefficients.Coefficients2.X
        ) = CalculateCoefficients(LowColor.X, MiddleColor.X, HighColor.X);
        Main.NewText("green");
        (
            coefficients.Coefficients0.Y,
            coefficients.Coefficients1.Y,
            coefficients.Coefficients2.Y
        ) = CalculateCoefficients(LowColor.Y, MiddleColor.Y, HighColor.Y);
        Main.NewText("blue");
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
    )
    {
        var coeffs = (x0, ((-3 * x0) + (4 * x1) - x2), ((2 * x0) - (4 * x1) + (2 * x2)));

        var criticalPoint = -coeffs.Item2 / (2.0 * coeffs.Item3);
        var extremum =
            coeffs.Item1
            + (criticalPoint * (coeffs.Item2 + (criticalPoint * coeffs.Item3)));

        if (criticalPoint > 0.0 && criticalPoint < 1.0)
        {
            if (
                (extremum < -0.000001 && coeffs.Item3 > 0.0)
                || (extremum > 1.000001 && coeffs.Item3 < 0.0)
            )
            {
                Main.NewText($"clipping detected ({criticalPoint}, {extremum})");
            }
            else if ((x1 > x0 && x2 > x1) || (x1 < x0 && x2 < x1))
            {
                // Main.NewText($"unwanted extremum detected ({criticalPoint}, {extremum})");
            }
        }

        return ((float)coeffs.Item1, (float)coeffs.Item2, (float)coeffs.Item3);
    }
}

public struct AtmosphereCoefficients
{
    public Vector3 Coefficients0;
    public Vector3 Coefficients1;
    public Vector3 Coefficients2;
}
