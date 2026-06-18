using System.Reflection;
using FancyLighting.Utils.Accessors;
using MonoMod.RuntimeDetour;
using ReLogic.Content;
using Terraria.Graphics;

namespace FancyLighting.Utils;

internal static class SpriteBatchEffectLoader
{
    private static SpriteBatchEffect _activeEffect;

    private static Hook _hook_SpriteBatch_PrepRenderState;
    private static Hook _hook_TileBatch_DrawBatch;
    private static Hook _hook_TileBatch_SortedDrawBatch;

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
                _hook_SpriteBatch_PrepRenderState = new(
                    detourMethod,
                    _SpriteBatch_PrepRenderState,
                    true
                );
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
                _hook_TileBatch_DrawBatch = new(detourMethod, _TileBatch_DrawBatch, true);
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
                _hook_TileBatch_SortedDrawBatch = new(
                    detourMethod,
                    _TileBatch_SortedDrawBatch,
                    true
                );
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        _activeEffect = null;

        _hook_SpriteBatch_PrepRenderState?.Dispose();
        _hook_TileBatch_DrawBatch?.Dispose();
        _hook_TileBatch_SortedDrawBatch?.Dispose();

        _hook_SpriteBatch_PrepRenderState = null;
        _hook_TileBatch_DrawBatch = null;
        _hook_TileBatch_SortedDrawBatch = null;
    }

    public static SpriteBatchEffect LoadEffect(string filePath, string techniqueName)
    {
        var effect = ModContent
            .Request<Effect>(filePath, AssetRequestMode.ImmediateLoad)
            .Value;
        var technique = effect.Techniques[techniqueName];

        return new(effect, technique);
    }

    public static void UnloadEffect(ref SpriteBatchEffect shader)
    {
        try
        {
            shader?.Unload();
        }
        catch (Exception)
        {
            // Shouldn't normally happen
        }
        finally
        {
            shader = null;
        }
    }

    internal static void ApplyEffect(SpriteBatchEffect effect) => _activeEffect = effect;

    public static void ClearEffect() => _activeEffect = null;

    private delegate void orig_SpriteBatch_PrepRenderState(SpriteBatch self);

    private static void _SpriteBatch_PrepRenderState(
        orig_SpriteBatch_PrepRenderState orig,
        SpriteBatch self
    )
    {
        orig(self);

        if (!ReferenceEquals(self, Main.spriteBatch) || _activeEffect is null)
        {
            return;
        }

        if (SpriteBatchAccessors.customEffect(self) is null)
        {
            SetMatrixTransform(self, _activeEffect);
            SpriteBatchAccessors.customEffect(self) = _activeEffect.ApplyTechnique();
        }
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

    internal static void SetMatrixTransform(
        SpriteBatch spriteBatch,
        SpriteBatchEffect effect
    )
    {
        // Code adapted from SpriteBatch code

        var viewport = Main.graphics.GraphicsDevice.Viewport;
        var tfWidth = (float)(2.0 / viewport.Width);
        var tfHeight = (float)(-2.0 / viewport.Height);

        var transformMatrix = SpriteBatchAccessors.transformMatrix(spriteBatch);
        var dstMatrix = transformMatrix;
        dstMatrix.M11 = (tfWidth * transformMatrix.M11) - transformMatrix.M14;
        dstMatrix.M21 = (tfWidth * transformMatrix.M21) - transformMatrix.M24;
        dstMatrix.M31 = (tfWidth * transformMatrix.M31) - transformMatrix.M34;
        dstMatrix.M41 = (tfWidth * transformMatrix.M41) - transformMatrix.M44;
        dstMatrix.M12 = (tfHeight * transformMatrix.M12) + transformMatrix.M14;
        dstMatrix.M22 = (tfHeight * transformMatrix.M22) + transformMatrix.M24;
        dstMatrix.M32 = (tfHeight * transformMatrix.M32) + transformMatrix.M34;
        dstMatrix.M42 = (tfHeight * transformMatrix.M42) + transformMatrix.M44;

        effect.SetParameter("MatrixTransform", dstMatrix);
    }
}
