using System;

namespace FancyLighting.Utils;

#nullable enable
internal static class NullSafetyExtensions
{
    public static T AssertNotNull<T>(this T? value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        return value;
    }
}
