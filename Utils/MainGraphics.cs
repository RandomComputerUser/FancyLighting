using FancyLighting.Utils.Accessors;

namespace FancyLighting.Utils;

internal static class MainGraphics
{
    private static Stack<(
        int slot,
        Texture texture,
        SamplerState samplerState
    )> _savedTextures = new();

    internal static void Unload()
    {
        _savedTextures = null;
    }

    public static RenderTarget2D GetRenderTarget()
    {
        var renderTargets = Main.graphics.GraphicsDevice.GetRenderTargets();
        var renderTarget =
            renderTargets is null || renderTargets.Length < 1
                ? null
                : (RenderTarget2D)renderTargets[0].RenderTarget;
        return renderTarget;
    }

    public static SamplerState GetSamplerState() =>
        SpriteBatchAccessors.samplerState(Main.spriteBatch) ?? SamplerState.LinearClamp;

    public static RasterizerState GetRasterizerState() =>
        SpriteBatchAccessors.rasterizerState(Main.spriteBatch)
        ?? RasterizerState.CullCounterClockwise;

    public static Matrix GetTransformMatrix() =>
        SpriteBatchAccessors.transformMatrix(Main.spriteBatch);

    internal static void ResetSavedTextures() => _savedTextures.Clear();

    internal static void SetTexture(int slot, Texture texture, SamplerState samplerState)
    {
        _savedTextures.Push(
            (
                slot,
                Main.graphics.GraphicsDevice.Textures[slot],
                Main.graphics.GraphicsDevice.SamplerStates[slot]
            )
        );

        Main.graphics.GraphicsDevice.Textures[slot] = texture;
        Main.graphics.GraphicsDevice.SamplerStates[slot] = samplerState;
    }

    internal static void RestoreSavedTextures()
    {
        while (_savedTextures.TryPop(out var textureInfo))
        {
            Main.graphics.GraphicsDevice.Textures[textureInfo.slot] = textureInfo.texture;
            Main.graphics.GraphicsDevice.SamplerStates[textureInfo.slot] =
                textureInfo.samplerState;
        }
    }
}
