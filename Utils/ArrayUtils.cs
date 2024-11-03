namespace FancyLighting.Utils;

internal static class ArrayUtils
{
    public static void MakeAtLeastSize<T>(ref T[] array, int length)
    {
        if (array is null || array.Length < length)
        {
            array = new T[length];
        }
    }
}
