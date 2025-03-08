using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;

namespace FancyLighting.Utils;

internal static class TileUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNonSolid(int x, int y)
    {
        var tile = Main.tile[x, y];
        if (!tile.HasTile)
        {
            return false;
        }

        var tileType = tile.TileType;
        return !Main.tileSolid[tileType];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasShimmer(Tile tile) =>
        tile is { LiquidAmount: > 0, LiquidType: LiquidID.Shimmer };
}
