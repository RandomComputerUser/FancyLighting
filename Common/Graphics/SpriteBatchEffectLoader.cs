using System.Reflection;
using Terraria.Graphics;

namespace FancyLighting.Common.Graphics;

internal static class SpriteBatchEffectLoader
{
    private static SpriteBatchEffect _activeEffect;
    private static BlendState _activeBlendState;

    internal static void Load()
    {
        var detourMethod = typeof(SpriteBatch).GetMethod(
            "PrepRenderState",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                MonoModHooks.Add(detourMethod, _SpriteBatch_PrepRenderState);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }

        detourMethod = typeof(TileBatch).GetMethod(
            "DrawBatch",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                MonoModHooks.Add(detourMethod, _TileBatch_DrawBatch);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }

        detourMethod = typeof(TileBatch).GetMethod(
            "SortedDrawBatch",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                MonoModHooks.Add(detourMethod, _TileBatch_SortedDrawBatch);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        Reset();
    }

    internal static void Apply(SpriteBatchEffect effect) => _activeEffect = effect;

    internal static void Apply(BlendState blendState) => _activeBlendState = blendState;

    public static void Reset()
    {
        _activeEffect = null;
        _activeBlendState = null;
    }

    private delegate void orig_SpriteBatch_PrepRenderState(SpriteBatch self);

    private static void _SpriteBatch_PrepRenderState(
        orig_SpriteBatch_PrepRenderState orig,
        SpriteBatch self
    )
    {
        if (!ReferenceEquals(self, Main.spriteBatch))
        {
            orig(self);
            return;
        }

        if (_activeBlendState is not null)
        {
            SpriteBatchAccessors.blendState(self) = _activeBlendState;
        }

        if (_activeEffect is null)
        {
            orig(self);
            return;
        }

        var currEffect = SpriteBatchAccessors.customEffect(self);
        if (currEffect is not null && !ReferenceEquals(currEffect, _activeEffect.Effect))
        {
            orig(self);
            return;
        }

        if (SettingsSystem._optimizeRendering)
        {
            // Code adapted from SpriteBatch code

            var device = self.GraphicsDevice;

            device.BlendState = SpriteBatchAccessors.blendState(self);
            device.SamplerStates[0] = SpriteBatchAccessors.samplerState(self);
            device.DepthStencilState = SpriteBatchAccessors.depthStencilState(self);
            device.RasterizerState = SpriteBatchAccessors.rasterizerState(self);

            device.SetVertexBuffer(SpriteBatchAccessors.vertexBuffer(self));
            device.Indices = SpriteBatchAccessors.indexBuffer(self);
        }
        else
        {
            orig(self);
        }

        SetMatrixTransform(self, _activeEffect);
        _activeEffect.ApplyTechnique();
        SpriteBatchAccessors.customEffect(self) = _activeEffect.Effect;
    }

    private delegate void orig_TileBatch_DrawBatch(TileBatch self);

    private static void _TileBatch_DrawBatch(
        orig_TileBatch_DrawBatch orig,
        TileBatch self
    )
    {
        if (ReferenceEquals(self, Main.tileBatch) && _activeEffect is not null)
        {
            _activeEffect.ApplyPass();
        }

        orig(self);
    }

    private delegate void orig_TileBatch_SortedDrawBatch(TileBatch self);

    private static void _TileBatch_SortedDrawBatch(
        orig_TileBatch_SortedDrawBatch orig,
        TileBatch self
    )
    {
        if (ReferenceEquals(self, Main.tileBatch) && _activeEffect is not null)
        {
            _activeEffect.ApplyPass();
        }

        orig(self);
    }

    private static void SetMatrixTransform(
        SpriteBatch spriteBatch,
        SpriteBatchEffect effect
    ) =>
        effect.SetSpriteBatchTransform(SpriteBatchAccessors.transformMatrix(spriteBatch));
}
