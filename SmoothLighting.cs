﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class SmoothLighting
{
    private Texture2D _colors;
    private Texture2D _colorsBackground;
    private RenderTarget2D _colorsHiRes;
    private RenderTarget2D _colorsBackgroundHiRes;

    private readonly Texture2D _ditherNoise;

    private Vector2 _lightMapPosition;
    private Vector2 _lightMapPositionFlipped;
    private Rectangle _lightMapTileArea;
    private Rectangle _lightMapRenderArea;

    private RenderTarget2D _drawTarget1;
    private RenderTarget2D _drawTarget2;

    private Vector3[] _lights;
    private byte[] _hasLight;
    private Color[] _finalLights;
    private Rgba64[] _finalLightsHiDef;

    internal Vector3[] _whiteLights;
    internal Vector3[] _tmpLights;
    internal Vector3[] _blackLights;

    private readonly bool[] _glowingTiles;
    private readonly Color[] _glowingTileColors;

    private bool _isDangersenseActive;
    private bool _isSpelunkerActive;
    private bool _isBiomeSightActive;

    private bool _smoothLightingLightMapValid;
    private bool _smoothLightingPositionValid;
    private bool _smoothLightingForeComplete;
    private bool _smoothLightingBackComplete;
    private bool _smoothLightingForeHiRes;
    private bool _smoothLightingBackHiRes;

    internal RenderTarget2D _cameraModeTarget1;
    internal RenderTarget2D _cameraModeTarget2;
    private RenderTarget2D _cameraModeTarget3;

    internal bool DrawSmoothLightingBack =>
        _smoothLightingBackComplete && LightingConfig.Instance.SmoothLightingEnabled();

    internal bool DrawSmoothLightingFore =>
        _smoothLightingForeComplete && LightingConfig.Instance.SmoothLightingEnabled();

    private readonly FancyLightingMod _modInstance;

    private Shader _bicubicDitherShader;
    private Shader _bicubicNoDitherHiDefShader;
    private Shader _noFilterShader;
    private Shader _normalsShader;
    private Shader _normalsOverbrightShader;
    private Shader _normalsOverbrightAmbientOcclusionShader;
    private Shader _normalsOverbrightLightOnlyShader;
    private Shader _normalsOverbrightLightOnlyOpaqueShader;
    private Shader _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader;
    private Shader _overbrightShader;
    private Shader _overbrightAmbientOcclusionShader;
    private Shader _overbrightLightOnlyShader;
    private Shader _overbrightLightOnlyOpaqueShader;
    private Shader _overbrightLightOnlyOpaqueAmbientOcclusionShader;
    private Shader _overbrightMaxShader;
    private Shader _lightOnlyShader;
    private Shader _brightenBackgroundShader;
    private Shader _glowMaskShader;
    private Shader _enhancedGlowMaskShader;

    public SmoothLighting(FancyLightingMod mod)
    {
        _modInstance = mod;

        _lightMapTileArea = new Rectangle(0, 0, 0, 0);
        _lightMapRenderArea = new Rectangle(0, 0, 0, 0);

        _smoothLightingLightMapValid = false;
        _smoothLightingPositionValid = false;
        _smoothLightingForeComplete = false;
        _smoothLightingBackComplete = false;
        _smoothLightingForeHiRes = false;
        _smoothLightingBackHiRes = false;

        _tmpLights = null;

        _glowingTiles = new bool[ushort.MaxValue + 1];
        foreach (
            var id in new[]
            {
                TileID.Crystals, // Crystal Shards and Gelatin Crystal
                TileID.AshGrass,
                TileID.LavaMoss,
                TileID.LavaMossBrick,
                TileID.LavaMossBlock,
                TileID.ArgonMoss,
                TileID.ArgonMossBrick,
                TileID.ArgonMossBlock,
                TileID.KryptonMoss,
                TileID.KryptonMossBrick,
                TileID.KryptonMossBlock,
                TileID.XenonMoss,
                TileID.XenonMossBrick,
                TileID.XenonMossBlock,
                TileID.VioletMoss, // Neon Moss
                TileID.VioletMossBrick,
                TileID.VioletMossBlock,
                TileID.RainbowMoss,
                TileID.RainbowMossBrick,
                TileID.RainbowMossBlock,
                TileID.RainbowBrick,
                TileID.MeteoriteBrick,
                TileID.MartianConduitPlating,
                TileID.LihzahrdAltar,
                TileID.LunarMonolith,
                TileID.VoidMonolith,
                TileID.ShimmerMonolith, // Aether Monolith
                TileID.PixelBox,
                TileID.LavaLamp,
            }
        )
        {
            _glowingTiles[id] = true;
        }

        _glowingTileColors = new Color[_glowingTiles.Length];

        _glowingTileColors[TileID.Crystals] = Color.White;
        _glowingTileColors[TileID.AshGrass] = new(153, 66, 23);

        _glowingTileColors[TileID.LavaMoss] =
            _glowingTileColors[TileID.LavaMossBrick] =
            _glowingTileColors[TileID.LavaMossBlock] =
                new(225, 61, 0);
        _glowingTileColors[TileID.ArgonMoss] =
            _glowingTileColors[TileID.ArgonMossBrick] =
            _glowingTileColors[TileID.ArgonMossBlock] =
                new(255, 13, 129);
        _glowingTileColors[TileID.KryptonMoss] =
            _glowingTileColors[TileID.KryptonMossBrick] =
            _glowingTileColors[TileID.KryptonMossBlock] =
                new(20, 255, 0);
        _glowingTileColors[TileID.XenonMoss] =
            _glowingTileColors[TileID.XenonMossBrick] =
            _glowingTileColors[TileID.XenonMossBlock] =
                new(0, 181, 255);
        _glowingTileColors[
            TileID.VioletMoss
        ] // Neon Moss
        =
            _glowingTileColors[TileID.VioletMossBrick] =
            _glowingTileColors[TileID.VioletMossBlock] =
                new(166, 9, 255);
        // Rainbow Moss and Bricks are handled separately

        _glowingTileColors[TileID.MeteoriteBrick] = new(219, 104, 19);
        // Martian Conduit Plating is handled separately

        _glowingTileColors[TileID.LihzahrdAltar] = new(138, 130, 22);
        _glowingTileColors[TileID.LunarMonolith] = new(192, 192, 192);
        _glowingTileColors[TileID.VoidMonolith] = new(161, 255, 223);
        _glowingTileColors[TileID.ShimmerMonolith] = new(213, 196, 252);
        _glowingTileColors[TileID.PixelBox] = new(255, 255, 255);
        _glowingTileColors[TileID.LavaLamp] = new(255, 90, 2);

        _isDangersenseActive = false;
        _isSpelunkerActive = false;
        _isBiomeSightActive = false;

        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _bicubicDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "BicubicDither"
        );
        _bicubicNoDitherHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "BicubicNoDitherHiDef"
        );
        _noFilterShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "NoFilter"
        );

        _normalsShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Normals",
            true
        );
        _normalsOverbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbright",
            true
        );
        _normalsOverbrightAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightAmbientOcclusion",
            true
        );
        _normalsOverbrightLightOnlyShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightLightOnly",
            true
        );
        _normalsOverbrightLightOnlyOpaqueShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightLightOnlyOpaque",
            true
        );
        _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightLightOnlyOpaqueAmbientOcclusion",
            true
        );
        _overbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Overbright",
            true
        );
        _overbrightAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightAmbientOcclusion",
            true
        );
        _overbrightLightOnlyShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightLightOnly",
            true
        );
        _overbrightLightOnlyOpaqueShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightLightOnlyOpaque",
            true
        );
        _overbrightLightOnlyOpaqueAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightLightOnlyOpaqueAmbientOcclusion",
            true
        );
        _overbrightMaxShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightMax",
            true
        );
        _lightOnlyShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "LightOnly"
        );
        _brightenBackgroundShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "BrightenBackground"
        );
        _glowMaskShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "GlowMask"
        );
        _enhancedGlowMaskShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "EnhancedGlowMask"
        );
    }

    public void Unload()
    {
        _drawTarget1?.Dispose();
        _drawTarget2?.Dispose();
        _colors?.Dispose();
        _colorsBackground?.Dispose();
        _colorsHiRes?.Dispose();
        _colorsBackgroundHiRes?.Dispose();
        _cameraModeTarget1?.Dispose();
        _cameraModeTarget2?.Dispose();
        _cameraModeTarget3?.Dispose();
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _bicubicDitherShader);
        EffectLoader.UnloadEffect(ref _bicubicNoDitherHiDefShader);
        EffectLoader.UnloadEffect(ref _noFilterShader);
        EffectLoader.UnloadEffect(ref _normalsShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightLightOnlyShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightLightOnlyOpaqueShader);
        EffectLoader.UnloadEffect(
            ref _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader
        );
        EffectLoader.UnloadEffect(ref _overbrightShader);
        EffectLoader.UnloadEffect(ref _overbrightAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _overbrightLightOnlyShader);
        EffectLoader.UnloadEffect(ref _overbrightLightOnlyOpaqueShader);
        EffectLoader.UnloadEffect(ref _overbrightLightOnlyOpaqueAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _overbrightMaxShader);
        EffectLoader.UnloadEffect(ref _lightOnlyShader);
        EffectLoader.UnloadEffect(ref _brightenBackgroundShader);
        EffectLoader.UnloadEffect(ref _glowMaskShader);
        EffectLoader.UnloadEffect(ref _enhancedGlowMaskShader);
    }

    internal void ApplyLightOnlyShader() => _lightOnlyShader.Apply();

    internal void ApplyBrightenBackgroundShader() =>
        _brightenBackgroundShader
            .SetParameter(
                "BackgroundBrightnessMult",
                LightingConfig.Instance.UseLightMapToneMapping ? 1.05f : 1.1f
            )
            .Apply();

    private void PrintException()
    {
        LightingConfig.Instance.UseSmoothLighting = false;
        Main.NewText(
            "[Fancy Lighting] Caught an IndexOutOfRangeException while trying to run smooth lighting.",
            Color.Orange
        );
        Main.NewText(
            "[Fancy Lighting] Smooth lighting has been automatically disabled.",
            Color.Orange
        );
    }

    private static Color MartianConduitPlatingGlowColor() =>
        new(
            new Vector3(
                (float)(
                    0.4
                    - (
                        0.4
                        * Math.Cos(
                            (int)(0.08 * Main.timeForVisualEffects / 6.283) % 3 == 1
                                ? 0.08 * Main.timeForVisualEffects
                                : 0.0
                        )
                    )
                )
            )
        );

    private static Color RainbowGlowColor()
    {
        var color = Main.DiscoColor;
        var vector = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        vector.X = MathF.Sqrt(vector.X);
        vector.Y = MathF.Sqrt(vector.Y);
        vector.Z = MathF.Sqrt(vector.Z);
        VectorToColor.Assign(ref color, 1f, vector);
        return color;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasShimmer(Tile tile) =>
        tile is { LiquidAmount: > 0, LiquidType: LiquidID.Shimmer };

    private static bool ShouldTileShine(ushort type, short frameX)
    {
        // We could use the method from vanilla, but that's
        //   private and using reflection to get a delegate
        //   might reduce performance

        // This code is adapted from vanilla

        if ((Main.shimmerAlpha > 0f && Main.tileSolid[type]) || type == TileID.Stalactite)
        {
            return true;
        }

        if (!Main.tileShine2[type])
        {
            return false;
        }

        switch (type)
        {
            case TileID.Containers:
            case TileID.FakeContainers:
                if (frameX >= 36)
                {
                    return frameX < 178;
                }

                return false;

            case TileID.Containers2:
            case TileID.FakeContainers2:
                if (frameX >= 144)
                {
                    return frameX < 178;
                }

                return false;
        }

        return true;
    }

    private static void TileShine(ref Vector3 color, Tile tile)
    {
        // Method from vanilla limits brightness to 1f,
        //   which we don't want

        // This code is adapted from vanilla

        var type = tile.TileType;
        var frameX = tile.TileFrameX;

        if (!ShouldTileShine(type, frameX))
        {
            return;
        }

        float brightness;

        switch (type)
        {
            case TileID.Ebonstone:
                color.X *= 0.95f;
                color.Y *= 0.85f;
                color.Z *= 1.1f;
                break;

            case TileID.Pearlstone:
                color.X *= 1.1f;
                color.Z *= 1.2f;
                break;

            case TileID.SnowBlock:
            case TileID.IceBlock:
                color.X *= 1.1f;
                color.Y *= 1.12f;
                color.Z *= 1.15f;
                break;

            case TileID.CorruptIce:
                color.X *= 1.05f;
                color.Y *= 1.1f;
                color.Z *= 1.15f;
                break;

            case TileID.HallowedIce:
                color.X *= 1.1f;
                color.Y *= 1.1f;
                color.Z *= 1.2f;
                break;

            case TileID.ExposedGems:
                color.X *= 1.5f;
                color.Y *= 1.5f;
                color.Z *= 1.5f;
                break;

            case TileID.SmallPiles:
            case TileID.LargePiles:
                color.X *= 1.3f;
                color.Y *= 1.3f;
                color.Z *= 1.3f;
                break;

            case TileID.Crimtane:
                brightness = 0.3f + (Main.mouseTextColor * (1f / 300f));
                color.X *= 1.3f * brightness;
                return;

            case TileID.Chlorophyte:
                brightness = 0.3f + (Main.mouseTextColor * (1f / 300f));
                color.Y *= 1.5f * brightness;
                color.Z *= 1.1f * brightness;
                break;

            case TileID.AmethystGemspark:
            case TileID.TopazGemspark:
            case TileID.SapphireGemspark:
            case TileID.EmeraldGemspark:
            case TileID.RubyGemspark:
            case TileID.DiamondGemspark:
            case TileID.AmberGemspark:
                color.X += 100f / 255f;
                color.Y += 100f / 255f;
                color.Z += 100f / 255f;
                break;

            default:
                if (Main.tileShine2[type])
                {
                    color.X *= 1.6f;
                    color.Y *= 1.6f;
                    color.Z *= 1.6f;
                }

                break;
        }

        var shimmer = Main.shimmerAlpha;
        if (shimmer > 0f)
        {
            var tmp = 1f - shimmer;
            color.X *= tmp + (1.2f * shimmer);
            color.Z *= tmp + (1.6f * shimmer);
        }
    }

    internal void GetAndBlurLightMap(
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    )
    {
        _smoothLightingLightMapValid = false;
        _smoothLightingPositionValid = false;
        _smoothLightingForeComplete = false;
        _smoothLightingBackComplete = false;
        _smoothLightingForeHiRes = false;
        _smoothLightingBackHiRes = false;

        var length = width * height;

        ArrayUtil.MakeAtLeastSize(ref _lights, length);
        ArrayUtil.MakeAtLeastSize(ref _whiteLights, length);
        ArrayUtil.MakeAtLeastSize(ref _blackLights, length);
        ArrayUtil.MakeAtLeastSize(ref _hasLight, length);

        if (width == 0 || height == 0)
        {
            return;
        }

        if (colors.Length < length)
        {
            return;
        }

        var caughtException = 0;
        var doGammaCorrection = LightingConfig.Instance.DoGammaCorrection();
        var blurLightMap = LightingConfig.Instance.UseLightMapBlurring;
        var doToneMap = LightingConfig.Instance.UseLightMapToneMapping;

        if (doGammaCorrection && !LightingConfig.Instance.FancyLightingEngineEnabled())
        {
            Parallel.For(
                0,
                width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (x) =>
                {
                    var i = height * x;
                    for (var y = 0; y < height; ++y)
                    {
                        try
                        {
                            GammaConverter.GammaToLinear(ref colors[i++]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                            break;
                        }
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }
        }

        var lightEngine = (LightingEngine)_modInstance._field_activeEngine.GetValue(null);
        var lightMapTileArea = (Rectangle)
            _modInstance
                ._field_workingProcessedArea.GetValue(lightEngine)
                .AssertNotNull();

        if (blurLightMap)
        {
            BlurLightMap(
                colors,
                lightMasks,
                width,
                height,
                lightMapTileArea,
                ref caughtException
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            var offset = (width - 1) * height;
            for (var i = 0; i < height; ++i)
            {
                try
                {
                    _lights[i] = colors[i];
                    _lights[i + offset] = colors[i + offset];
                }
                catch (IndexOutOfRangeException)
                {
                    PrintException();
                    return;
                }
            }

            var end = (width - 1) * height;
            offset = height - 1;
            for (var i = height; i < end; i += height)
            {
                try
                {
                    _lights[i] = colors[i];
                    _lights[i + offset] = colors[i + offset];
                }
                catch (IndexOutOfRangeException)
                {
                    PrintException();
                    return;
                }
            }
        }

        if (doToneMap)
        {
            var lights = blurLightMap ? _lights : colors;

            if (doGammaCorrection)
            {
                Parallel.For(
                    0,
                    width,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x) =>
                    {
                        var i = height * x;
                        for (var y = 0; y < height; ++y)
                        {
                            try
                            {
                                ToneMapping.ToneMap(ref lights[i++]);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                                break;
                            }
                        }
                    }
                );
            }
            else
            {
                Parallel.For(
                    0,
                    width,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x) =>
                    {
                        var i = height * x;
                        for (var y = 0; y < height; ++y)
                        {
                            try
                            {
                                ref var lightColor = ref lights[i++];

                                GammaConverter.GammaToLinear(ref lightColor);
                                ToneMapping.ToneMap(ref lightColor);
                                GammaConverter.LinearToGamma(ref lightColor);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                                break;
                            }
                        }
                    }
                );
            }

            if (caughtException == 1)
            {
                PrintException();
                return;
            }
        }

        if (doGammaCorrection)
        {
            var lights = blurLightMap ? _lights : colors;
            var otherLights = blurLightMap ? colors : _lights;

            Parallel.For(
                0,
                width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (x) =>
                {
                    var i = height * x;
                    for (var y = 0; y < height; ++y)
                    {
                        try
                        {
                            GammaConverter.LinearToGamma(ref lights[i++]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                            break;
                        }
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            Array.Copy(lights, otherLights, length);
        }
        else
        {
            if (blurLightMap)
            {
                Array.Copy(_lights, colors, length);
            }
            else
            {
                Array.Copy(colors, _lights, length);
            }
        }

        var low = 0.49f / 255f;
        const int HasLightAmount = 1;

        var ymax = lightMapTileArea.Y + lightMapTileArea.Height;
        if (LightingConfig.Instance.SupportGlowMasks())
        {
            Parallel.For(
                lightMapTileArea.X,
                lightMapTileArea.X + lightMapTileArea.Width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (x) =>
                {
                    var dummyColor = new Color();

                    var isXInTilemap = x >= 0 && x < Main.tile.Width;
                    var tilemapHeight = Main.tile.Height;
                    var i = height * (x - lightMapTileArea.X);
                    for (var y = lightMapTileArea.Y; y < ymax; ++y)
                    {
                        try
                        {
                            ref var color = ref _lights[i];
                            if (color.X > low || color.Y > low || color.Z > low)
                            {
                                _hasLight[i++] = HasLightAmount;
                                continue;
                            }

                            if (isXInTilemap && y >= 0 && y < tilemapHeight)
                            {
                                var tile = Main.tile[x, y];

                                if (
                                    HasShimmer(tile) // Shimmer
                                    || (
                                        _isDangersenseActive // Dangersense Potion
                                        && TileDrawing.IsTileDangerous(
                                            x,
                                            y,
                                            Main.LocalPlayer
                                        )
                                    )
                                    || (
                                        _isSpelunkerActive && Main.IsTileSpelunkable(x, y)
                                    ) // Spelunker Potion
                                    || (
                                        _isBiomeSightActive
                                        && Main.IsTileBiomeSightable(x, y, ref dummyColor)
                                    ) // Biome Sight Potion
                                )
                                {
                                    _hasLight[i++] = HasLightAmount;
                                    continue;
                                }
                            }

                            if (_hasLight[i] != 0)
                            {
                                --_hasLight[i];
                            }

                            ++i;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                            break;
                        }
                    }
                }
            );
        }
        else
        {
            Parallel.For(
                lightMapTileArea.X,
                lightMapTileArea.X + lightMapTileArea.Width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (x) =>
                {
                    var dummyColor = new Color();

                    var isXInTilemap = x >= 0 && x < Main.tile.Width;
                    var tilemapHeight = Main.tile.Height;
                    var i = height * (x - lightMapTileArea.X);
                    for (var y = lightMapTileArea.Y; y < ymax; ++y)
                    {
                        try
                        {
                            ref var color = ref _lights[i];
                            if (color.X > low || color.Y > low || color.Z > low)
                            {
                                _hasLight[i++] = HasLightAmount;
                                continue;
                            }

                            if (isXInTilemap && y >= 0 && y < tilemapHeight)
                            {
                                var tile = Main.tile[x, y];

                                if (
                                    tile.IsTileFullbright // Illuminant Paint
                                    || tile.IsWallFullbright
                                    || HasShimmer(tile) // Shimmer
                                    || _glowingTiles[tile.TileType] // Glowing Tiles
                                    || (
                                        _isDangersenseActive // Dangersense Potion
                                        && TileDrawing.IsTileDangerous(
                                            x,
                                            y,
                                            Main.LocalPlayer
                                        )
                                    )
                                    || (
                                        _isSpelunkerActive && Main.IsTileSpelunkable(x, y)
                                    ) // Spelunker Potion
                                    || (
                                        _isBiomeSightActive
                                        && Main.IsTileBiomeSightable(x, y, ref dummyColor)
                                    ) // Biome Sight Potion
                                )
                                {
                                    _hasLight[i++] = HasLightAmount;
                                    continue;
                                }
                            }

                            if (_hasLight[i] != 0)
                            {
                                --_hasLight[i];
                            }

                            ++i;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                            break;
                        }
                    }
                }
            );
        }

        if (caughtException == 1)
        {
            PrintException();
            return;
        }

        Parallel.For(
            1,
            width - 1,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
            },
            (x) =>
            {
                var i = height * x;
                for (var y = 1; y < height - 1; ++y)
                {
                    try
                    {
                        ref var whiteLight = ref _whiteLights[++i];
                        ref var blackLight = ref _blackLights[i];

                        if (
                            _hasLight[i] != 0
                            || _hasLight[i - 1] != 0
                            || _hasLight[i + 1] != 0
                            || _hasLight[i - height] != 0
                            || _hasLight[i - height - 1] != 0
                            || _hasLight[i - height + 1] != 0
                            || _hasLight[i + height] != 0
                            || _hasLight[i + height - 1] != 0
                            || _hasLight[i + height + 1] != 0
                        )
                        {
                            whiteLight.X = 1f;
                            whiteLight.Y = 1f;
                            whiteLight.Z = 1f;
                            blackLight.X = 1f / 255;
                            blackLight.Y = 1f / 255;
                            blackLight.Z = 1f / 255;
                        }
                        else
                        {
                            whiteLight.X = 0f;
                            whiteLight.Y = 0f;
                            whiteLight.Z = 0f;
                            blackLight.X = 0f;
                            blackLight.Y = 0f;
                            blackLight.Z = 0f;
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        Interlocked.Exchange(ref caughtException, 1);
                        break;
                    }
                }
            }
        );

        if (caughtException == 1)
        {
            PrintException();
            return;
        }

        _lightMapTileArea = lightMapTileArea;
        _lightMapRenderArea = new Rectangle(
            0,
            0,
            _lightMapTileArea.Height,
            _lightMapTileArea.Width
        );

        _smoothLightingLightMapValid = true;
    }

    private void BlurLightMap(
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height,
        Rectangle lightMapTileArea,
        ref int caughtException
    )
    {
        var caught = caughtException;

        if (LightingConfig.Instance.UseEnhancedBlurring)
        {
            if (PreferencesConfig.Instance.FancyLightingEngineVinesOpaque)
            {
                Parallel.For(
                    1,
                    width - 1,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x) =>
                    {
                        var i = height * x;
                        for (var y = 1; y < height - 1; ++y)
                        {
                            ++i;

                            try
                            {
                                var mask = lightMasks[i];

                                var upperLeftMult =
                                    lightMasks[i - height - 1] == mask ? 1f : 0f;
                                var leftMult = lightMasks[i - height] == mask ? 2f : 0f;
                                var lowerLeftMult =
                                    lightMasks[i - height + 1] == mask ? 1f : 0f;
                                var upperMult = lightMasks[i - 1] == mask ? 2f : 0f;
                                var middleMult = mask is LightMaskMode.Solid ? 12f : 4f;
                                var lowerMult = lightMasks[i + 1] == mask ? 2f : 0f;
                                var upperRightMult =
                                    lightMasks[i + height - 1] == mask ? 1f : 0f;
                                var rightMult = lightMasks[i + height] == mask ? 2f : 0f;
                                var lowerRightMult =
                                    lightMasks[i + height + 1] == mask ? 1f : 0f;

                                var mult =
                                    1f
                                    / (
                                        (upperLeftMult + leftMult + lowerLeftMult)
                                        + (upperMult + middleMult + lowerMult)
                                        + (upperRightMult + rightMult + lowerRightMult)
                                    );

                                ref var light = ref _lights[i];

                                ref var upperLeft = ref colors[i - height - 1];
                                ref var left = ref colors[i - height];
                                ref var lowerLeft = ref colors[i - height + 1];
                                ref var upper = ref colors[i - 1];
                                ref var middle = ref colors[i];
                                ref var lower = ref colors[i + 1];
                                ref var upperRight = ref colors[i + height - 1];
                                ref var right = ref colors[i + height];
                                ref var lowerRight = ref colors[i + height + 1];

                                // Faster to do it separately for each component
                                light.X =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.X)
                                            + (leftMult * left.X)
                                            + (lowerLeftMult * lowerLeft.X)
                                        )
                                        + (
                                            (upperMult * upper.X)
                                            + (middleMult * middle.X)
                                            + (lowerMult * lower.X)
                                        )
                                        + (
                                            (upperRightMult * upperRight.X)
                                            + (rightMult * right.X)
                                            + (lowerRightMult * lowerRight.X)
                                        )
                                    ) * mult;

                                light.Y =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Y)
                                            + (leftMult * left.Y)
                                            + (lowerLeftMult * lowerLeft.Y)
                                        )
                                        + (
                                            (upperMult * upper.Y)
                                            + (middleMult * middle.Y)
                                            + (lowerMult * lower.Y)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Y)
                                            + (rightMult * right.Y)
                                            + (lowerRightMult * lowerRight.Y)
                                        )
                                    ) * mult;

                                light.Z =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Z)
                                            + (leftMult * left.Z)
                                            + (lowerLeftMult * lowerLeft.Z)
                                        )
                                        + (
                                            (upperMult * upper.Z)
                                            + (middleMult * middle.Z)
                                            + (lowerMult * lower.Z)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Z)
                                            + (rightMult * right.Z)
                                            + (lowerRightMult * lowerRight.Z)
                                        )
                                    ) * mult;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caught, 1);
                                break;
                            }
                        }
                    }
                );
            }
            else
            {
                Parallel.For(
                    1,
                    width - 1,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x) =>
                    {
                        var tileX = x + lightMapTileArea.X;
                        var tileY = lightMapTileArea.Y;
                        var i = height * x;
                        for (var y = 1; y < height - 1; ++y)
                        {
                            ++i;
                            ++tileY;

                            try
                            {
                                var mask = lightMasks[i];
                                var isSolid = mask is LightMaskMode.Solid;
                                var isVine = TileUtil.IsVine(tileX, tileY);

                                var upperLeftMult =
                                    lightMasks[i - height - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX - 1, tileY - 1) == isVine
                                    )
                                        ? 1f
                                        : 0f;
                                var leftMult =
                                    lightMasks[i - height] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX - 1, tileY) == isVine
                                    )
                                        ? 2f
                                        : 0f;
                                var lowerLeftMult =
                                    lightMasks[i - height + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX - 1, tileY + 1) == isVine
                                    )
                                        ? 1f
                                        : 0f;
                                var upperMult =
                                    lightMasks[i - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX, tileY - 1) == isVine
                                    )
                                        ? 2f
                                        : 0f;
                                var middleMult = isSolid && !isVine ? 12f : 4f;
                                var lowerMult =
                                    lightMasks[i + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX, tileY + 1) == isVine
                                    )
                                        ? 2f
                                        : 0f;
                                var upperRightMult =
                                    lightMasks[i + height - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX + 1, tileY - 1) == isVine
                                    )
                                        ? 1f
                                        : 0f;
                                var rightMult =
                                    lightMasks[i + height] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX + 1, tileY) == isVine
                                    )
                                        ? 2f
                                        : 0f;
                                var lowerRightMult =
                                    lightMasks[i + height + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtil.IsVine(tileX + 1, tileY + 1) == isVine
                                    )
                                        ? 1f
                                        : 0f;

                                var mult =
                                    1f
                                    / (
                                        (upperLeftMult + leftMult + lowerLeftMult)
                                        + (upperMult + middleMult + lowerMult)
                                        + (upperRightMult + rightMult + lowerRightMult)
                                    );

                                ref var light = ref _lights[i];

                                ref var upperLeft = ref colors[i - height - 1];
                                ref var left = ref colors[i - height];
                                ref var lowerLeft = ref colors[i - height + 1];
                                ref var upper = ref colors[i - 1];
                                ref var middle = ref colors[i];
                                ref var lower = ref colors[i + 1];
                                ref var upperRight = ref colors[i + height - 1];
                                ref var right = ref colors[i + height];
                                ref var lowerRight = ref colors[i + height + 1];

                                // Faster to do it separately for each component
                                light.X =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.X)
                                            + (leftMult * left.X)
                                            + (lowerLeftMult * lowerLeft.X)
                                        )
                                        + (
                                            (upperMult * upper.X)
                                            + (middleMult * middle.X)
                                            + (lowerMult * lower.X)
                                        )
                                        + (
                                            (upperRightMult * upperRight.X)
                                            + (rightMult * right.X)
                                            + (lowerRightMult * lowerRight.X)
                                        )
                                    ) * mult;

                                light.Y =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Y)
                                            + (leftMult * left.Y)
                                            + (lowerLeftMult * lowerLeft.Y)
                                        )
                                        + (
                                            (upperMult * upper.Y)
                                            + (middleMult * middle.Y)
                                            + (lowerMult * lower.Y)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Y)
                                            + (rightMult * right.Y)
                                            + (lowerRightMult * lowerRight.Y)
                                        )
                                    ) * mult;

                                light.Z =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Z)
                                            + (leftMult * left.Z)
                                            + (lowerLeftMult * lowerLeft.Z)
                                        )
                                        + (
                                            (upperMult * upper.Z)
                                            + (middleMult * middle.Z)
                                            + (lowerMult * lower.Z)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Z)
                                            + (rightMult * right.Z)
                                            + (lowerRightMult * lowerRight.Z)
                                        )
                                    ) * mult;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caught, 1);
                                break;
                            }
                        }
                    }
                );
            }
        }
        else
        {
            Parallel.For(
                1,
                width - 1,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (x) =>
                {
                    var i = height * x;
                    for (var y = 1; y < height - 1; ++y)
                    {
                        ++i;

                        try
                        {
                            ref var light = ref _lights[i];

                            ref var upperLeft = ref colors[i - height - 1];
                            ref var left = ref colors[i - height];
                            ref var lowerLeft = ref colors[i - height + 1];
                            ref var upper = ref colors[i - 1];
                            ref var middle = ref colors[i];
                            ref var lower = ref colors[i + 1];
                            ref var upperRight = ref colors[i + height - 1];
                            ref var right = ref colors[i + height];
                            ref var lowerRight = ref colors[i + height + 1];

                            // Faster to do it separately for each component
                            light.X =
                                (
                                    (upperLeft.X + (2f * left.X) + lowerLeft.X)
                                    + (2f * (upper.X + (2f * middle.X) + lower.X))
                                    + (upperRight.X + (2f * right.X) + lowerRight.X)
                                ) * (1f / 16f);

                            light.Y =
                                (
                                    (upperLeft.Y + (2f * left.Y) + lowerLeft.Y)
                                    + (2f * (upper.Y + (2f * middle.Y) + lower.Y))
                                    + (upperRight.Y + (2f * right.Y) + lowerRight.Y)
                                ) * (1f / 16f);

                            light.Z =
                                (
                                    (upperLeft.Z + (2f * left.Z) + lowerLeft.Z)
                                    + (2f * (upper.Z + (2f * middle.Z) + lower.Z))
                                    + (upperRight.Z + (2f * right.Z) + lowerRight.Z)
                                ) * (1f / 16f);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caught, 1);
                            break;
                        }
                    }
                }
            );
        }

        caughtException = caught;
    }

    private void GetColorsPosition(bool cameraMode)
    {
        var xmin = _lightMapTileArea.X;
        var ymin = _lightMapTileArea.Y;
        var width = _lightMapTileArea.Width;
        var height = _lightMapTileArea.Height;

        if (width == 0 || height == 0)
        {
            return;
        }

        _lightMapPosition = 16f * new Vector2(xmin + width, ymin);
        _lightMapPositionFlipped = 16f * new Vector2(xmin, ymin + height);

        _smoothLightingPositionValid = !cameraMode;
    }

    internal void CalculateSmoothLighting(
        bool background,
        bool cameraMode = false,
        bool force = false
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        if (!_smoothLightingLightMapValid)
        {
            return;
        }

        if (!force)
        {
            if (background && !cameraMode && _smoothLightingBackComplete)
            {
                return;
            }

            if (!background && !cameraMode && _smoothLightingForeComplete)
            {
                return;
            }
        }

        _isDangersenseActive = Main.LocalPlayer.dangerSense;
        _isSpelunkerActive = Main.LocalPlayer.findTreasure;
        _isBiomeSightActive = Main.LocalPlayer.biomeSight;

        if (!_smoothLightingPositionValid || cameraMode)
        {
            GetColorsPosition(cameraMode);
        }

        if (!_smoothLightingPositionValid && !cameraMode)
        {
            return;
        }

        if (Main.tile.Height == 0 || Main.tile.Width == 0)
        {
            return;
        }

        var xmin = _lightMapTileArea.X;
        var ymin = _lightMapTileArea.Y;
        var width = _lightMapTileArea.Width;
        var height = _lightMapTileArea.Height;
        var ymax = ymin + height;

        var clampedXmin = Math.Clamp(xmin, 0, Main.tile.Width);
        var clampedXmax = Math.Clamp(xmin + width, 0, Main.tile.Width);
        if (clampedXmax - clampedXmin < 1)
        {
            return;
        }

        var clampedStart = Math.Clamp(clampedXmin - xmin, 0, width);
        var clampedEnd = Math.Clamp(clampedXmax - clampedXmin, 0, width);
        if (clampedEnd - clampedStart < 1)
        {
            return;
        }

        var clampedYmin = Math.Clamp(ymin, 0, Main.tile.Height);
        var clampedYmax = Math.Clamp(ymax, 0, Main.tile.Height);
        if (clampedYmax - clampedYmin < 1)
        {
            return;
        }

        var offset = clampedYmin - ymin;
        if (offset < 0 || offset >= height)
        {
            return;
        }

        if (LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            CalculateSmoothLightingHiDef(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                background,
                cameraMode
            );
        }
        else
        {
            CalculateSmoothLightingReach(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                background,
                cameraMode
            );
        }
    }

    private void CalculateSmoothLightingHiDef(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool background,
        bool cameraMode
    )
    {
        var length = width * height;

        ArrayUtil.MakeAtLeastSize(ref _finalLightsHiDef, length);
        _finalLights = null; // Save some memory

        var caughtException = 0;

        const int OverbrightWhite = 4096;
        const float OverbrightMult = OverbrightWhite / 65535f;

        var brightness = Lighting.GlobalBrightness;
        var glowMult = brightness / 255f;
        var multFromOverbright = LightingConfig.Instance.DrawOverbright()
            ? OverbrightMult
            : 1f;
        var useGlowMasks = LightingConfig.Instance.SupportGlowMasks();

        if (background)
        {
            if (useGlowMasks)
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );

                                var tile = Main.tile[x, y];

                                // Shimmer
                                if (HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }

                                VectorToColor.Assign(
                                    ref _finalLightsHiDef[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }
            else
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );

                                var tile = Main.tile[x, y];

                                // Illuminant Paint and Shimmer
                                if (tile.IsWallFullbright || HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }

                                VectorToColor.Assign(
                                    ref _finalLightsHiDef[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureUtil.MakeAtLeastSize(ref _colorsBackground, height, width);

            _colorsBackground.SetData(
                0,
                _lightMapRenderArea,
                _finalLightsHiDef,
                0,
                length
            );

            _smoothLightingBackComplete = !cameraMode;
        }
        else
        {
            if (useGlowMasks)
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );
                                var tile = Main.tile[x, y];

                                var biomeSightColor = new Color();

                                // Shimmer
                                if (HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }
                                // Dangersense Potion
                                else if (
                                    _isDangersenseActive
                                    && TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 255f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 50f / 255f);
                                    lightColor.Z = Math.Max(lightColor.Z, 50f / 255f);
                                }
                                // Spelunker Potion
                                else if (
                                    _isSpelunkerActive && Main.IsTileSpelunkable(x, y)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 200f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 170f / 255f);
                                }
                                // Biome Sight Potion
                                else if (
                                    _isBiomeSightActive
                                    && Main.IsTileBiomeSightable(
                                        x,
                                        y,
                                        ref biomeSightColor
                                    )
                                )
                                {
                                    lightColor.X = Math.Max(
                                        lightColor.X,
                                        (1f / 255f) * biomeSightColor.R
                                    );
                                    lightColor.Y = Math.Max(
                                        lightColor.Y,
                                        (1f / 255f) * biomeSightColor.G
                                    );
                                    lightColor.Z = Math.Max(
                                        lightColor.Z,
                                        (1f / 255f) * biomeSightColor.B
                                    );
                                }

                                TileShine(ref lightColor, tile);

                                VectorToColor.Assign(
                                    ref _finalLightsHiDef[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }
            else
            {
                _glowingTileColors[TileID.MartianConduitPlating] =
                    MartianConduitPlatingGlowColor();

                _glowingTileColors[TileID.RainbowMoss] =
                    _glowingTileColors[TileID.RainbowMossBrick] =
                    _glowingTileColors[TileID.RainbowMossBlock] =
                    _glowingTileColors[TileID.RainbowBrick] =
                        RainbowGlowColor();

                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );
                                var tile = Main.tile[x, y];

                                var biomeSightColor = new Color();

                                // Illuminant Paint and Shimmer
                                if (tile.IsTileFullbright || HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }
                                // Glowing tiles
                                else if (_glowingTiles[tile.TileType])
                                {
                                    ref var glow = ref _glowingTileColors[tile.TileType];

                                    lightColor.X = Math.Max(
                                        lightColor.X,
                                        glowMult * glow.R
                                    );
                                    lightColor.Y = Math.Max(
                                        lightColor.Y,
                                        glowMult * glow.G
                                    );
                                    lightColor.Z = Math.Max(
                                        lightColor.Z,
                                        glowMult * glow.B
                                    );
                                }
                                // Dangersense Potion
                                else if (
                                    _isDangersenseActive
                                    && TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 255f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 50f / 255f);
                                    lightColor.Z = Math.Max(lightColor.Z, 50f / 255f);
                                }
                                // Spelunker Potion
                                else if (
                                    _isSpelunkerActive && Main.IsTileSpelunkable(x, y)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 200f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 170f / 255f);
                                }
                                // Biome Sight Potion
                                else if (
                                    _isBiomeSightActive
                                    && Main.IsTileBiomeSightable(
                                        x,
                                        y,
                                        ref biomeSightColor
                                    )
                                )
                                {
                                    lightColor.X = Math.Max(
                                        lightColor.X,
                                        (1f / 255f) * biomeSightColor.R
                                    );
                                    lightColor.Y = Math.Max(
                                        lightColor.Y,
                                        (1f / 255f) * biomeSightColor.G
                                    );
                                    lightColor.Z = Math.Max(
                                        lightColor.Z,
                                        (1f / 255f) * biomeSightColor.B
                                    );
                                }

                                TileShine(ref lightColor, tile);

                                VectorToColor.Assign(
                                    ref _finalLightsHiDef[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureUtil.MakeAtLeastSize(ref _colors, height, width);

            _colors.SetData(0, _lightMapRenderArea, _finalLightsHiDef, 0, length);

            _smoothLightingForeComplete = !cameraMode;
        }
    }

    private void CalculateSmoothLightingReach(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool background,
        bool cameraMode
    )
    {
        var length = width * height;

        ArrayUtil.MakeAtLeastSize(ref _finalLights, length);
        _finalLightsHiDef = null; // Save some memory

        var caughtException = 0;

        const int OverbrightWhite = 128;
        const float OverbrightMult = OverbrightWhite / 255f;

        var brightness = Lighting.GlobalBrightness;
        var glowMult = brightness / 255f;
        var multFromOverbright = LightingConfig.Instance.DrawOverbright()
            ? OverbrightMult
            : 1f;
        var useGlowMasks = LightingConfig.Instance.SupportGlowMasks();

        if (background)
        {
            if (useGlowMasks)
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );

                                var tile = Main.tile[x, y];

                                // Shimmer
                                if (HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }

                                VectorToColor.Assign(
                                    ref _finalLights[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }
            else
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );

                                var tile = Main.tile[x, y];

                                // Illuminant Paint and Shimmer
                                if (tile.IsWallFullbright || HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }

                                VectorToColor.Assign(
                                    ref _finalLights[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureUtil.MakeAtLeastSize(ref _colorsBackground, height, width);

            _colorsBackground.SetData(0, _lightMapRenderArea, _finalLights, 0, length);

            _smoothLightingBackComplete = !cameraMode;
        }
        else
        {
            if (useGlowMasks)
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );
                                var tile = Main.tile[x, y];

                                var biomeSightColor = new Color();

                                // Shimmer
                                if (HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }
                                // Dangersense Potion
                                else if (
                                    _isDangersenseActive
                                    && TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 255f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 50f / 255f);
                                    lightColor.Z = Math.Max(lightColor.Z, 50f / 255f);
                                }
                                // Spelunker Potion
                                else if (
                                    _isSpelunkerActive && Main.IsTileSpelunkable(x, y)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 200f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 170f / 255f);
                                }
                                // Biome Sight Potion
                                else if (
                                    _isBiomeSightActive
                                    && Main.IsTileBiomeSightable(
                                        x,
                                        y,
                                        ref biomeSightColor
                                    )
                                )
                                {
                                    lightColor.X = Math.Max(
                                        lightColor.X,
                                        (1f / 255f) * biomeSightColor.R
                                    );
                                    lightColor.Y = Math.Max(
                                        lightColor.Y,
                                        (1f / 255f) * biomeSightColor.G
                                    );
                                    lightColor.Z = Math.Max(
                                        lightColor.Z,
                                        (1f / 255f) * biomeSightColor.B
                                    );
                                }

                                TileShine(ref lightColor, tile);

                                VectorToColor.Assign(
                                    ref _finalLights[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }
            else
            {
                _glowingTileColors[TileID.MartianConduitPlating] =
                    MartianConduitPlatingGlowColor();

                _glowingTileColors[TileID.RainbowMoss] =
                    _glowingTileColors[TileID.RainbowMossBrick] =
                    _glowingTileColors[TileID.RainbowMossBlock] =
                    _glowingTileColors[TileID.RainbowBrick] =
                        RainbowGlowColor();

                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                    },
                    (x1) =>
                    {
                        var i = (height * x1) + offset;
                        var x = x1 + xmin;
                        for (var y = clampedYmin; y < clampedYmax; ++y)
                        {
                            try
                            {
                                Vector3.Multiply(
                                    ref _lights[i],
                                    brightness,
                                    out var lightColor
                                );
                                var tile = Main.tile[x, y];

                                var biomeSightColor = new Color();

                                // Illuminant Paint and Shimmer
                                if (tile.IsTileFullbright || HasShimmer(tile))
                                {
                                    lightColor.X = Math.Max(lightColor.X, brightness);
                                    lightColor.Y = Math.Max(lightColor.Y, brightness);
                                    lightColor.Z = Math.Max(lightColor.Z, brightness);
                                }
                                // Glowing tiles
                                else if (_glowingTiles[tile.TileType])
                                {
                                    ref var glow = ref _glowingTileColors[tile.TileType];

                                    lightColor.X = Math.Max(
                                        lightColor.X,
                                        glowMult * glow.R
                                    );
                                    lightColor.Y = Math.Max(
                                        lightColor.Y,
                                        glowMult * glow.G
                                    );
                                    lightColor.Z = Math.Max(
                                        lightColor.Z,
                                        glowMult * glow.B
                                    );
                                }
                                // Dangersense Potion
                                else if (
                                    _isDangersenseActive
                                    && TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 255f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 50f / 255f);
                                    lightColor.Z = Math.Max(lightColor.Z, 50f / 255f);
                                }
                                // Spelunker Potion
                                else if (
                                    _isSpelunkerActive && Main.IsTileSpelunkable(x, y)
                                )
                                {
                                    lightColor.X = Math.Max(lightColor.X, 200f / 255f);
                                    lightColor.Y = Math.Max(lightColor.Y, 170f / 255f);
                                }
                                // Biome Sight Potion
                                else if (
                                    _isBiomeSightActive
                                    && Main.IsTileBiomeSightable(
                                        x,
                                        y,
                                        ref biomeSightColor
                                    )
                                )
                                {
                                    lightColor.X = Math.Max(
                                        lightColor.X,
                                        (1f / 255f) * biomeSightColor.R
                                    );
                                    lightColor.Y = Math.Max(
                                        lightColor.Y,
                                        (1f / 255f) * biomeSightColor.G
                                    );
                                    lightColor.Z = Math.Max(
                                        lightColor.Z,
                                        (1f / 255f) * biomeSightColor.B
                                    );
                                }

                                TileShine(ref lightColor, tile);

                                VectorToColor.Assign(
                                    ref _finalLights[i],
                                    multFromOverbright,
                                    lightColor
                                );
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                            }

                            ++i;
                        }
                    }
                );
            }

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureUtil.MakeAtLeastSize(ref _colors, height, width);

            _colors.SetData(0, _lightMapRenderArea, _finalLights, 0, length);

            _smoothLightingForeComplete = !cameraMode;
        }
    }

    private void RenderHiResLighting(
        Texture2D lights,
        ref RenderTarget2D hiResLights,
        bool simulateNormalMaps
    )
    {
        TextureUtil.MakeAtLeastSize(
            ref hiResLights,
            16 * lights.Width,
            16 * lights.Height
        );

        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var doDitheringSecond = (simulateNormalMaps || doOverbright) && hiDef;

        Main.graphics.GraphicsDevice.SetRenderTarget(hiResLights);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        if (doDitheringSecond)
        {
            _bicubicNoDitherHiDefShader
                .SetParameter("LightMapSize", lights.Size())
                .SetParameter(
                    "PixelSize",
                    new Vector2(1f / lights.Width, 1f / lights.Height)
                )
                .Apply();
        }
        else
        {
            _bicubicDitherShader
                .SetParameter("LightMapSize", lights.Size())
                .SetParameter(
                    "PixelSize",
                    new Vector2(1f / lights.Width, 1f / lights.Height)
                )
                .SetParameter(
                    "DitherCoordMult",
                    new Vector2(
                        16f * lights.Width / _ditherNoise.Width,
                        16f * lights.Height / _ditherNoise.Height
                    )
                )
                .Apply();
            Main.graphics.GraphicsDevice.Textures[4] = _ditherNoise;
            Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointWrap;
        }

        Main.spriteBatch.Draw(
            lights,
            Vector2.Zero,
            _lightMapRenderArea,
            Color.White,
            0f,
            Vector2.Zero,
            new Vector2(16f),
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();
    }

    internal void DrawSmoothLighting(
        RenderTarget2D target,
        bool background,
        bool disableNormalMaps = false,
        RenderTarget2D tmpTarget = null,
        RenderTarget2D ambientOcclusionTarget = null
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        if (!background && !_smoothLightingForeComplete)
        {
            return;
        }

        if (background && !_smoothLightingBackComplete)
        {
            return;
        }

        var doScaling = tmpTarget is not null;
        Vector2 offset;
        if (tmpTarget is null)
        {
            TextureUtil.MakeSize(ref _drawTarget1, target.Width, target.Height);
            tmpTarget = _drawTarget1;
            offset = new Vector2(Main.offScreenRange);
        }
        else
        {
            offset =
                (tmpTarget.Size() - new Vector2(Main.screenWidth, Main.screenHeight))
                / 2f;
        }

        var lightMapTexture = background ? _colorsBackground : _colors;

        if (
            (LightingConfig.Instance.SimulateNormalMaps && !disableNormalMaps)
            || LightingConfig.Instance.DrawOverbright()
        )
        {
            TextureUtil.MakeAtLeastSize(
                ref _drawTarget2,
                tmpTarget.Width,
                tmpTarget.Height
            );
        }

        ApplySmoothLighting(
            lightMapTexture,
            tmpTarget,
            _drawTarget2,
            _lightMapPosition,
            Main.screenPosition - offset,
            doScaling && !FancyLightingMod._inCameraMode
                ? Main.GameViewMatrix.Zoom
                : Vector2.One,
            target,
            background,
            disableNormalMaps,
            doScaling,
            ambientOcclusionTarget
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(target);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(tmpTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
    }

    internal RenderTarget2D GetCameraModeRenderTarget(RenderTarget2D screenTarget)
    {
        TextureUtil.MakeSize(
            ref _cameraModeTarget1,
            screenTarget.Width,
            screenTarget.Height
        );
        return _cameraModeTarget1;
    }

    internal void DrawSmoothLightingCameraMode(
        RenderTarget2D screenTarget,
        RenderTarget2D target,
        bool background,
        bool skipFinalPass = false,
        bool disableNormalMaps = false,
        bool tileEntities = false,
        RenderTarget2D ambientOcclusionTarget = null,
        Texture2D glow = null,
        Texture2D lightedGlow = null
    )
    {
        var lightMapTexture = background ? _colorsBackground : _colors;

        TextureUtil.MakeAtLeastSize(
            ref _cameraModeTarget2,
            16 * lightMapTexture.Height,
            16 * lightMapTexture.Width
        );
        TextureUtil.MakeAtLeastSize(
            ref _cameraModeTarget3,
            16 * lightMapTexture.Height,
            16 * lightMapTexture.Width
        );

        ApplySmoothLighting(
            lightMapTexture,
            _cameraModeTarget2,
            _cameraModeTarget3,
            _lightMapPosition,
            16f
                * new Vector2(
                    FancyLightingMod._cameraModeArea.X,
                    FancyLightingMod._cameraModeArea.Y
                ),
            Vector2.One,
            target,
            background,
            disableNormalMaps,
            tileEntities,
            ambientOcclusionTarget
        );

        if (skipFinalPass)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(screenTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(_cameraModeTarget1, Vector2.Zero, Color.White);
        if (glow is null)
        {
            Main.spriteBatch.Draw(_cameraModeTarget2, Vector2.Zero, Color.White);
        }
        else
        {
            if (lightedGlow is null)
            {
                _glowMaskShader
                    .SetParameter(
                        "GlowCoordMult",
                        new Vector2(
                            (float)_cameraModeTarget2.Width / glow.Width,
                            (float)_cameraModeTarget2.Height / glow.Height
                        )
                    )
                    .Apply();
            }
            else
            {
                _enhancedGlowMaskShader
                    .SetParameter(
                        "GlowCoordMult",
                        new Vector2(
                            (float)_cameraModeTarget2.Width / glow.Width,
                            (float)_cameraModeTarget2.Height / glow.Height
                        )
                    )
                    .SetParameter(
                        "LightedGlowCoordMult",
                        new Vector2(
                            (float)_cameraModeTarget2.Width / lightedGlow.Width,
                            (float)_cameraModeTarget2.Height / lightedGlow.Height
                        )
                    )
                    .Apply();
                Main.graphics.GraphicsDevice.Textures[5] = lightedGlow;
                Main.graphics.GraphicsDevice.SamplerStates[5] = SamplerState.PointClamp;
            }

            Main.graphics.GraphicsDevice.Textures[4] = glow;
            Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointClamp;
            Main.spriteBatch.Draw(_cameraModeTarget2, Vector2.Zero, Color.White);
        }
        Main.spriteBatch.End();
    }

    private void ApplySmoothLighting(
        Texture2D lightMapTexture,
        RenderTarget2D target1,
        RenderTarget2D target2,
        Vector2 lightMapPosition,
        Vector2 positionOffset,
        Vector2 zoom,
        RenderTarget2D worldTarget,
        bool background,
        bool disableNormalMaps,
        bool doScaling,
        RenderTarget2D ambientOcclusionTarget
    )
    {
        var fineNormalMaps = PreferencesConfig.Instance.FineNormalMaps;
        var doBicubicUpscaling = LightingConfig.Instance.UseBicubicScaling();
        var simulateNormalMaps =
            !disableNormalMaps && LightingConfig.Instance.SimulateNormalMaps;
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var lightOnly = PreferencesConfig.Instance.RenderOnlyLight;
        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var doDitheringSecond = (simulateNormalMaps || doOverbright) && hiDef;
        var doGamma = LightingConfig.Instance.DoGammaCorrection();
        var doAmbientOcclusion = background && ambientOcclusionTarget is not null;
        var doOneStepOnly = !(simulateNormalMaps || doOverbright);

        if (doBicubicUpscaling)
        {
            if (background)
            {
                if (!_smoothLightingBackHiRes)
                {
                    RenderHiResLighting(
                        lightMapTexture,
                        ref _colorsBackgroundHiRes,
                        simulateNormalMaps
                    );
                    _smoothLightingBackHiRes = !hiDef || doOverbright;
                    // If hiDef is true and doOverbright is false, whether normal maps are enabled
                    // affects the result of the hi-res light map
                }

                lightMapTexture = _colorsBackgroundHiRes;
            }
            else
            {
                if (!_smoothLightingForeHiRes)
                {
                    RenderHiResLighting(
                        lightMapTexture,
                        ref _colorsHiRes,
                        simulateNormalMaps
                    );
                    _smoothLightingForeHiRes = !hiDef || doOverbright;
                }

                lightMapTexture = _colorsHiRes;
            }
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(doOneStepOnly ? target1 : target2);
        if (doOneStepOnly)
        {
            Main.graphics.GraphicsDevice.Clear(Color.White);
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            doOneStepOnly ? FancyLightingMod.MultiplyBlend : BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        var flippedGravity =
            doScaling
            && Main.LocalPlayer.gravDir == -1
            && !FancyLightingMod._inCameraMode;

        lightMapPosition -= positionOffset;
        var angle = (float)(Math.PI / 2.0);
        if (flippedGravity)
        {
            angle *= -1f;
            var top = (16f * _lightMapTileArea.Y) - positionOffset.Y;
            var bottom = top + (16f * _lightMapTileArea.Height);
            var targetMiddle = worldTarget.Height / 2f;
            lightMapPosition.Y -= bottom - targetMiddle - (targetMiddle - top);
            lightMapPosition += _lightMapPositionFlipped - _lightMapPosition;
        }

        var lightMapRectangle = _lightMapRenderArea;
        if (doBicubicUpscaling)
        {
            lightMapRectangle.X *= 16;
            lightMapRectangle.Y *= 16;
            lightMapRectangle.Width *= 16;
            lightMapRectangle.Height *= 16;
        }

        var lightMapScale = doBicubicUpscaling ? 1f : 16f;
        Main.spriteBatch.Draw(
            lightMapTexture,
            (zoom * (lightMapPosition - (target1.Size() / 2f))) + (target1.Size() / 2f),
            lightMapRectangle,
            Color.White,
            angle,
            Vector2.Zero,
            lightMapScale * new Vector2(zoom.Y, zoom.X),
            flippedGravity ? SpriteEffects.None : SpriteEffects.FlipVertically,
            0f
        );

        if (!doOneStepOnly)
        {
            Main.spriteBatch.End();

            Main.graphics.GraphicsDevice.SetRenderTarget(target1);
            if (!doOverbright)
            {
                Main.graphics.GraphicsDevice.Clear(Color.White);
            }

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                doOverbright ? BlendState.Opaque : FancyLightingMod.MultiplyBlend,
                simulateNormalMaps ? SamplerState.LinearClamp : SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            var shader = simulateNormalMaps
                ? doOverbright
                    ? lightOnly
                        ? background
                            ? doAmbientOcclusion
                                ? _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader
                                : _normalsOverbrightLightOnlyOpaqueShader
                            : _normalsOverbrightLightOnlyShader
                        : doAmbientOcclusion
                            ? _normalsOverbrightAmbientOcclusionShader
                            : _normalsOverbrightShader
                    : _normalsShader
                : doScaling // doOverbright is guaranteed to be true here
                    ? _overbrightMaxShader // if doScaling is true we're rendering tile entities, waterfalls, NPCs, etc.
                    : lightOnly
                        ? background
                            ? doAmbientOcclusion
                                ? _overbrightLightOnlyOpaqueAmbientOcclusionShader
                                : _overbrightLightOnlyOpaqueShader
                            : _overbrightLightOnlyShader
                        : doAmbientOcclusion
                            ? _overbrightAmbientOcclusionShader
                            : _overbrightShader;

            var normalMapResolution = fineNormalMaps ? 1f : 2f;
            var normalMapRadius = 12.5f;
            var normalMapMult = PreferencesConfig.Instance.NormalMapsMultiplier();
            if (fineNormalMaps)
            {
                normalMapMult *= 2f;
            }
            var normalMapStrength = 1f / (1f + normalMapMult);
            var overbrightMult = doOverbright
                ? hiDef
                    ? 65535f / 4096
                    : 255f / 128
                : 1f;

            shader
                .SetParameter("OverbrightMult", overbrightMult)
                .SetParameter(
                    "NormalMapResolution",
                    new Vector2(
                        normalMapResolution / worldTarget.Width,
                        normalMapResolution / worldTarget.Height
                    )
                )
                .SetParameter(
                    "NormalMapRadius",
                    new Vector2(
                        normalMapRadius / target2.Width,
                        normalMapRadius / target2.Height
                    )
                )
                .SetParameter("NormalMapStrength", normalMapStrength)
                .SetParameter(
                    "WorldCoordMult",
                    new Vector2(
                        (float)target2.Width / worldTarget.Width,
                        (float)target2.Height / worldTarget.Height
                    )
                );
            Main.graphics.GraphicsDevice.Textures[4] = worldTarget;
            Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointClamp;
            if (doDitheringSecond)
            {
                shader.SetParameter(
                    "DitherCoordMult",
                    new Vector2(
                        (float)target2.Width / _ditherNoise.Width,
                        (float)target2.Height / _ditherNoise.Height
                    )
                );
                Main.graphics.GraphicsDevice.Textures[5] = _ditherNoise;
                Main.graphics.GraphicsDevice.SamplerStates[5] = SamplerState.PointWrap;
            }

            if (doAmbientOcclusion)
            {
                shader.SetParameter(
                    "AmbientOcclusionCoordMult",
                    new Vector2(
                        (float)target2.Width / ambientOcclusionTarget.Width,
                        (float)target2.Height / ambientOcclusionTarget.Height
                    )
                );
                Main.graphics.GraphicsDevice.Textures[6] = ambientOcclusionTarget;
                Main.graphics.GraphicsDevice.SamplerStates[6] = SamplerState.PointClamp;
            }

            shader.Apply();

            Main.spriteBatch.Draw(target2, Vector2.Zero, Color.White);
        }

        if (!(doOverbright || lightOnly))
        {
            if (doBicubicUpscaling || simulateNormalMaps)
            {
                _noFilterShader.Apply();
            }

            Main.spriteBatch.Draw(worldTarget, Vector2.Zero, Color.White);
        }

        Main.spriteBatch.End();
    }

    internal void DrawGlow(
        Texture2D lighted,
        Texture2D glow,
        Texture2D lightedGlow = null
    )
    {
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        if (lightedGlow is null)
        {
            _glowMaskShader
                .SetParameter(
                    "GlowCoordMult",
                    new Vector2(
                        (float)lighted.Width / glow.Width,
                        (float)lighted.Height / glow.Height
                    )
                )
                .Apply();
        }
        else
        {
            _enhancedGlowMaskShader
                .SetParameter(
                    "GlowCoordMult",
                    new Vector2(
                        (float)lighted.Width / glow.Width,
                        (float)lighted.Height / glow.Height
                    )
                )
                .SetParameter(
                    "LightedGlowCoordMult",
                    new Vector2(
                        (float)lighted.Width / lightedGlow.Width,
                        (float)lighted.Height / lightedGlow.Height
                    )
                )
                .Apply();
            Main.graphics.GraphicsDevice.Textures[5] = lightedGlow;
            Main.graphics.GraphicsDevice.SamplerStates[5] = SamplerState.PointClamp;
        }

        Main.graphics.GraphicsDevice.Textures[4] = glow;
        Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointClamp;
        Main.spriteBatch.Draw(lighted, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
    }
}
