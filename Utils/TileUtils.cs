using System.Runtime.CompilerServices;
using Terraria.ID;

namespace FancyLighting.Utils;

internal static class TileUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNonSolid(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Main.tile.Width || y >= Main.tile.Height)
        {
            return false;
        }

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
