using System;

namespace FancyLighting.Util;

#nullable enable
internal static class NullSafetyExtensions
{
    public static T AssertNotNull<T>(this T? value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        return value;
    }
}
