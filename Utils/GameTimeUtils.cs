namespace FancyLighting.Utils;

internal static class GameTimeUtils
{
    public static double CalculateCurrentHour() =>
        Main.dayTime ? 4.5 + (Main.time / 3600.0) : 12.0 + 7.5 + (Main.time / 3600.0);
}
