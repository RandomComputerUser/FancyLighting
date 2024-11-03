using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;

namespace FancyLighting.Utils;

internal static class TileUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsVine(int x, int y)
    {
        var tile = Main.tile[x, y];
        if (!tile.HasTile)
        {
            return false;
        }

        var tileType = tile.TileType;
        return TileID.Sets.IsVine[tileType] || tileType is TileID.Seaweed;
    }
}
