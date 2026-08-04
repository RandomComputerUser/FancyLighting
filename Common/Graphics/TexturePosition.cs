namespace FancyLighting.Common.Graphics;

public record struct TexturePosition(
    Vector2 TopLeftWorldCoords,
    Vector2 LeftToRightLength,
    Vector2 TopToBottomLength
)
{
    public static TexturePosition GetScreenPosition(Texture2D screenTarget)
    {
        var transform = Main.GameViewMatrix.TransformationMatrix;
        MatrixUtils.Invert2x2HomogeneousTransformation(ref transform);
        var topLeft = Vector2.Transform(Vector2.Zero, transform);
        return new(
            topLeft + Main.screenPosition,
            Vector2.Transform(new Vector2(screenTarget.Width, 0f), transform) - topLeft,
            Vector2.Transform(new Vector2(0f, screenTarget.Height), transform) - topLeft
        );
    }

    public static TexturePosition GetTileTargetPosition(Texture2D tileTarget) =>
        new(
            Main.screenPosition - new Vector2(Main.offScreenRange, Main.offScreenRange),
            new Vector2(tileTarget.Width, 0f),
            new Vector2(0f, tileTarget.Height)
        );

    public static TexturePosition GetTileTargetPosition(
        Texture2D tileTarget,
        Vector2 tilesPosition
    ) =>
        new(
            tilesPosition,
            new Vector2(tileTarget.Width, 0f),
            new Vector2(0f, tileTarget.Height)
        );

    public static TexturePosition FromTextureTileCoords(
        Texture2D texture,
        int topLeftTileX,
        int topLeftTileY,
        float pixelSizeInTiles = 1f,
        bool swapAxes = false
    )
    {
        var topLeft = 16f * new Vector2(topLeftTileX, topLeftTileY);
        var scale = 16f * pixelSizeInTiles;

        Vector2 leftToRight;
        Vector2 topToBottom;
        if (swapAxes)
        {
            leftToRight = scale * new Vector2(0f, texture.Width);
            topToBottom = scale * new Vector2(texture.Height, 0f);
        }
        else
        {
            leftToRight = scale * new Vector2(texture.Width, 0f);
            topToBottom = scale * new Vector2(0f, texture.Height);
        }

        return new(topLeft, leftToRight, topToBottom);
    }

    public void WorldToTextureTransform(out Matrix transform)
    {
        TextureToWorldTransform(out transform);
        MatrixUtils.Invert2x2HomogeneousTransformation(ref transform);
    }

    public void TextureToWorldTransform(out Matrix transform)
    {
        transform = default;

        transform.M11 = LeftToRightLength.X;
        transform.M12 = LeftToRightLength.Y;

        transform.M21 = TopToBottomLength.X;
        transform.M22 = TopToBottomLength.Y;

        transform.M33 = 1f;

        transform.M41 = TopLeftWorldCoords.X;
        transform.M42 = TopLeftWorldCoords.Y;
        transform.M44 = 1f;
    }

    public void WorldToVertexTransform(out Matrix transform)
    {
        VertexToWorldTransform(out transform);
        MatrixUtils.Invert2x2HomogeneousTransformation(ref transform);
    }

    public void VertexToWorldTransform(out Matrix transform)
    {
        transform = default;

        transform.M11 = 0.5f * LeftToRightLength.X;
        transform.M12 = 0.5f * LeftToRightLength.Y;

        transform.M21 = -0.5f * TopToBottomLength.X;
        transform.M22 = -0.5f * TopToBottomLength.Y;

        transform.M33 = 1f;

        transform.M41 =
            TopLeftWorldCoords.X + (0.5f * (LeftToRightLength.X + TopToBottomLength.X));
        transform.M42 =
            TopLeftWorldCoords.Y + (0.5f * (LeftToRightLength.Y + TopToBottomLength.Y));
        transform.M44 = 1f;
    }
}
