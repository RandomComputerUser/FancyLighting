using System;
using System.Reflection;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using Terraria.ModLoader;

namespace FancyLighting.ModCompatibility;

internal static class LightsCompatibility
{
    private static Hook _hook_NewScreenTarget;
    private static Hook _hook_UseLightAndShadow;
    private static Hook _hook_UseBloom;

    private static RenderTarget2D _lightsTarget1;
    private static RenderTarget2D _lightsTarget2;

    internal static void Load()
    {
        if (!ModLoader.HasMod("Lights"))
        {
            return;
        }

        var modClass = ModLoader.GetMod("Lights").GetType();
        MethodInfo detourMethod;

        detourMethod = modClass.GetMethod(
            "NewScreenTarget",
            BindingFlags.Public | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                _hook_NewScreenTarget = new(detourMethod, _NewScreenTarget, true);
            }
            catch (ArgumentException)
            {
                // Unable to add the hook
            }
        }

        detourMethod = modClass.GetMethod(
            "UseLightAndShadow",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                _hook_UseLightAndShadow = new(detourMethod, _UseLightAndShadow, true);
            }
            catch (ArgumentException)
            {
                // Unable to add the hook
            }
        }

        detourMethod = modClass.GetMethod(
            "UseBloom",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                _hook_UseBloom = new(detourMethod, _UseBloom, true);
            }
            catch (ArgumentException)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        _hook_NewScreenTarget?.Dispose();
        _hook_UseLightAndShadow?.Dispose();
        _hook_UseBloom?.Dispose();

        _lightsTarget1?.Dispose();
        _lightsTarget2?.Dispose();
    }

    private delegate void orig_NewScreenTarget(object self);

    private static void _NewScreenTarget(orig_NewScreenTarget orig, object self)
    {
        if (FancyLightingModSystem._hiDef)
        {
            return;
        }

        orig(self);
    }

    private delegate void orig_UseLightAndShadow(
        object self,
        GraphicsDevice gd,
        SpriteBatch sb,
        RenderTarget2D rt1,
        RenderTarget2D rt2
    );

    private static void _UseLightAndShadow(
        orig_UseLightAndShadow orig,
        object self,
        GraphicsDevice gd,
        SpriteBatch sb,
        RenderTarget2D rt1,
        RenderTarget2D rt2
    )
    {
        if (!FancyLightingModSystem._hiDef)
        {
            orig(self, gd, sb, rt1, rt2);
            return;
        }

        TextureUtils.MakeSize(
            ref _lightsTarget1,
            rt1.Width,
            rt1.Height,
            SurfaceFormat.Color,
            RenderTargetUsage.PreserveContents
        );
        TextureUtils.MakeSize(
            ref _lightsTarget2,
            rt1.Width,
            rt1.Height,
            SurfaceFormat.Color
        );

        gd.SetRenderTarget(_lightsTarget1);
        sb.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        sb.Draw(rt1, Vector2.Zero, Color.White);
        sb.End();
        orig(self, gd, sb, _lightsTarget1, _lightsTarget2);
        gd.SetRenderTarget(rt1);
        sb.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        sb.Draw(_lightsTarget1, Vector2.Zero, Color.White);
        sb.End();
    }

    private delegate void orig_UseBloom(
        object self,
        GraphicsDevice graphicsDevice,
        RenderTarget2D rt1,
        RenderTarget2D rt2
    );

    private static void _UseBloom(
        orig_UseBloom orig,
        object self,
        GraphicsDevice graphicsDevice,
        RenderTarget2D rt1,
        RenderTarget2D rt2
    )
    {
        if (FancyLightingModSystem._hiDef)
        {
            // Use the included bloom effect instead
            return;
        }

        orig(self, graphicsDevice, rt1, rt2);
    }
}
