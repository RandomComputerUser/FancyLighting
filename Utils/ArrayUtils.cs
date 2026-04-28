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

    public static void MakeSize<T>(ref T[] array, int length)
    {
        if (array is null || array.Length != length)
        {
            array = new T[length];
        }
    }

    public static void MakeAtLeastSizePreserveContents<T>(ref T[] array, int length)
    {
        if (array is null)
        {
            array = new T[length];
            return;
        }

        if (array.Length < length)
        {
            var newArray = new T[length];
            Array.Copy(array, newArray, array.Length);
            array = newArray;
        }
    }
}
