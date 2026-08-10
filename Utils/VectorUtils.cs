using System.Runtime.CompilerServices;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.Utils;

internal static class VectorUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ToSystemVector3(this Vector3 vec) => new(vec.X, vec.Y, vec.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToXnaVector3(this Vec3 vec) => new(vec.X, vec.Y, vec.Z);
}
