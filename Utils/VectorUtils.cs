using System.Runtime.CompilerServices;
using SystemVec3 = System.Numerics.Vector3;
using XnaVec3 = Microsoft.Xna.Framework.Vector3;

namespace FancyLighting.Utils;

internal static class VectorUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SystemVec3 ToSystemVector3(this XnaVec3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XnaVec3 ToXnaVector3(this SystemVec3 vector) =>
        new(vector.X, vector.Y, vector.Z);
}
