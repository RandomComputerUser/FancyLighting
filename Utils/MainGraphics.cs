using FancyLighting.Utils.Accessors;

namespace FancyLighting.Utils;

internal static class MainGraphics
{
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

    public static Matrix GetTransformMatrix() =>
        SpriteBatchAccessors.transformMatrix(Main.spriteBatch);
}
