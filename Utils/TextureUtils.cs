﻿using System;
using FancyLighting.Config;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FancyLighting.Utils;

internal static class TextureUtils
{
    public static SurfaceFormat Format =>
        LightingConfig.Instance.HiDefFeaturesEnabled()
            ? SurfaceFormat.HalfVector4
            : SurfaceFormat.Color;

    public static void MakeSize(
        ref RenderTarget2D target,
        int width,
        int height,
        SurfaceFormat? format = null,
        RenderTargetUsage? usage = null
    )
    {
        format ??= Format;
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
            target = new RenderTarget2D(
                Main.graphics.GraphicsDevice,
                width,
                height,
                false,
                format.Value,
                DepthFormat.None,
                0,
                usage.Value
            );
        }
    }

    public static void MakeAtLeastSize(ref RenderTarget2D target, int width, int height)
    {
        if (
            target is null
            || target.GraphicsDevice != Main.graphics.GraphicsDevice
            || target.Width < width
            || target.Height < height
            || target.Format != Format
        )
        {
            target?.Dispose();
            width = Math.Max(width, target?.Width ?? 0);
            height = Math.Max(height, target?.Height ?? 0);
            target = new RenderTarget2D(
                Main.graphics.GraphicsDevice,
                width,
                height,
                false,
                Format,
                DepthFormat.None
            );
        }
    }

    public static void MakeAtLeastSize(ref Texture2D texture, int width, int height)
    {
        if (
            texture is null
            || texture.GraphicsDevice != Main.graphics.GraphicsDevice
            || texture.Width < width
            || texture.Height < height
            || texture.Format != Format
        )
        {
            width = Math.Max(width, texture?.Width ?? 0);
            height = Math.Max(height, texture?.Height ?? 0);

            texture?.Dispose();
            texture = new Texture2D(
                Main.graphics.GraphicsDevice,
                width,
                height,
                false,
                Format
            );
        }
    }

    public static void EnsureFormat(ref RenderTarget2D target, bool reset = false)
    {
        if (target is null || target.GraphicsDevice != Main.graphics.GraphicsDevice)
        {
            return;
        }

        var format = reset ? SurfaceFormat.Color : Format;

        if (target.Format == format)
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
        target = new RenderTarget2D(
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
