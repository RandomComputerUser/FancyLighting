using System;
using System.Threading;
using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class SmoothLighting
{
    private Texture2D _colors;
    private RenderTarget2D _colorsHiRes;

    private readonly Texture2D _ditherNoise;

    private Rectangle _lightMapTileArea;

    private RenderTarget2D _drawTarget;

    private Vector3[] _lights;
    private bool[] _hasLight;
    private Rgba1010102[] _finalLights;
    private HalfVector4[] _finalLightsHiDef;

    internal Vector3[] _whiteLights;
    internal Vector3[] _tmpLights;
    internal Vector3[] _blackLights;

    private bool _smoothLightingLightMapValid;
    private bool _smoothLightingComplete;
    private bool _smoothLightingHiResComplete;

    internal RenderTarget2D _cameraModeTarget1;
    internal RenderTarget2D _cameraModeTarget2;

    internal bool CanDrawSmoothLighting =>
        _smoothLightingComplete && LightingConfig.Instance.SmoothLightingEnabled();

    private readonly FancyLightingMod _modInstance;

    private Shader _bicubicFilteringShader;
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
    private Shader _brightenShader;
    private Shader _glowMaskShader;
    private Shader _enhancedGlowMaskShader;

    public SmoothLighting(FancyLightingMod mod)
    {
        _modInstance = mod;

        _lightMapTileArea = new(0, 0, 0, 0);

        _smoothLightingLightMapValid = false;
        _smoothLightingComplete = false;
        _smoothLightingHiResComplete = false;

        _tmpLights = null;

        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _bicubicFilteringShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "BicubicFiltering"
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
        _brightenShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Brighten"
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
        _drawTarget?.Dispose();
        _colors?.Dispose();
        _colorsHiRes?.Dispose();
        _cameraModeTarget1?.Dispose();
        _cameraModeTarget2?.Dispose();
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _bicubicFilteringShader);
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
        EffectLoader.UnloadEffect(ref _brightenShader);
        EffectLoader.UnloadEffect(ref _glowMaskShader);
        EffectLoader.UnloadEffect(ref _enhancedGlowMaskShader);
    }

    internal void InvalidateSmoothLighting() => _smoothLightingComplete = false;

    internal void ApplyLightOnlyShader() => _lightOnlyShader.Apply();

    internal void ApplyBrightenShader(float brightness) =>
        _brightenShader.SetParameter("BrightnessMult", brightness).Apply();

    private void PrintException()
    {
        LightingConfig.Instance.UseSmoothLighting = false;
        var prefix = ModLoader.HasMod("ChatSource") ? string.Empty : "[Fancy Lighting] ";
        Main.NewText(
            $"{prefix}An error occurred while trying to use smooth lighting.",
            Color.Orange
        );
        Main.NewText(
            $"{prefix}Smooth lighting has been automatically disabled.",
            Color.Orange
        );
    }

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
        _smoothLightingComplete = false;

        var length = width * height;

        ArrayUtils.MakeAtLeastSize(ref _lights, length);
        ArrayUtils.MakeAtLeastSize(ref _whiteLights, length);
        ArrayUtils.MakeAtLeastSize(ref _blackLights, length);
        ArrayUtils.MakeAtLeastSize(ref _hasLight, length);

        if (width == 0 || height == 0)
        {
            return;
        }

        if (colors.Length < length)
        {
            return;
        }

        var caughtException = 0;
        var doGammaCorrection = LightingConfig.Instance.HiDefFeaturesEnabled();
        var blurLightMap = LightingConfig.Instance.UseLightMapBlurring;
        var doToneMap = LightingConfig.Instance.UseLightMapToneMapping();

        if (doGammaCorrection && !LightingConfig.Instance.FancyLightingEngineEnabled())
        {
            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (x) =>
                {
                    var i = height * x;
                    for (var y = 0; y < height; ++y)
                    {
                        try
                        {
                            ColorUtils.GammaToLinear(ref colors[i++]);
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

            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (x) =>
                {
                    var i = height * x;
                    for (var y = 0; y < height; ++y)
                    {
                        try
                        {
                            ref var lightColor = ref lights[i++];

                            ColorUtils.GammaToLinear(ref lightColor);
                            ToneMapping.ToneMap(ref lightColor);
                            ColorUtils.LinearToGamma(ref lightColor);
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

        if (doGammaCorrection)
        {
            var lights = blurLightMap ? _lights : colors;
            var otherLights = blurLightMap ? colors : _lights;

            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (x) =>
                {
                    var i = height * x;
                    for (var y = 0; y < height; ++y)
                    {
                        try
                        {
                            ColorUtils.LinearToGamma(ref lights[i]);
                            lights[i++] *= PostProcessing.HiDefBrightnessScale;
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

        var low = (1f / 255f) / Lighting.GlobalBrightness;

        Parallel.For(
            0,
            width,
            SettingsSystem._parallelOptions,
            (x) =>
            {
                var i = height * x;
                var end = i + height;
                while (i < end)
                {
                    try
                    {
                        ref var color = ref _lights[i];
                        _hasLight[i++] =
                            color.X >= low || color.Y >= low || color.Z >= low;
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

        Parallel.For(
            1,
            width - 1,
            SettingsSystem._parallelOptions,
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
                            _hasLight[i]
                            || _hasLight[i - 1]
                            || _hasLight[i + 1]
                            || _hasLight[i - height]
                            || _hasLight[i - height - 1]
                            || _hasLight[i - height + 1]
                            || _hasLight[i + height]
                            || _hasLight[i + height - 1]
                            || _hasLight[i + height + 1]
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
            if (PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque)
            {
                Parallel.For(
                    1,
                    width - 1,
                    SettingsSystem._parallelOptions,
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
                    SettingsSystem._parallelOptions,
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
                                var isNonSolid = TileUtils.IsNonSolid(tileX, tileY);

                                var upperLeftMult =
                                    lightMasks[i - height - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX - 1, tileY - 1)
                                            == isNonSolid
                                    )
                                        ? 1f
                                        : 0f;
                                var leftMult =
                                    lightMasks[i - height] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX - 1, tileY)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var lowerLeftMult =
                                    lightMasks[i - height + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX - 1, tileY + 1)
                                            == isNonSolid
                                    )
                                        ? 1f
                                        : 0f;
                                var upperMult =
                                    lightMasks[i - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX, tileY - 1)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var middleMult = isSolid && !isNonSolid ? 12f : 4f;
                                var lowerMult =
                                    lightMasks[i + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX, tileY + 1)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var upperRightMult =
                                    lightMasks[i + height - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX + 1, tileY - 1)
                                            == isNonSolid
                                    )
                                        ? 1f
                                        : 0f;
                                var rightMult =
                                    lightMasks[i + height] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX + 1, tileY)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var lowerRightMult =
                                    lightMasks[i + height + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX + 1, tileY + 1)
                                            == isNonSolid
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
                SettingsSystem._parallelOptions,
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

    internal void CalculateSmoothLighting(bool cameraMode = false)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        if (!_smoothLightingLightMapValid)
        {
            return;
        }

        if (!cameraMode && _smoothLightingComplete)
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

        if (LightingConfig.Instance.DrawOverbright())
        {
            CalculateSmoothLightingHdr(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                cameraMode
            );
        }
        else
        {
            CalculateSmoothLightingLdr(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                cameraMode
            );
        }

        _smoothLightingHiResComplete = false;
    }

    private void CalculateSmoothLightingHdr(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool cameraMode
    )
    {
        var length = width * height;

        ArrayUtils.MakeAtLeastSize(ref _finalLightsHiDef, length);
        _finalLights = null; // Save some memory

        var caughtException = 0;

        var brightness = Lighting.GlobalBrightness;
        Parallel.For(
            clampedStart,
            clampedEnd,
            SettingsSystem._parallelOptions,
            (x1) =>
            {
                var i = (height * x1) + offset;
                var x = x1 + xmin;
                for (var y = clampedYmin; y < clampedYmax; ++y)
                {
                    try
                    {
                        Vector3.Multiply(ref _lights[i], brightness, out var lightColor);
                        var tile = Main.tile[x, y];

                        if (TileUtils.HasShimmer(tile))
                        {
                            lightColor.X = Math.Max(lightColor.X, 1f);
                            lightColor.Y = Math.Max(lightColor.Y, 1f);
                            lightColor.Z = Math.Max(lightColor.Z, 1f);
                        }

                        TileShine(ref lightColor, tile);
                        ColorUtils.Assign(ref _finalLightsHiDef[i], lightColor);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        Interlocked.Exchange(ref caughtException, 1);
                    }

                    ++i;
                }
            }
        );

        if (caughtException == 1)
        {
            PrintException();
            return;
        }

        TextureUtils.MakeSize(ref _colors, height, width, SurfaceFormat.HalfVector4);

        _colors.SetData(_finalLightsHiDef, 0, length);

        _smoothLightingComplete = !cameraMode;
    }

    private void CalculateSmoothLightingLdr(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool cameraMode
    )
    {
        var length = width * height;

        ArrayUtils.MakeAtLeastSize(ref _finalLights, length);
        _finalLightsHiDef = null; // Save some memory

        var caughtException = 0;

        var brightness = Lighting.GlobalBrightness;
        Parallel.For(
            clampedStart,
            clampedEnd,
            SettingsSystem._parallelOptions,
            (x1) =>
            {
                var i = (height * x1) + offset;
                var x = x1 + xmin;
                for (var y = clampedYmin; y < clampedYmax; ++y)
                {
                    try
                    {
                        Vector3.Multiply(ref _lights[i], brightness, out var lightColor);
                        var tile = Main.tile[x, y];

                        if (TileUtils.HasShimmer(tile))
                        {
                            lightColor.X = Math.Max(lightColor.X, 1f);
                            lightColor.Y = Math.Max(lightColor.Y, 1f);
                            lightColor.Z = Math.Max(lightColor.Z, 1f);
                        }

                        TileShine(ref lightColor, tile);
                        ColorUtils.Assign(ref _finalLights[i], 1f, lightColor);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        Interlocked.Exchange(ref caughtException, 1);
                    }

                    ++i;
                }
            }
        );

        if (caughtException == 1)
        {
            PrintException();
            return;
        }

        TextureUtils.MakeSize(ref _colors, height, width, SurfaceFormat.Rgba1010102);

        _colors.SetData(_finalLights, 0, length);

        _smoothLightingComplete = !cameraMode;
    }

    private void RenderHiResLighting(Texture2D lights, ref RenderTarget2D hiResLights)
    {
        TextureUtils.MakeSize(
            ref hiResLights,
            4 * lights.Width,
            4 * lights.Height,
            TextureUtils.LightMapFormat
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(hiResLights);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _bicubicFilteringShader
            .SetParameter("LightMapSize", lights.Size())
            .SetParameter("PixelSize", new Vector2(1f / lights.Width, 1f / lights.Height))
            .Apply();
        Main.spriteBatch.Draw(
            lights,
            Vector2.Zero,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            4f,
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
        if (!_smoothLightingComplete)
        {
            return;
        }

        var doScaling = tmpTarget is not null;
        Vector2 offset;
        if (tmpTarget is null)
        {
            TextureUtils.MakeSize(
                ref _drawTarget,
                target.Width,
                target.Height,
                TextureUtils.ScreenFormat
            );
            tmpTarget = _drawTarget;
            offset = new Vector2(Main.offScreenRange);
        }
        else
        {
            offset =
                (tmpTarget.Size() - new Vector2(Main.screenWidth, Main.screenHeight))
                / 2f;
        }

        var lightMapTexture = _colors;

        ApplySmoothLighting(
            lightMapTexture,
            tmpTarget,
            16f * new Vector2(_lightMapTileArea.X, _lightMapTileArea.Y),
            Main.screenPosition - offset,
            doScaling && !FancyLightingMod._inCameraMode
                ? Main.GameViewMatrix.Zoom
                    * new Vector2(1f, Main.LocalPlayer.gravDir < 0f ? -1f : 1f)
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
        TextureUtils.MakeSize(
            ref _cameraModeTarget1,
            screenTarget.Width,
            screenTarget.Height,
            TextureUtils.ScreenFormat
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
        var lightMapTexture = _colors;

        TextureUtils.MakeSize(
            ref _cameraModeTarget2,
            16 * lightMapTexture.Height,
            16 * lightMapTexture.Width,
            TextureUtils.ScreenFormat
        );

        ApplySmoothLighting(
            lightMapTexture,
            _cameraModeTarget2,
            16f * new Vector2(_lightMapTileArea.X, _lightMapTileArea.Y),
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
        RenderTarget2D target,
        Vector2 lightMapPosition,
        Vector2 worldPosition,
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
        var doOneStepOnly = !(simulateNormalMaps || doOverbright);
        var doAmbientOcclusion = background && ambientOcclusionTarget is not null;
        var doDithering = doOverbright && !hiDef;

        var lightMapScale = 16f;

        if (doBicubicUpscaling)
        {
            if (!_smoothLightingHiResComplete)
            {
                RenderHiResLighting(lightMapTexture, ref _colorsHiRes);
                _smoothLightingHiResComplete = true;
            }

            lightMapTexture = _colorsHiRes;
            lightMapScale = 4f;
        }

        // We need to correct for the orientation of the light map texture
        // In the light map texture, X and Y are flipped compared to world coordinates

        var lightMapCenter =
            lightMapPosition
            + (
                0.5f
                * lightMapScale
                * new Vector2(lightMapTexture.Height, lightMapTexture.Width)
            );
        var worldCenter =
            worldPosition + (0.5f * new Vector2(worldTarget.Width, worldTarget.Height));
        var position =
            (zoom * (lightMapCenter - worldCenter))
            + (0.5f * new Vector2(worldTarget.Width, worldTarget.Height));
        var rotation = -MathHelper.PiOver2;
        var origin = 0.5f * new Vector2(lightMapTexture.Width, lightMapTexture.Height);
        var scale = lightMapScale * new Vector2(zoom.Y, zoom.X);

        Main.graphics.GraphicsDevice.SetRenderTarget(target);

        if (doOneStepOnly)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(
                lightMapTexture,
                position,
                null,
                Color.White,
                rotation,
                origin,
                scale,
                SpriteEffects.FlipHorizontally,
                0f
            );
            Main.spriteBatch.End();

            if (!lightOnly)
            {
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendStates.Multiply,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                Main.spriteBatch.Draw(worldTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }

            return;
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
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
                ? _overbrightMaxShader // if doScaling is true we're doing post-processing
                : lightOnly
                    ? background
                        ? doAmbientOcclusion
                            ? _overbrightLightOnlyOpaqueAmbientOcclusionShader
                            : _overbrightLightOnlyOpaqueShader
                        : _overbrightLightOnlyShader
                    : doAmbientOcclusion
                        ? _overbrightAmbientOcclusionShader
                        : _overbrightShader;

        var gamma = PreferencesConfig.Instance.GammaExponent();
        var normalMapResolution = fineNormalMaps ? 1f : 2f;
        var overbrightMult = doOverbright
            ? hiDef
                ? 1f / PostProcessing.HiDefBrightnessScale
                : 1f
            : 1f;
        var normalMapGradientMult = 24f * overbrightMult * zoom;
        var normalMapMult = PreferencesConfig.Instance.NormalMapsMultiplier();
        if (background)
        {
            normalMapMult *= 0.75f;
        }
        var normalMapStrength = 1f / (1f + normalMapMult);

        var worldCoordMult =
            new Vector2(scale.Y, scale.X)
            * new Vector2(lightMapTexture.Height, lightMapTexture.Width)
            / new Vector2(worldTarget.Width, worldTarget.Height);

        shader
            .SetParameter("ReciprocalGamma", 1f / gamma)
            .SetParameter(
                "NormalMapResolution",
                new Vector2(
                    normalMapResolution / worldTarget.Width,
                    normalMapResolution / worldTarget.Height
                )
            )
            .SetParameter("NormalMapGradientMult", normalMapGradientMult)
            .SetParameter("NormalMapStrength", normalMapStrength)
            .SetParameter("WorldCoordMult", worldCoordMult)
            .SetParameter(
                "WorldCoordOffset",
                (position / new Vector2(worldTarget.Width, worldTarget.Height))
                    - (worldCoordMult * new Vector2(0.5f))
            );

        Main.graphics.GraphicsDevice.Textures[4] = worldTarget;
        Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointClamp;

        if (doAmbientOcclusion)
        {
            Main.graphics.GraphicsDevice.Textures[5] = ambientOcclusionTarget;
            Main.graphics.GraphicsDevice.SamplerStates[5] = SamplerState.PointClamp;
        }

        if (doDithering)
        {
            shader
                .SetParameter(
                    "DitherCoordMult",
                    new Vector2(
                        4f * zoom.Y * lightMapTexture.Width / _ditherNoise.Width,
                        4f * zoom.X * lightMapTexture.Height / _ditherNoise.Height
                    )
                )
                .Apply();
            Main.graphics.GraphicsDevice.Textures[6] = _ditherNoise;
            Main.graphics.GraphicsDevice.SamplerStates[6] = SamplerState.PointWrap;
        }

        shader.Apply();
        Main.spriteBatch.Draw(
            lightMapTexture,
            position,
            null,
            Color.White,
            rotation,
            origin,
            scale,
            SpriteEffects.FlipHorizontally,
            0f
        );
        Main.spriteBatch.End();

        if (!doOverbright && !lightOnly)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendStates.Multiply,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(worldTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }
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
