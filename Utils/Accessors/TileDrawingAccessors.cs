using System.Runtime.CompilerServices;
using Terraria.GameContent.Drawing;

namespace FancyLighting.Utils.Accessors;

internal static class TileDrawingAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawMultiTileVines")]
    public static extern void DrawMultiTileVines(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawMultiTileGrass")]
    public static extern void DrawMultiTileGrass(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawVoidLenses")]
    public static extern void DrawVoidLenses(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawTeleportationPylons")]
    public static extern void DrawTeleportationPylons(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawMasterTrophies")]
    public static extern void DrawMasterTrophies(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawGrass")]
    public static extern void DrawGrass(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawAnyDirectionalGrass")]
    public static extern void DrawAnyDirectionalGrass(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawTrees")]
    public static extern void DrawTrees(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawVines")]
    public static extern void DrawVines(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawReverseVines")]
    public static extern void DrawReverseVines(TileDrawing obj);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DrawCustom")]
    public static extern void DrawCustom(TileDrawing obj, bool solidLayer);
}
