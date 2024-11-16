using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FancyLighting.Utils;

internal static class MainGraphics
{
    private static FieldInfo _field_samplerState = typeof(SpriteBatch)
        .GetField("samplerState", BindingFlags.NonPublic | BindingFlags.Instance)
        .AssertNotNull();
    private static FieldInfo _field_transformMatrix = typeof(SpriteBatch)
        .GetField("transformMatrix", BindingFlags.NonPublic | BindingFlags.Instance)
        .AssertNotNull();

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
        (SamplerState)(
            _field_samplerState.GetValue(Main.spriteBatch) ?? SamplerState.LinearClamp
        );

    public static Matrix GetTransformMatrix() =>
        (Matrix)(_field_transformMatrix.GetValue(Main.spriteBatch) ?? Matrix.Identity);
}
