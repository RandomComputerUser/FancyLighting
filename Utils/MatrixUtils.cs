namespace FancyLighting.Utils;

internal static class MatrixUtils
{
    public static void Invert2x2HomogeneousTransformation(ref Matrix transform)
    {
        var det =
            ((double)transform.M11 * (double)transform.M22)
            - ((double)transform.M12 * (double)transform.M21);

        var inv11 = (double)transform.M22 / det;
        var inv12 = -(double)transform.M12 / det;
        var inv21 = -(double)transform.M21 / det;
        var inv22 = (double)transform.M11 / det;

        transform.M11 = (float)inv11;
        transform.M12 = (float)inv12;
        transform.M21 = (float)inv21;
        transform.M22 = (float)inv22;

        var m41 = (double)transform.M41;
        var m42 = (double)transform.M42;

        transform.M41 = -(float)((inv11 * m41) + (inv21 * m42));
        transform.M42 = -(float)((inv12 * m41) + (inv22 * m42));
    }
}
