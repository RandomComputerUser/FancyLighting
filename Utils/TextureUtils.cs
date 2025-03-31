using System;
using FancyLighting.Config;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FancyLighting.Utils;

internal static class TextureUtils
{
    public static SurfaceFormat ScreenFormat =>
        LightingConfig.Instance.HiDefFeaturesEnabled()
            ? SurfaceFormat.HalfVector4
            : SurfaceFormat.Color;

    public static SurfaceFormat LightMapFormat =>
        LightingConfig.Instance.DrawOverbright()
            ? SurfaceFormat.HalfVector4
            : SurfaceFormat.Color;

    public static void MakeSize(
        ref RenderTarget2D target,
        int width,
        int height,
        SurfaceFormat format,
        RenderTargetUsage? usage = null
    )
    {
        usage ??= RenderTargetUsage.DiscardContents;

        if (
            target is null
            || target.GraphicsDevice != Main.graphics.GraphicsDevice
            || target.Width != width
            || target.Height != height
            || target.Format != format
            || target.RenderTargetUsage != usage
        )
        {
            target?.Dispose();
            target = new(
                Main.graphics.GraphicsDevice,
                width,
                height,
                false,
                format,
                DepthFormat.None,
                0,
                usage.Value
            );
        }
    }

    public static void MakeAtLeastSize(
        ref Texture2D texture,
        int width,
        int height,
        SurfaceFormat format
    )
    {
        if (
            texture is null
            || texture.GraphicsDevice != Main.graphics.GraphicsDevice
            || texture.Width < width
            || texture.Height < height
            || texture.Format != format
        )
        {
            if (texture is not null)
            {
                width = Math.Max(width, texture.Width);
                height = Math.Max(height, texture.Height);
            }

            texture?.Dispose();
            texture = new(Main.graphics.GraphicsDevice, width, height, false, format);
        }
    }

    public static void EnsureFormat(ref RenderTarget2D target, SurfaceFormat format)
    {
        if (
            target is null
            || target.GraphicsDevice != Main.graphics.GraphicsDevice
            || target.Format == format
        )
        {
            return;
        }

        var width = target.Width;
        var height = target.Height;
        var mipMap = target.LevelCount > 1;
        var depthStencilFormat = target.DepthStencilFormat;
        var multisampleCount = target.MultiSampleCount;
        var usage = target.RenderTargetUsage;
        target.Dispose();
        target = new(
            Main.graphics.GraphicsDevice,
            width,
            height,
            mipMap,
            format,
            depthStencilFormat,
            multisampleCount,
            usage
        );
    }
}
